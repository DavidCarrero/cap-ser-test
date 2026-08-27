using System.Text.Json;
using Northgate.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shouldly;
using Xunit;

namespace Northgate.Api.Tests.Infrastructure;

public sealed class ProblemDetailsExceptionHandlerTests
{
    private readonly ProblemDetailsExceptionHandler _handler =
        new(NullLogger<ProblemDetailsExceptionHandler>.Instance);

    private static DefaultHttpContext ContextWithBody()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/transactions/search";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.Clone();
    }

    public static TheoryData<Exception, int, string> Mappings => new()
    {
        { new ArgumentOutOfRangeException("pageSize"), StatusCodes.Status422UnprocessableEntity, "value_out_of_range" },
        { new ArgumentException("blank", "customerName"), StatusCodes.Status400BadRequest, "invalid_request" },
        { new PostgresException("relation does not exist", "ERROR", "ERROR", "42P01"), StatusCodes.Status500InternalServerError, "database_error" },
        { new NpgsqlException("connection refused"), StatusCodes.Status503ServiceUnavailable, "database_unavailable" },
        { new TimeoutException("too slow"), StatusCodes.Status504GatewayTimeout, "upstream_timeout" },
        { new HttpRequestException("rates are down"), StatusCodes.Status502BadGateway, "upstream_error" },
        { new InvalidOperationException("anything else"), StatusCodes.Status500InternalServerError, "internal_error" },
        // EF Core wraps provider failures; the mapping has to see through the wrapper.
        {
            new InvalidOperationException(
                "An exception has been raised that is likely due to a transient failure.",
                new NpgsqlException("Failed to connect to 127.0.0.1:5432")),
            StatusCodes.Status503ServiceUnavailable,
            "database_unavailable"
        },
        {
            new DbUpdateException(
                "An error occurred while saving the entity changes.",
                new PostgresException("duplicate key", "ERROR", "ERROR", "23505")),
            StatusCodes.Status500InternalServerError,
            "database_error"
        }
    };

    [Theory]
    [MemberData(nameof(Mappings))]
    public async Task TryHandleAsync_maps_the_exception_to_a_status_and_a_stable_code(
        Exception exception, int expectedStatus, string expectedCode)
    {
        var context = ContextWithBody();

        var handled = await _handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(expectedStatus);

        var body = await ReadBodyAsync(context);
        body.GetProperty("status").GetInt32().ShouldBe(expectedStatus);
        body.GetProperty("code").GetString().ShouldBe(expectedCode);
    }

    [Fact]
    public async Task TryHandleAsync_writes_the_request_path_and_a_trace_id()
    {
        var context = ContextWithBody();
        context.TraceIdentifier = "trace-42";

        await _handler.TryHandleAsync(context, new InvalidOperationException(), TestContext.Current.CancellationToken);

        var body = await ReadBodyAsync(context);
        body.GetProperty("instance").GetString().ShouldBe("/api/transactions/search");
        body.GetProperty("traceId").GetString().ShouldBe("trace-42");
    }

    [Fact]
    public async Task TryHandleAsync_does_not_map_a_postgres_error_onto_the_generic_500_code()
    {
        // PostgresException derives from NpgsqlException; order in the switch matters.
        var context = ContextWithBody();

        await _handler.TryHandleAsync(
            context,
            new PostgresException("syntax error", "ERROR", "ERROR", "42601"),
            TestContext.Current.CancellationToken);

        var body = await ReadBodyAsync(context);
        body.GetProperty("code").GetString().ShouldBe("database_error");
    }

    [Fact]
    public async Task TryHandleAsync_writes_nothing_when_the_client_cancelled_the_request()
    {
        var context = ContextWithBody();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var handled = await _handler.TryHandleAsync(
            context, new OperationCanceledException(), cancelled.Token);

        handled.ShouldBeTrue();
        context.Response.Body.Length.ShouldBe(0);
    }
}
