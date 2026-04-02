using CodeReview.Domain.Common;
using CodeReview.Domain.Events;
using CodeReview.Domain.Exceptions;

namespace CodeReview.Domain.Entities;

public sealed class User : AuditableEntity
{
    private User() { }

    public string Email { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string? GitHubLogin { get; private set; }
    public string? GitHubAccessToken { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsEmailVerified { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public int AnalysisCount { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static User Create(string email, string username, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var user = new User
        {
            Email = email.ToLowerInvariant().Trim(),
            Username = username.Trim(),
            PasswordHash = passwordHash
        };

        user.RaiseDomainEvent(new UserRegisteredEvent(user.Id, email));
        return user;
    }

    public void ConnectGitHub(string login, string accessToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

        GitHubLogin = login;
        GitHubAccessToken = accessToken;
        SetUpdatedAt();

        RaiseDomainEvent(new GitHubConnectedEvent(Id, login));
    }

    public void DisconnectGitHub()
    {
        GitHubLogin = null;
        GitHubAccessToken = null;
        SetUpdatedAt();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
        SetUpdatedAt();
    }

    public void IncrementAnalysisCount()
    {
        AnalysisCount++;
        SetUpdatedAt();
    }

    public void VerifyEmail()
    {
        if (IsEmailVerified)
            throw new DomainException("Email already verified");

        IsEmailVerified = true;
        SetUpdatedAt();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAt();
    }

    public bool HasGitHubConnected => !string.IsNullOrWhiteSpace(GitHubLogin);
}
