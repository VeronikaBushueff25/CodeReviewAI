using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CodeReview.API.Controllers;

/// <summary>
/// Base controller providing MediatR dispatch and authenticated user context
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;

    protected ISender Sender =>
        _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    protected Guid CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new UnauthorizedAccessException("User identity not found in token.");

            return Guid.TryParse(sub, out var id)
                ? id
                : throw new UnauthorizedAccessException("Invalid user ID in token.");
        }
    }
}
