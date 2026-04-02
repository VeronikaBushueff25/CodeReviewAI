using CodeReview.Application.DTOs;
using CodeReview.Application.Features.Analyses.Commands;
using CodeReview.Application.Features.Analyses.Queries;
using CodeReview.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CodeReview.API.Controllers;

/// <summary>
/// Manages code analysis lifecycle: create, query, delete.
/// Supports manual input, file upload, and GitHub sources.
/// </summary>
[Authorize]
public sealed class AnalysesController : ApiControllerBase
{
    /// <summary>Submit code for analysis (manual input or GitHub source)</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AnalysisSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAnalysisRequest request,
        CancellationToken ct)
    {
        var result = await Sender.Send(new CreateAnalysisCommand
        {
            UserId = CurrentUserId,
            Title = request.Title,
            Code = request.Code,
            Language = request.Language,
            SourceType = request.SourceType,
            FileName = request.FileName,
            RepositoryFullName = request.RepositoryFullName,
            PullRequestNumber = request.PullRequestNumber
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Upload a code file for analysis (.cs, .js, .ts, .py, etc.)</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(512_000)] // 512 KB
    [ProducesResponseType(typeof(AnalysisSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadFile(
        [FromForm] UploadFileRequest request,
        CancellationToken ct)
    {
        if (request.File.Length == 0)
            return BadRequest(new ProblemDetails { Title = "File is empty." });

        var allowedExtensions = new[] { ".cs", ".js", ".ts", ".tsx", ".py", ".java", ".go", ".rs", ".php", ".rb" };
        var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return BadRequest(new ProblemDetails { Title = "Unsupported file type.", Detail = $"Allowed: {string.Join(", ", allowedExtensions)}" });

        using var reader = new StreamReader(request.File.OpenReadStream());
        var code = await reader.ReadToEndAsync(ct);

        var result = await Sender.Send(new CreateAnalysisCommand
        {
            UserId = CurrentUserId,
            Title = request.Title ?? Path.GetFileNameWithoutExtension(request.File.FileName),
            Code = code,
            FileName = request.File.FileName,
            SourceType = CodeSourceType.FileUpload
        }, ct);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Get paginated list of analyses for the current user</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AnalysisSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] AnalysisStatus? status = null,
        CancellationToken ct = default)
    {
        var result = await Sender.Send(new GetAnalysesPagedQuery
        {
            UserId = CurrentUserId,
            Page = page,
            PageSize = pageSize,
            StatusFilter = status
        }, ct);

        return Ok(result);
    }

    /// <summary>Get a specific analysis by ID (includes all issues and metrics)</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AnalysisDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await Sender.Send(new GetAnalysisByIdQuery(id, CurrentUserId), ct);
        return Ok(result);
    }

    /// <summary>Delete an analysis (must belong to current user)</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Sender.Send(new DeleteAnalysisCommand(id, CurrentUserId), ct);
        return NoContent();
    }
}

// ─── Request Models ───────────────────────────────────────────────────────────

public sealed record CreateAnalysisRequest
{
    public required string Title { get; init; }
    public required string Code { get; init; }
    public CodeLanguage? Language { get; init; }
    public CodeSourceType SourceType { get; init; } = CodeSourceType.ManualInput;
    public string? FileName { get; init; }
    public string? RepositoryFullName { get; init; }
    public int? PullRequestNumber { get; init; }
}

public sealed class UploadFileRequest
{
    public required IFormFile File { get; init; }
    public string? Title { get; init; }
}
