using CaseInterview.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CaseInterview.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);

        var (statusCode, title, detail, errors) = exception switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                ve.Message,
                ve.Errors
            ),
            NotFoundException nfe => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                nfe.Message,
                null
            ),
            UnauthorizedException ue => (
                StatusCodes.Status403Forbidden,
                "Access Denied",
                ue.Message,
                null
            ),
            DomainException de => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                de.Message,
                null
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                _env.IsDevelopment() ? exception.ToString() : "An internal server error occurred. Please try again later.",
                null
            )
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        if (errors != null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
