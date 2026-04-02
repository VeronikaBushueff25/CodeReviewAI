using CodeReview.Domain.Common;

namespace CodeReview.Domain.Events;

public sealed record AnalysisCreatedEvent(Guid AnalysisId, Guid UserId) : DomainEvent;
public sealed record AnalysisStartedEvent(Guid AnalysisId, Guid UserId) : DomainEvent;
public sealed record AnalysisCompletedEvent(Guid AnalysisId, Guid UserId, double OverallScore) : DomainEvent;
public sealed record AnalysisFailedEvent(Guid AnalysisId, Guid UserId, string Reason) : DomainEvent;
public sealed record UserRegisteredEvent(Guid UserId, string Email) : DomainEvent;
public sealed record GitHubConnectedEvent(Guid UserId, string GitHubLogin) : DomainEvent;
