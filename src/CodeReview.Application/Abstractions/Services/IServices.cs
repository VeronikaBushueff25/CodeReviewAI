using CodeReview.Domain.Enums;
using CodeReview.Domain.ValueObjects;

namespace CodeReview.Application.Abstractions.Services;

/// <summary>
/// Static code analysis without AI (cyclomatic complexity, naming, duplication)
/// </summary>
public interface IStaticCodeAnalyzer
{
    Task<StaticMetrics> AnalyzeAsync(string code, CodeLanguage language, CancellationToken ct = default);
    CodeLanguage DetectLanguage(string code, string? fileName = null);
}

/// <summary>
/// Token/Auth management 
/// </summary>
public interface IJwtService
{
    string GenerateAccessToken(Guid userId, string email, string username);
    string GenerateRefreshToken();
    Guid? ValidateToken(string token);
    DateTime GetTokenExpiry();
}

/// <summary>
/// Password hashing abstraction
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>
/// Caching abstraction over Redis/Memory
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPatternAsync(string pattern, CancellationToken ct = default);
}

/// <summary>
/// Real-time notifications via SignalR
/// </summary>
public interface IAnalysisNotificationService
{
    Task NotifyAnalysisStartedAsync(Guid userId, Guid analysisId, CancellationToken ct = default);
    Task NotifyAnalysisProgressAsync(Guid userId, Guid analysisId, int progressPercent, string stage, CancellationToken ct = default);
    Task NotifyAnalysisCompletedAsync(Guid userId, Guid analysisId, CancellationToken ct = default);
    Task NotifyAnalysisFailedAsync(Guid userId, Guid analysisId, string reason, CancellationToken ct = default);
}
