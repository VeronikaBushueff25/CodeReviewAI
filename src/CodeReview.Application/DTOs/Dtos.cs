using CodeReview.Domain.Enums;

namespace CodeReview.Application.DTOs;

public sealed record AnalysisSummaryDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public AnalysisStatus Status { get; init; }
    public CodeLanguage Language { get; init; }
    public CodeSourceType SourceType { get; init; }
    public QualityScoreDto? Score { get; init; }
    public int IssueCount { get; init; }
    public int CriticalIssueCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public TimeSpan? Duration { get; init; }
}

public sealed record AnalysisDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public AnalysisStatus Status { get; init; }
    public CodeLanguage Language { get; init; }
    public CodeSourceDto Source { get; init; } = null!;
    public QualityScoreDto? Score { get; init; }
    public StaticMetricsDto? Metrics { get; init; }
    public IReadOnlyList<AnalysisIssueDto> Issues { get; init; } = [];
    public string? FailureReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration { get; init; }
}

public sealed record QualityScoreDto
{
    public double Overall { get; init; }
    public double Architecture { get; init; }
    public double Readability { get; init; }
    public double Maintainability { get; init; }
    public string Grade { get; init; } = string.Empty;
}

public sealed record StaticMetricsDto
{
    public int TotalLines { get; init; }
    public int CodeLines { get; init; }
    public int CommentLines { get; init; }
    public double AverageCyclomaticComplexity { get; init; }
    public int MaxCyclomaticComplexity { get; init; }
    public int LongMethodCount { get; init; }
    public int DuplicateBlockCount { get; init; }
    public double DuplicationPercentage { get; init; }
    public int TotalMethods { get; init; }
    public int TotalClasses { get; init; }
    public double AverageMethodLength { get; init; }
    public double CommentRatio { get; init; }
}

public sealed record AnalysisIssueDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Suggestion { get; init; }
    public IssueSeverity Severity { get; init; }
    public IssueCategory Category { get; init; }
    public int? LineStart { get; init; }
    public int? LineEnd { get; init; }
    public string? CodeSnippet { get; init; }
    public bool IsAiGenerated { get; init; }
    public double? Confidence { get; init; }
}

public sealed record CodeSourceDto
{
    public CodeSourceType Type { get; init; }
    public string? FileName { get; init; }
    public string? RepositoryFullName { get; init; }
    public int? PullRequestNumber { get; init; }
    public long ContentSizeBytes { get; init; }
}

public sealed record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string? GitHubLogin { get; init; }
    public bool IsEmailVerified { get; init; }
    public int AnalysisCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; init; }
    public bool HasGitHubConnected { get; init; }
}

public sealed record AuthTokenDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public UserDto User { get; init; } = null!;
}

public sealed record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
