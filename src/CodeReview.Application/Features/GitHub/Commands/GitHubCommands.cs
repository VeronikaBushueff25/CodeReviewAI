using CodeReview.Application.Abstractions.Services;
using CodeReview.Application.Common.Exceptions;
using CodeReview.Application.DTOs;
using CodeReview.Application.Features.Analyses.Commands;
using CodeReview.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodeReview.Application.Features.GitHub.Commands;

// Connect GitHub OAuth
public sealed record ConnectGitHubCommand : IRequest<UserDto>
{
    public required Guid UserId { get; init; }
    public required string OAuthCode { get; init; }
}

public sealed class ConnectGitHubCommandHandler : IRequestHandler<ConnectGitHubCommand, UserDto>
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGitHubService _github;
    private readonly ILogger<ConnectGitHubCommandHandler> _logger;

    public ConnectGitHubCommandHandler(
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IGitHubService github,
        ILogger<ConnectGitHubCommandHandler> logger)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _github = github;
        _logger = logger;
    }

    public async Task<UserDto> Handle(ConnectGitHubCommand cmd, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(cmd.UserId, ct) ?? throw new NotFoundException("User", cmd.UserId);

        var accessToken = await _github.ExchangeCodeForTokenAsync(cmd.OAuthCode, ct);

        // Fetch GitHub profile to verify token and get login
        var ghUser = await _github.GetUserAsync(accessToken, ct);

        user.ConnectGitHub(ghUser.Login, accessToken);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} connected GitHub account @{GitHubLogin}", cmd.UserId, ghUser.Login);

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            GitHubLogin = user.GitHubLogin,
            IsEmailVerified = user.IsEmailVerified,
            AnalysisCount = user.AnalysisCount,
            CreatedAt = user.CreatedAt,
            HasGitHubConnected = user.HasGitHubConnected
        };
    }
}

// Analyze Pull Request
public sealed record AnalyzePullRequestCommand : IRequest<AnalysisSummaryDto>
{
    public required Guid UserId { get; init; }
    public required string Owner { get; init; }
    public required string Repository { get; init; }
    public required int PullRequestNumber { get; init; }
}

public sealed class AnalyzePullRequestCommandValidator : AbstractValidator<AnalyzePullRequestCommand>
{
    public AnalyzePullRequestCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Owner).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Repository).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PullRequestNumber).GreaterThan(0);
    }
}

public sealed class AnalyzePullRequestCommandHandler : IRequestHandler<AnalyzePullRequestCommand, AnalysisSummaryDto>
{
    private readonly IUserRepository _userRepo;
    private readonly IGitHubService _github;
    private readonly IMediator _mediator;
    private readonly ILogger<AnalyzePullRequestCommandHandler> _logger;

    public AnalyzePullRequestCommandHandler(
        IUserRepository userRepo,
        IGitHubService github,
        IMediator mediator,
        ILogger<AnalyzePullRequestCommandHandler> logger)
    {
        _userRepo = userRepo;
        _github = github;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<AnalysisSummaryDto> Handle(AnalyzePullRequestCommand cmd, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(cmd.UserId, ct)
            ?? throw new NotFoundException("User", cmd.UserId);

        if (!user.HasGitHubConnected)
            throw new ForbiddenException("GitHub account is not connected. Please connect your GitHub account first.");

        var repoFullName = $"{cmd.Owner}/{cmd.Repository}";

        // Fetch changed files from PR
        var files = await _github.GetPullRequestFilesAsync(user.GitHubAccessToken!, cmd.Owner, cmd.Repository, cmd.PullRequestNumber, ct);

        // Aggregate all changed code from the PR diff
        var codeBuilder = new System.Text.StringBuilder();
        foreach (var file in files.Where(f => f.Patch is not null))
        {
            codeBuilder.AppendLine($"// File: {file.FileName}");
            codeBuilder.AppendLine(file.Patch);
            codeBuilder.AppendLine();
        }

        var combinedCode = codeBuilder.ToString();
        if (string.IsNullOrWhiteSpace(combinedCode))
            throw new ConflictException("No code changes found in this pull request.");

        return await _mediator.Send(new CreateAnalysisCommand
        {
            UserId = cmd.UserId,
            Title = $"PR #{cmd.PullRequestNumber} — {repoFullName}",
            Code = combinedCode,
            SourceType = Domain.Enums.CodeSourceType.GitHubPullRequest,
            RepositoryFullName = repoFullName,
            PullRequestNumber = cmd.PullRequestNumber
        }, ct);
    }
}
