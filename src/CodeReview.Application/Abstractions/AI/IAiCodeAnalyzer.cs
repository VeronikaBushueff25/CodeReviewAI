using CodeReview.Domain.Entities;
using CodeReview.Domain.Enums;
using CodeReview.Domain.ValueObjects;

namespace CodeReview.Application.Abstractions.AI;

/// <summary>
/// Core AI analysis contract — abstracted from specific provider
/// Supports HuggingFace, OpenAI, Anthropic via strategy pattern
/// </summary>
public interface IAiCodeAnalyzer
{
    Task<AiAnalysisResult> AnalyzeAsync(
        string code,
        CodeLanguage language,
        StaticMetrics metrics,
        CancellationToken ct = default);

    string ProviderName { get; }
    bool IsAvailable { get; }
}

public sealed record AiAnalysisResult
{
    public required IReadOnlyList<AiIssue> Issues { get; init; }
    public required QualityScore Score { get; init; }
    public required string Summary { get; init; }
    public required string ProviderName { get; init; }
    public required TimeSpan ProcessingTime { get; init; }
    public int TokensUsed { get; init; }
}

public sealed record AiIssue
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required IssueSeverity Severity { get; init; }
    public required IssueCategory Category { get; init; }
    public string? Suggestion { get; init; }
    public int? LineStart { get; init; }
    public int? LineEnd { get; init; }
    public string? CodeSnippet { get; init; }
    public double Confidence { get; init; } = 1.0;
}
