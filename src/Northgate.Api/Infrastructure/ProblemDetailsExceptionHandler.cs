using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

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
        // The client hung up: nothing left to write a response to.
        if (cancellationToken.IsCancellationRequested && exception is OperationCanceledException)
        {
            _logger.LogInformation("Request cancelled by the client for {Path}", httpContext.Request.Path);
            return true;
        }

        _logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);

        var (status, code, title) = Map(exception);

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

    // EF Core does not surface provider errors directly: a failed connection arrives
    // as InvalidOperationException("...likely due to a transient failure") wrapping an
    // NpgsqlException, and a failed write as DbUpdateException wrapping a
    // PostgresException. Matching only on the outermost type would report every one of
    // those as a generic 500, so an unmatched exception is retried against its cause.
    private static (int Status, string Code, string Title) Map(Exception exception) => exception switch
    {
        ArgumentOutOfRangeException => (StatusCodes.Status422UnprocessableEntity, "value_out_of_range", "Value out of range"),
        ArgumentException => (StatusCodes.Status400BadRequest, "invalid_request", "Invalid request"),
        PostgresException => (StatusCodes.Status500InternalServerError, "database_error", "Database error"),
        NpgsqlException => (StatusCodes.Status503ServiceUnavailable, "database_unavailable", "Database unavailable"),
        TimeoutException => (StatusCodes.Status504GatewayTimeout, "upstream_timeout", "Upstream service timed out"),
        OperationCanceledException => (StatusCodes.Status504GatewayTimeout, "upstream_timeout", "Upstream service timed out"),
        HttpRequestException => (StatusCodes.Status502BadGateway, "upstream_error", "Upstream service error"),
        { InnerException: { } inner } => Map(inner),
        _ => (StatusCodes.Status500InternalServerError, "internal_error", "Internal server error")
    };
}
