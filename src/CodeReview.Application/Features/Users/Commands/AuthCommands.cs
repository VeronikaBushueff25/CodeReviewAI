using CodeReview.Application.Abstractions.Services;
using CodeReview.Application.Common.Exceptions;
using CodeReview.Application.DTOs;
using CodeReview.Domain.Entities;
using CodeReview.Domain.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CodeReview.Application.Features.Users.Commands;

// ─── Register ────────────────────────────────────────────────────────────────
public sealed record RegisterCommand : IRequest<AuthTokenDto>
{
    public required string Email { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
}

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress().WithMessage("A valid email is required.");

        RuleFor(x => x.Username)
            .NotEmpty()
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(50)
            .Matches(@"^[a-zA-Z0-9_-]+$").WithMessage("Username may only contain letters, numbers, underscores, and hyphens.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.");
    }
}

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthTokenDto>
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;

    public RegisterCommandHandler(
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtService jwtService)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
    }

    public async Task<AuthTokenDto> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        if (await _userRepo.ExistsByEmailAsync(cmd.Email, ct))
            throw new ConflictException($"Email '{cmd.Email}' is already registered.");

        if (await _userRepo.ExistsByUsernameAsync(cmd.Username, ct))
            throw new ConflictException($"Username '{cmd.Username}' is already taken.");

        var passwordHash = _passwordHasher.Hash(cmd.Password);
        var user = User.Create(cmd.Email, cmd.Username, passwordHash);

        await _userRepo.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var accessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, user.Username);
        var refreshToken = _jwtService.GenerateRefreshToken();

        return new AuthTokenDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = _jwtService.GetTokenExpiry(),
            User = MapUser(user)
        };
    }

    private static UserDto MapUser(User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        Username = u.Username,
        IsEmailVerified = u.IsEmailVerified,
        AnalysisCount = u.AnalysisCount,
        CreatedAt = u.CreatedAt
    };
}

// ─── Login ────────────────────────────────────────────────────────────────────
public sealed record LoginCommand : IRequest<AuthTokenDto>
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthTokenDto>
{
    private readonly IUserRepository _userRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtService _jwtService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepo,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtService jwtService,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepo = userRepo;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<AuthTokenDto> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var user = await _userRepo.GetByEmailAsync(cmd.Email, ct);

        // Constant-time comparison to prevent timing attacks
        if (user is null || !_passwordHasher.Verify(cmd.Password, user.PasswordHash))
            throw new ForbiddenException("Invalid email or password.");

        if (!user.IsActive)
            throw new ForbiddenException("Account has been deactivated.");

        user.RecordLogin();
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} logged in", user.Id);

        return new AuthTokenDto
        {
            AccessToken = _jwtService.GenerateAccessToken(user.Id, user.Email, user.Username),
            RefreshToken = _jwtService.GenerateRefreshToken(),
            ExpiresAt = _jwtService.GetTokenExpiry(),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Username = user.Username,
                GitHubLogin = user.GitHubLogin,
                IsEmailVerified = user.IsEmailVerified,
                AnalysisCount = user.AnalysisCount,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt,
                HasGitHubConnected = user.HasGitHubConnected
            }
        };
    }
}
