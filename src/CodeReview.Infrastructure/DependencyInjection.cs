using CodeReview.Application.Abstractions.AI;
using CodeReview.Application.Abstractions.Services;
using CodeReview.Domain.Interfaces;
using CodeReview.Infrastructure.AI.Providers;
using CodeReview.Infrastructure.GitHub;
using CodeReview.Infrastructure.Persistence;
using CodeReview.Infrastructure.Persistence.Repositories;
using CodeReview.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;

namespace CodeReview.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                config.GetConnectionString("PostgreSQL"),
                npgsql =>
                {
                    npgsql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                    npgsql.CommandTimeout(30);
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                })
            .EnableSensitiveDataLogging(config.GetValue<bool>("EnableSensitiveDataLogging")));

        services.AddScoped<IAnalysisRepository, AnalysisRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();

        services.AddStackExchangeRedisCache(opts =>
            opts.Configuration = config.GetConnectionString("Redis"));
        services.AddScoped<ICacheService, CacheService>();

        services.AddHttpClient("AI", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
            client.DefaultRequestHeaders.Add("User-Agent", "CodeReviewAI/1.0");
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddScoped<IAiCodeAnalyzer, HuggingFaceProvider>();

        services.AddScoped<IStaticCodeAnalyzer, StaticCodeAnalyzer>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IGitHubService, GitHubService>();
        services.AddScoped<IAnalysisNotificationService, AnalysisNotificationService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (outcome, duration, attempt, _) =>
                    Console.WriteLine($"[Polly] Retry {attempt} after {duration}s due to: {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}"));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30));
}
