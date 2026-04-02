using CodeReview.Application.DTOs;
using CodeReview.Application.Features.Users.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeReview.API.Controllers;

/// <summary>
/// Handles user registration and authentication (JWT-based).
/// </summary>
public sealed class AuthController : ApiControllerBase
{
    /// <summary>Register a new user account</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCommand command,
        CancellationToken ct)
    {
        var result = await Sender.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Login with email and password to receive JWT tokens</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthTokenDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(
        [FromBody] LoginCommand command,
        CancellationToken ct)
    {
        var result = await Sender.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Get current authenticated user profile</summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await Sender.Send(new GetCurrentUserQuery(CurrentUserId), ct);
        return Ok(result);
    }
}
