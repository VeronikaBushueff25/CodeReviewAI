namespace CodeReview.Application.Abstractions.Services;

public interface IGitHubService
{
    Task<GitHubUser> GetUserAsync(string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubRepository>> GetRepositoriesAsync(string accessToken, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubPullRequest>> GetPullRequestsAsync(string accessToken, string owner, string repo, CancellationToken ct = default);
    Task<string> GetFileContentAsync(string accessToken, string owner, string repo, string path, string? branch = null, CancellationToken ct = default);
    Task<IReadOnlyList<GitHubFile>> GetPullRequestFilesAsync(string accessToken, string owner, string repo, int prNumber, CancellationToken ct = default);
    Task<string> ExchangeCodeForTokenAsync(string code, CancellationToken ct = default);
}

public sealed record GitHubUser(string Login, string? Name, string? Email, string? AvatarUrl);
public sealed record GitHubRepository(long Id, string Name, string FullName, string? Description, bool IsPrivate, string DefaultBranch, string CloneUrl);
public sealed record GitHubPullRequest(int Number, string Title, string State, string HeadBranch, string BaseBranch, int ChangedFiles, int Additions, int Deletions);
public sealed record GitHubFile(string FileName, string Status, int Additions, int Deletions, string? Patch, string? RawUrl);
