using CodeReview.Application.Abstractions.Services;
using CodeReview.Application.DTOs;
using CodeReview.Application.Features.GitHub.Commands;
using CodeReview.Application.Features.GitHub.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeReview.API.Controllers;

/// <summary>
/// GitHub OAuth integration and repository/PR analysis endpoints
/// </summary>
[Authorize]
public sealed class GitHubController : ApiControllerBase
{
    /// <summary>Complete GitHub OAuth flow — exchange code for access token and link account</summary>
    [HttpPost("connect")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Connect(
        [FromBody] ConnectGitHubRequest request,
        CancellationToken ct)
    {
        var result = await Sender.Send(new ConnectGitHubCommand
        {
            UserId = CurrentUserId,
            OAuthCode = request.Code
        }, ct);

        return Ok(result);
    }

    /// <summary>Get all repositories for the connected GitHub account</summary>
    [HttpGet("repositories")]
    [ProducesResponseType(typeof(IReadOnlyList<GitHubRepository>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRepositories(CancellationToken ct)
    {
        var result = await Sender.Send(new GetGitHubRepositoriesQuery(CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Get open pull requests for a specific repository</summary>
    [HttpGet("repositories/{owner}/{repo}/pull-requests")]
    [ProducesResponseType(typeof(IReadOnlyList<GitHubPullRequest>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPullRequests(
        string owner,
        string repo,
        CancellationToken ct)
    {
        var result = await Sender.Send(new GetGitHubPullRequestsQuery(CurrentUserId, owner, repo), ct);
        return Ok(result);
    }

    /// <summary>Analyze a pull request — fetches diff and runs full code review</summary>
    [HttpPost("repositories/{owner}/{repo}/pull-requests/{prNumber:int}/analyze")]
    [ProducesResponseType(typeof(AnalysisSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AnalyzePullRequest(
        string owner,
        string repo,
        int prNumber,
        CancellationToken ct)
    {
        var result = await Sender.Send(new AnalyzePullRequestCommand
        {
            UserId = CurrentUserId,
            Owner = owner,
            Repository = repo,
            PullRequestNumber = prNumber
        }, ct);

        return CreatedAtAction(
            nameof(AnalysesController.GetById),
            "Analyses",
            new { id = result.Id },
            result);
    }

    /// <summary>Disconnect GitHub account</summary>
    [HttpDelete("disconnect")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        await Sender.Send(new DisconnectGitHubCommand(CurrentUserId), ct);
        return NoContent();
    }
}

public sealed record ConnectGitHubRequest(string Code);
