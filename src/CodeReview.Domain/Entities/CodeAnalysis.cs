using CodeReview.Domain.Common;
using CodeReview.Domain.Enums;
using CodeReview.Domain.Events;
using CodeReview.Domain.Exceptions;
using CodeReview.Domain.ValueObjects;

namespace CodeReview.Domain.Entities;

/// <summary>
/// Aggregate Root — represents a single code analysis session
/// Encapsulates all business rules for code analysis lifecycle
/// </summary>
public sealed class CodeAnalysis : AuditableEntity
{
    private readonly List<AnalysisIssue> _issues = [];
    private readonly List<AnalysisSnapshot> _snapshots = [];

    // Private constructor enforces factory method usage
    private CodeAnalysis() { }

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public CodeSource Source { get; private set; } = null!;
    public AnalysisStatus Status { get; private set; }
    public CodeLanguage Language { get; private set; }
    public QualityScore? Score { get; private set; }
    public StaticMetrics? Metrics { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue
        ? CompletedAt.Value - StartedAt.Value
        : null;

    public IReadOnlyCollection<AnalysisIssue> Issues => _issues.AsReadOnly();
    public IReadOnlyCollection<AnalysisSnapshot> Snapshots => _snapshots.AsReadOnly();

    /// <summary>Factory method — ensures valid aggregate state from creation</summary>
    public static CodeAnalysis Create(
        Guid userId,
        string title,
        CodeSource source,
        CodeLanguage language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(source);

        var analysis = new CodeAnalysis
        {
            UserId = userId,
            Title = title.Trim(),
            Source = source,
            Language = language,
            Status = AnalysisStatus.Pending
        };

        analysis.RaiseDomainEvent(new AnalysisCreatedEvent(analysis.Id, userId));
        return analysis;
    }

    public void Start()
    {
        if (Status != AnalysisStatus.Pending)
            throw new DomainException($"Cannot start analysis in status {Status}");

        Status = AnalysisStatus.InProgress;
        StartedAt = DateTime.UtcNow;
        SetUpdatedAt();

        RaiseDomainEvent(new AnalysisStartedEvent(Id, UserId));
    }

    public void Complete(QualityScore score, StaticMetrics metrics, IEnumerable<AnalysisIssue> issues)
    {
        if (Status != AnalysisStatus.InProgress)
            throw new DomainException($"Cannot complete analysis in status {Status}");

        Score = score;
        Metrics = metrics;
        Status = AnalysisStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        _issues.Clear();
        _issues.AddRange(issues);

        // Create snapshot for history comparison
        _snapshots.Add(AnalysisSnapshot.Create(Id, score, _issues.Count));

        SetUpdatedAt();
        RaiseDomainEvent(new AnalysisCompletedEvent(Id, UserId, score.Overall));
    }

    public void Fail(string reason)
    {
        if (Status == AnalysisStatus.Completed)
            throw new DomainException("Cannot fail a completed analysis");

        Status = AnalysisStatus.Failed;
        FailureReason = reason;
        CompletedAt = DateTime.UtcNow;
        SetUpdatedAt();

        RaiseDomainEvent(new AnalysisFailedEvent(Id, UserId, reason));
    }

    public void Cancel()
    {
        if (Status is AnalysisStatus.Completed or AnalysisStatus.Failed)
            throw new DomainException($"Cannot cancel analysis in status {Status}");

        Status = AnalysisStatus.Cancelled;
        SetUpdatedAt();
    }

    public bool BelongsTo(Guid userId) => UserId == userId;
}
