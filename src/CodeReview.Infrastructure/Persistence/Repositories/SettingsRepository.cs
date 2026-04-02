using CodeReview.Domain.Interfaces;
using Microsoft.Extensions.Configuration;

namespace CodeReview.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads settings from IConfiguration (appsettings.json / env vars / secrets).
/// Allows the AI provider to retrieve API keys without direct IConfiguration dependency.
/// </summary>
public sealed class SettingsRepository : ISettingsRepository
{
    private readonly IConfiguration _configuration;

    public SettingsRepository(IConfiguration configuration) =>
        _configuration = configuration;

    public Task<string?> GetValueAsync(string key, CancellationToken ct = default)
    {
        // Supports both "AI:ApiKey" dot-notation and environment variable style
        var value = _configuration[key];
        return Task.FromResult(value);
    }

    public Task SetValueAsync(string key, string value, CancellationToken ct = default)
    {
        // Configuration is read-only at runtime; this would require a DB-backed settings store
        // for dynamic updates. For now, throw to signal unsupported operation.
        throw new NotSupportedException(
            "Runtime configuration updates are not supported. Use environment variables or appsettings.json.");
    }
}
