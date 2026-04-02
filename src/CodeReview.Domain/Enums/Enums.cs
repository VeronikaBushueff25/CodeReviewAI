namespace CodeReview.Domain.Enums;

public enum AnalysisStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4
}

public enum CodeLanguage
{
    Unknown = 0,
    CSharp = 1,
    JavaScript = 2,
    TypeScript = 3,
    Python = 4,
    Java = 5,
    Go = 6,
    Rust = 7,
    Cpp = 8,
    PHP = 9,
    Ruby = 10,
    Swift = 11,
    Kotlin = 12
}

public enum IssueCategory
{
    SolidViolation = 0,
    AntiPattern = 1,
    Readability = 2,
    Complexity = 3,
    Naming = 4,
    Performance = 5,
    Security = 6,
    Duplication = 7,
    Architecture = 8,
    Maintainability = 9
}

public enum IssueSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2,
    Critical = 3
}

public enum CodeSourceType
{
    ManualInput = 0,
    FileUpload = 1,
    GitHubRepository = 2,
    GitHubPullRequest = 3
}
