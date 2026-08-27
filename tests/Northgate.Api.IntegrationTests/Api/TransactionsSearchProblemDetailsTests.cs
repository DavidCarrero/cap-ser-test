using System.Net;
using System.Text.Json;
using Northgate.Api.IntegrationTests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Northgate.Api.IntegrationTests.Api;

/// <summary>
/// The failure paths of the endpoint, end to end through the real middleware.
/// None of these reach a query, so they point at a database that is not there on
/// purpose and need no container.
/// </summary>
public sealed class TransactionsSearchProblemDetailsTests : IAsyncLifetime
{
    private readonly NorthgateApiFactory _factory = new(NorthgateApiFactory.UnreachableDatabase);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private async Task<(HttpStatusCode Status, JsonElement Body)> GetAsync(string query)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var response = await _factory.CreateClient().GetAsync(query, cancellationToken);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));

        return (response.StatusCode, body.RootElement.Clone());
    }

    [Theory]
    [InlineData("/api/transactions/search")]
    [InlineData("/api/transactions/search?customerName=")]
    [InlineData("/api/transactions/search?customerName=%20%20")]
    public async Task A_missing_or_blank_customer_name_is_rejected_before_the_action_runs(string query)
    {
        // The model binder turns a whitespace-only query value into null, so
        // [ApiController] validation answers these -- the service guard never runs.
        // That means they carry the framework's `errors` member and NOT our `code`.
        var (status, body) = await GetAsync(query);

        status.ShouldBe(HttpStatusCode.BadRequest);
        body.TryGetProperty("errors", out var errors).ShouldBeTrue();
        errors.TryGetProperty("customerName", out _).ShouldBeTrue();
        body.TryGetProperty("code", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(201)]
    public async Task A_page_size_outside_the_allowed_range_is_a_422(int pageSize)
    {
        var (status, body) = await GetAsync($"/api/transactions/search?customerName=Ana&pageSize={pageSize}");

        status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        body.GetProperty("code").GetString().ShouldBe("value_out_of_range");
    }

    [Fact]
    public async Task A_negative_page_is_a_422()
    {
        var (status, body) = await GetAsync("/api/transactions/search?customerName=Ana&page=-1");

        status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        body.GetProperty("code").GetString().ShouldBe("value_out_of_range");
    }

    [Fact]
    public async Task An_unreachable_database_is_a_503_and_never_leaks_the_connection_string()
    {
        var (status, body) = await GetAsync("/api/transactions/search?customerName=Ana&pageSize=10");

        status.ShouldBe(HttpStatusCode.ServiceUnavailable);
        body.GetProperty("code").GetString().ShouldBe("database_unavailable");
        body.GetProperty("title").GetString().ShouldBe("Database unavailable");
        body.GetRawText().ShouldNotContain("Password");
        body.GetRawText().ShouldNotContain("127.0.0.1");
    }

    [Fact]
    public async Task The_rates_service_is_never_called_when_the_database_is_down()
    {
        await GetAsync("/api/transactions/search?customerName=Ana&pageSize=10");

        await _factory.Rates.DidNotReceive().GetChfRateAsync(Arg.Any<CancellationToken>());
    }
}
