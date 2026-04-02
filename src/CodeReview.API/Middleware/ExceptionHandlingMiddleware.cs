using CodeReview.Application.Common.Exceptions;
using CodeReview.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace CodeReview.API.Middleware;

/// <summary>
/// Global exception handler middleware — centralizes error response formatting.
/// Maps exceptions to RFC 7807 ProblemDetails responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problem) = exception switch
        {
            Application.Common.Exceptions.ValidationException ve => (
                HttpStatusCode.UnprocessableEntity,
                new ValidationProblemDetails(ve.Errors)
                {
                    Title = "Validation failed",
                    Status = (int)HttpStatusCode.UnprocessableEntity,
                    Detail = "One or more validation errors occurred.",
                    Instance = context.Request.Path
                }),

            Application.Common.Exceptions.NotFoundException nfe => (
                HttpStatusCode.NotFound,
                new ProblemDetails
                {
                    Title = "Resource not found",
                    Status = (int)HttpStatusCode.NotFound,
                    Detail = nfe.Message,
                    Instance = context.Request.Path
                }),

            Application.Common.Exceptions.ForbiddenException fe => (
                HttpStatusCode.Forbidden,
                new ProblemDetails
                {
                    Title = "Access denied",
                    Status = (int)HttpStatusCode.Forbidden,
                    Detail = fe.Message,
                    Instance = context.Request.Path
                }),

            Application.Common.Exceptions.ConflictException ce => (
                HttpStatusCode.Conflict,
                new ProblemDetails
                {
                    Title = "Conflict",
                    Status = (int)HttpStatusCode.Conflict,
                    Detail = ce.Message,
                    Instance = context.Request.Path
                }),

            DomainException de => (
                HttpStatusCode.BadRequest,
                new ProblemDetails
                {
                    Title = "Domain rule violation",
                    Status = (int)HttpStatusCode.BadRequest,
                    Detail = de.Message,
                    Instance = context.Request.Path
                }),

            OperationCanceledException => (
                HttpStatusCode.RequestTimeout,
                new ProblemDetails
                {
                    Title = "Request cancelled",
                    Status = (int)HttpStatusCode.RequestTimeout,
                    Detail = "The request was cancelled or timed out.",
                    Instance = context.Request.Path
                }),

            _ => (
                HttpStatusCode.InternalServerError,
                new ProblemDetails
                {
                    Title = "An unexpected error occurred",
                    Status = (int)HttpStatusCode.InternalServerError,
                    Detail = "An internal server error has occurred. Please try again later.",
                    Instance = context.Request.Path
                })
        };

        // Log server errors with full stack trace; client errors at warning level
        if ((int)statusCode >= 500)
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning(exception, "Handled exception ({Status}) for {Method} {Path}",
                (int)statusCode, context.Request.Method, context.Request.Path);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
