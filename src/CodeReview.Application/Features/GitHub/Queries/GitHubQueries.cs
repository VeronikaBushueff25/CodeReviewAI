using CodeReview.Application.Abstractions.Services;
using CodeReview.Application.Common.Exceptions;
using CodeReview.Domain.Interfaces;
using MediatR;

namespace CodeReview.Application.Features.GitHub.Queries;

// ─── Get Repositories ────────────────────────────────────────────────────────
public sealed record GetGitHubRepositoriesQuery(Guid UserId) : IRequest<IReadOnlyList<GitHubRepository>>;

public sealed class GetGitHubRepositoriesQueryHandler : IRequestHandler<GetGitHubRepositoriesQuery, IReadOnlyList<GitHubRepository>>
{
    private readonly IUserRepository _userRepo;
    private readonly IGitHubService _github;

    public GetGitHubRepositoriesQueryHandler(IUserRepository userRepo, IGitHubService github)
    {
        _userRepo = userRepo;
        _github = github;
    }

    public async Task<IReadOnlyList<GitHubRepository>> Handle(GetGitHubRepositoriesQuery query, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(query.UserId, ct)
            ?? throw new NotFoundException("User", query.UserId);

        if (!user.HasGitHubConnected)
            throw new ForbiddenException("GitHub account is not connected.");

        return await _github.GetRepositoriesAsync(user.GitHubAccessToken!, ct);
    }
}

// ─── Get Pull Requests ────────────────────────────────────────────────────────
public sealed record GetGitHubPullRequestsQuery(Guid UserId, string Owner, string Repository)
    : IRequest<IReadOnlyList<GitHubPullRequest>>;

public sealed class GetGitHubPullRequestsQueryHandler : IRequestHandler<GetGitHubPullRequestsQuery, IReadOnlyList<GitHubPullRequest>>
{
    private readonly IUserRepository _userRepo;
    private readonly IGitHubService _github;

    public GetGitHubPullRequestsQueryHandler(IUserRepository userRepo, IGitHubService github)
    {
        _userRepo = userRepo;
        _github = github;
    }

    public async Task<IReadOnlyList<GitHubPullRequest>> Handle(GetGitHubPullRequestsQuery query, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(query.UserId, ct)
            ?? throw new NotFoundException("User", query.UserId);

        if (!user.HasGitHubConnected)
            throw new ForbiddenException("GitHub account is not connected.");

        return await _github.GetPullRequestsAsync(user.GitHubAccessToken!, query.Owner, query.Repository, ct);
    }
}
