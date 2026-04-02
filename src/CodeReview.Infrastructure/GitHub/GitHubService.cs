using CodeReview.Application.Abstractions.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Octokit;

namespace CodeReview.Infrastructure.GitHub;

public sealed class GitHubService : IGitHubService
{
    private readonly IConfiguration _config;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(IConfiguration config, ILogger<GitHubService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<string> ExchangeCodeForTokenAsync(string code, CancellationToken ct = default)
    {
        var clientId = _config["GitHub:ClientId"]
            ?? throw new InvalidOperationException("GitHub:ClientId not configured");
        var clientSecret = _config["GitHub:ClientSecret"]
            ?? throw new InvalidOperationException("GitHub:ClientSecret not configured");

        var github = new GitHubClient(new ProductHeaderValue("CodeReviewAI"));
        var token = await github.Oauth.CreateAccessToken(new OauthTokenRequest(clientId, clientSecret, code));

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException("GitHub OAuth token exchange failed: empty token received");

        return token.AccessToken;
    }

    public async Task<Application.Abstractions.Services.GitHubUser> GetUserAsync(string accessToken, CancellationToken ct = default)
    {
        var client = CreateClient(accessToken);
        var user = await client.User.Current();

        return new Application.Abstractions.Services.GitHubUser(
            user.Login,
            user.Name,
            user.Email,
            user.AvatarUrl);
    }

    public async Task<IReadOnlyList<Application.Abstractions.Services.GitHubRepository>> GetRepositoriesAsync(
        string accessToken, CancellationToken ct = default)
    {
        var client = CreateClient(accessToken);

        var repos = await client.Repository.GetAllForCurrent(
            new ApiOptions { PageSize = 100 });

        return repos.Select(r => new Application.Abstractions.Services.GitHubRepository(
            r.Id,
            r.Name,
            r.FullName,
            r.Description,
            r.Private,
            r.DefaultBranch,
            r.CloneUrl)).ToList();
    }

    public async Task<IReadOnlyList<Application.Abstractions.Services.GitHubPullRequest>> GetPullRequestsAsync(
        string accessToken, string owner, string repo, CancellationToken ct = default)
    {
        var client = CreateClient(accessToken);

        var prs = await client.PullRequest.GetAllForRepository(
            owner, repo,
            new PullRequestRequest { State = ItemStateFilter.Open },
            new ApiOptions { PageSize = 50 });

        return prs.Select(pr => new Application.Abstractions.Services.GitHubPullRequest(
            pr.Number,
            pr.Title,
            pr.State.StringValue,
            pr.Head.Ref,
            pr.Base.Ref,
            pr.ChangedFiles,
            pr.Additions,
            pr.Deletions)).ToList();
    }

    public async Task<string> GetFileContentAsync(
        string accessToken, string owner, string repo, string path,
        string? branch = null, CancellationToken ct = default)
    {
        var client = CreateClient(accessToken);

        var contents = branch is not null
            ? await client.Repository.Content.GetAllContentsByRef(owner, repo, path, branch)
            : await client.Repository.Content.GetAllContents(owner, repo, path);

        var file = contents.FirstOrDefault()
            ?? throw new InvalidOperationException($"File '{path}' not found in {owner}/{repo}");

        return file.EncodedContent is not null
            ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(
                file.EncodedContent.Replace("\n", "")))
            : file.Content ?? string.Empty;
    }

    public async Task<IReadOnlyList<Application.Abstractions.Services.GitHubFile>> GetPullRequestFilesAsync(
        string accessToken, string owner, string repo, int prNumber, CancellationToken ct = default)
    {
        var client = CreateClient(accessToken);

        var files = await client.PullRequest.Files(owner, repo, prNumber);

        return files.Select(f => new Application.Abstractions.Services.GitHubFile(
            f.FileName,
            f.Status,
            f.Additions,
            f.Deletions,
            f.Patch,
            f.RawUrl)).ToList();
    }

    private static GitHubClient CreateClient(string accessToken)
    {
        var client = new GitHubClient(new ProductHeaderValue("CodeReviewAI"));
        client.Credentials = new Credentials(accessToken);
        return client;
    }
}
