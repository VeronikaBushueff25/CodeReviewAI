using CodeReview.Application.Abstractions.AI;
using CodeReview.Application.Abstractions.Services;
using CodeReview.Application.Common.Exceptions;
using CodeReview.Application.DTOs;
using CodeReview.Domain.Entities;
using CodeReview.Domain.Enums;
using CodeReview.Domain.Interfaces;
using CodeReview.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodeReview.Application.Features.Analyses.Commands;

// ─── Command ────────────────────────────────────────────────────────────────
public sealed record CreateAnalysisCommand : IRequest<AnalysisSummaryDto>
{
    public required Guid UserId { get; init; }
    public required string Title { get; init; }
    public required string Code { get; init; }
    public CodeLanguage? Language { get; init; }
    public string? FileName { get; init; }
    public CodeSourceType SourceType { get; init; } = CodeSourceType.ManualInput;
    public string? RepositoryFullName { get; init; }
    public int? PullRequestNumber { get; init; }
}

// ─── Validator ───────────────────────────────────────────────────────────────
public sealed class CreateAnalysisCommandValidator : AbstractValidator<CreateAnalysisCommand>
{
    private const int MaxCodeSizeBytes = 512_000; // 512 KB

    public CreateAnalysisCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code content is required.")
            .Must(code => System.Text.Encoding.UTF8.GetByteCount(code) <= MaxCodeSizeBytes)
            .WithMessage($"Code size must not exceed {MaxCodeSizeBytes / 1024}KB.");

        When(x => x.SourceType == CodeSourceType.GitHubRepository || x.SourceType == CodeSourceType.GitHubPullRequest, () =>
        {
            RuleFor(x => x.RepositoryFullName)
                .NotEmpty().WithMessage("Repository name is required for GitHub sources.");
        });

        When(x => x.SourceType == CodeSourceType.GitHubPullRequest, () =>
        {
            RuleFor(x => x.PullRequestNumber)
                .NotNull().GreaterThan(0).WithMessage("Pull request number must be provided.");
        });
    }
}

// ─── Handler ─────────────────────────────────────────────────────────────────
public sealed class CreateAnalysisCommandHandler : IRequestHandler<CreateAnalysisCommand, AnalysisSummaryDto>
{
    private readonly IAnalysisRepository _analysisRepo;
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStaticCodeAnalyzer _staticAnalyzer;
    private readonly IAiCodeAnalyzer _aiAnalyzer;
    private readonly IAnalysisNotificationService _notifications;
    private readonly ILogger<CreateAnalysisCommandHandler> _logger;

    public CreateAnalysisCommandHandler(
        IAnalysisRepository analysisRepo,
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IStaticCodeAnalyzer staticAnalyzer,
        IAiCodeAnalyzer aiAnalyzer,
        IAnalysisNotificationService notifications,
        ILogger<CreateAnalysisCommandHandler> logger)
    {
        _analysisRepo = analysisRepo;
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _staticAnalyzer = staticAnalyzer;
        _aiAnalyzer = aiAnalyzer;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<AnalysisSummaryDto> Handle(CreateAnalysisCommand cmd, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(cmd.UserId, ct)
            ?? throw new NotFoundException(nameof(User), cmd.UserId);

        // Detect language if not specified
        var language = cmd.Language ?? _staticAnalyzer.DetectLanguage(cmd.Code, cmd.FileName);

        // Build CodeSource value object based on source type
        var source = cmd.SourceType switch
        {
            CodeSourceType.FileUpload => CodeSource.FromFile(cmd.Code, cmd.FileName!),
            CodeSourceType.GitHubRepository => CodeSource.FromGitHub(cmd.Code, cmd.RepositoryFullName!),
            CodeSourceType.GitHubPullRequest => CodeSource.FromPullRequest(cmd.Code, cmd.RepositoryFullName!, cmd.PullRequestNumber!.Value),
            _ => CodeSource.FromManualInput(cmd.Code)
        };

        // Create aggregate
        var analysis = CodeAnalysis.Create(cmd.UserId, cmd.Title, source, language);

        await _analysisRepo.AddAsync(analysis, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Analysis {AnalysisId} created for user {UserId}", analysis.Id, cmd.UserId);

        // Trigger background processing (fire-and-forget with proper error handling)
        _ = ProcessAnalysisAsync(analysis.Id, cmd.Code, language, cmd.UserId, ct);

        return MapToSummary(analysis);
    }

    /// <summary>
    /// Runs the full analysis pipeline asynchronously:
    /// 1. Static metrics (cyclomatic complexity, naming, duplication)
    /// 2. AI analysis (SOLID violations, anti-patterns, readability)
    /// 3. Score aggregation
    /// 4. Persist results
    /// 5. Notify client via SignalR
    /// </summary>
    private async Task ProcessAnalysisAsync(Guid analysisId, string code, CodeLanguage language, Guid userId, CancellationToken ct)
    {
        try
        {
            await _notifications.NotifyAnalysisStartedAsync(userId, analysisId, ct);

            var analysis = await _analysisRepo.GetWithIssuesAsync(analysisId, ct)
                ?? throw new InvalidOperationException($"Analysis {analysisId} not found during processing");

            analysis.Start();
            await _unitOfWork.SaveChangesAsync(ct);

            // Stage 1: Static analysis
            await _notifications.NotifyAnalysisProgressAsync(userId, analysisId, 25, "Running static analysis...", ct);
            var metrics = await _staticAnalyzer.AnalyzeAsync(code, language, ct);

            // Stage 2: AI analysis
            await _notifications.NotifyAnalysisProgressAsync(userId, analysisId, 50, "Running AI analysis...", ct);
            var aiResult = await _aiAnalyzer.AnalyzeAsync(code, language, metrics, ct);

            // Stage 3: Build issues from AI result
            await _notifications.NotifyAnalysisProgressAsync(userId, analysisId, 75, "Aggregating results...", ct);
            var issues = aiResult.Issues.Select(i => AnalysisIssue.Create(
                analysisId: analysisId,
                title: i.Title,
                description: i.Description,
                severity: i.Severity,
                category: i.Category,
                lineStart: i.LineStart,
                lineEnd: i.LineEnd,
                codeSnippet: i.CodeSnippet,
                suggestion: i.Suggestion,
                isAiGenerated: true,
                confidence: i.Confidence
            )).ToList();

            // Stage 4: Complete aggregate
            analysis.Complete(aiResult.Score, metrics, issues);

            var user = await _userRepo.GetByIdAsync(userId, ct);
            user?.IncrementAnalysisCount();

            await _unitOfWork.SaveChangesAsync(ct);

            await _notifications.NotifyAnalysisCompletedAsync(userId, analysisId, ct);
            _logger.LogInformation("Analysis {AnalysisId} completed with score {Score}", analysisId, aiResult.Score.Overall);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis {AnalysisId} failed", analysisId);
            try
            {
                var analysis = await _analysisRepo.GetByIdAsync(analysisId, ct);
                analysis?.Fail(ex.Message);
                await _unitOfWork.SaveChangesAsync(ct);
                await _notifications.NotifyAnalysisFailedAsync(userId, analysisId, ex.Message, ct);
            }
            catch (Exception innerEx)
            {
                _logger.LogCritical(innerEx, "Failed to mark analysis {AnalysisId} as failed", analysisId);
            }
        }
    }

    private static AnalysisSummaryDto MapToSummary(CodeAnalysis analysis) => new()
    {
        Id = analysis.Id,
        Title = analysis.Title,
        Status = analysis.Status,
        Language = analysis.Language,
        SourceType = analysis.Source.Type,
        CreatedAt = analysis.CreatedAt
    };
}
