using CodeReview.Application.Abstractions.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CodeReview.Infrastructure.Services;

/// <summary>
/// Real-time analysis progress notifications via SignalR
/// Clients subscribe to their userId group to receive personal updates
/// </summary>
public sealed class AnalysisNotificationService : IAnalysisNotificationService
{
    private readonly IHubContext<AnalysisHub> _hubContext;
    private readonly ILogger<AnalysisNotificationService> _logger;

    public AnalysisNotificationService(
        IHubContext<AnalysisHub> hubContext,
        ILogger<AnalysisNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyAnalysisStartedAsync(Guid userId, Guid analysisId, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(userId.ToString())
            .SendAsync("AnalysisStarted", new { AnalysisId = analysisId }, ct);
        _logger.LogDebug("Notified user {UserId} analysis {AnalysisId} started", userId, analysisId);
    }

    public async Task NotifyAnalysisProgressAsync(Guid userId, Guid analysisId, int progressPercent, string stage, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(userId.ToString())
            .SendAsync("AnalysisProgress", new { AnalysisId = analysisId, Progress = progressPercent, Stage = stage }, ct);
    }

    public async Task NotifyAnalysisCompletedAsync(Guid userId, Guid analysisId, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(userId.ToString())
            .SendAsync("AnalysisCompleted", new { AnalysisId = analysisId }, ct);
    }

    public async Task NotifyAnalysisFailedAsync(Guid userId, Guid analysisId, string reason, CancellationToken ct = default)
    {
        await _hubContext.Clients.Group(userId.ToString())
            .SendAsync("AnalysisFailed", new { AnalysisId = analysisId, Reason = reason }, ct);
    }
}

/// <summary>
/// SignalR Hub — clients connect and join their userId group for personal notifications
/// </summary>
public sealed class AnalysisHub : Hub
{
    private readonly ILogger<AnalysisHub> _logger;

    public AnalysisHub(ILogger<AnalysisHub> logger) => _logger = logger;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                  ?? Context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            _logger.LogInformation("Client {ConnectionId} joined group {UserId}", Context.ConnectionId, userId);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client {ConnectionId} disconnected", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
