using CodeReview.Domain.Common;
using CodeReview.Domain.Enums;

namespace CodeReview.Domain.Entities;

/// <summary>
/// Child entity of CodeAnalysis — represents a single detected code issue
/// </summary>
public sealed class AnalysisIssue : BaseEntity
{
    private AnalysisIssue() { }

    public Guid AnalysisId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string? Suggestion { get; private set; }
    public IssueSeverity Severity { get; private set; }
    public IssueCategory Category { get; private set; }
    public int? LineStart { get; private set; }
    public int? LineEnd { get; private set; }
    public string? CodeSnippet { get; private set; }
    public bool IsAiGenerated { get; private set; }
    public double? Confidence { get; private set; }

    public static AnalysisIssue Create(
        Guid analysisId,
        string title,
        string description,
        IssueSeverity severity,
        IssueCategory category,
        int? lineStart = null,
        int? lineEnd = null,
        string? codeSnippet = null,
        string? suggestion = null,
        bool isAiGenerated = false,
        double? confidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new AnalysisIssue
        {
            AnalysisId = analysisId,
            Title = title.Trim(),
            Description = description.Trim(),
            Suggestion = suggestion?.Trim(),
            Severity = severity,
            Category = category,
            LineStart = lineStart,
            LineEnd = lineEnd,
            CodeSnippet = codeSnippet,
            IsAiGenerated = isAiGenerated,
            Confidence = confidence
        };
    }
}

/// <summary>
/// Immutable snapshot for version comparison history
/// </summary>
public sealed class AnalysisSnapshot : BaseEntity
{
    private AnalysisSnapshot() { }

    public Guid AnalysisId { get; private set; }
    public double OverallScore { get; private set; }
    public double ArchitectureScore { get; private set; }
    public double ReadabilityScore { get; private set; }
    public double MaintainabilityScore { get; private set; }
    public int TotalIssues { get; private set; }
    public int Version { get; private set; }

    public static AnalysisSnapshot Create(Guid analysisId, ValueObjects.QualityScore score, int totalIssues) =>
        new()
        {
            AnalysisId = analysisId,
            OverallScore = score.Overall,
            ArchitectureScore = score.Architecture,
            ReadabilityScore = score.Readability,
            MaintainabilityScore = score.Maintainability,
            TotalIssues = totalIssues,
            Version = 1
        };
}
