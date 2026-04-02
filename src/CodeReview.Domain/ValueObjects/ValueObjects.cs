using CodeReview.Domain.Enums;
using CodeReview.Domain.Exceptions;

namespace CodeReview.Domain.ValueObjects;

/// <summary>
/// Immutable value object representing the quality score breakdown
/// Enforces business invariants on score ranges
/// </summary>
public sealed record QualityScore
{
    public double Overall { get; }
    public double Architecture { get; }
    public double Readability { get; }
    public double Maintainability { get; }
    public string Grade => Overall switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        _ => "F"
    };

    private QualityScore(double architecture, double readability, double maintainability)
    {
        Architecture = ValidateScore(architecture, nameof(Architecture));
        Readability = ValidateScore(readability, nameof(Readability));
        Maintainability = ValidateScore(maintainability, nameof(Maintainability));
        Overall = Math.Round((Architecture * 0.35 + Readability * 0.30 + Maintainability * 0.35), 2);
    }

    public static QualityScore Create(double architecture, double readability, double maintainability) =>
        new(architecture, readability, maintainability);

    private static double ValidateScore(double value, string name)
    {
        if (value < 0 || value > 100)
            throw new DomainException($"{name} score must be between 0 and 100, got {value}");
        return Math.Round(value, 2);
    }
}

/// <summary>
/// Value object — static code metrics from analyzer
/// </summary>
public sealed record StaticMetrics
{
    public int TotalLines { get; init; }
    public int CodeLines { get; init; }
    public int CommentLines { get; init; }
    public int BlankLines { get; init; }
    public double AverageCyclomaticComplexity { get; init; }
    public int MaxCyclomaticComplexity { get; init; }
    public int LongMethodCount { get; init; }
    public int DuplicateBlockCount { get; init; }
    public double DuplicationPercentage { get; init; }
    public int TotalMethods { get; init; }
    public int TotalClasses { get; init; }
    public double AverageMethodLength { get; init; }
    public double CommentRatio => TotalLines > 0
        ? Math.Round((double)CommentLines / TotalLines * 100, 2)
        : 0;
}

/// <summary>
/// Value object — code source information (where the code came from)
/// </summary>
public sealed record CodeSource
{
    public CodeSourceType Type { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public string? RepositoryFullName { get; init; }
    public int? PullRequestNumber { get; init; }
    public string? CommitSha { get; init; }
    public long ContentSizeBytes { get; init; }

    public static CodeSource FromManualInput(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new CodeSource
        {
            Type = CodeSourceType.ManualInput,
            Content = code,
            ContentSizeBytes = System.Text.Encoding.UTF8.GetByteCount(code)
        };
    }

    public static CodeSource FromFile(string code, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return new CodeSource
        {
            Type = CodeSourceType.FileUpload,
            Content = code,
            FileName = fileName,
            ContentSizeBytes = System.Text.Encoding.UTF8.GetByteCount(code)
        };
    }

    public static CodeSource FromGitHub(string code, string repoFullName, string? commitSha = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoFullName);
        return new CodeSource
        {
            Type = CodeSourceType.GitHubRepository,
            Content = code,
            RepositoryFullName = repoFullName,
            CommitSha = commitSha,
            ContentSizeBytes = System.Text.Encoding.UTF8.GetByteCount(code)
        };
    }

    public static CodeSource FromPullRequest(string code, string repoFullName, int prNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(repoFullName);
        return new CodeSource
        {
            Type = CodeSourceType.GitHubPullRequest,
            Content = code,
            RepositoryFullName = repoFullName,
            PullRequestNumber = prNumber,
            ContentSizeBytes = System.Text.Encoding.UTF8.GetByteCount(code)
        };
    }
}
