using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Northgate.Api.Infrastructure;

public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;

    public ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);

        var (status, code, title) = exception switch
        {
            ArgumentOutOfRangeException => (StatusCodes.Status422UnprocessableEntity, "value_out_of_range", "Value out of range"),
            ArgumentException => (StatusCodes.Status400BadRequest, "invalid_request", "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "internal_error", "Internal server error")
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Instance = httpContext.Request.Path
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
