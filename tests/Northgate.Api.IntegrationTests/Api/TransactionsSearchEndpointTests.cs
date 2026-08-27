using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Northgate.Api.Contracts;
using Northgate.Api.IntegrationTests.Support;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Northgate.Api.IntegrationTests.Api;

/// <summary>
/// HTTP in, Postgres out. Everything between the two is the real application.
/// </summary>
public sealed class TransactionsSearchEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private NorthgateApiFactory? _factory;

    public TransactionsSearchEndpointTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public ValueTask InitializeAsync()
    {
        if (_postgres.SkipReason is null)
        {
            _factory = new NorthgateApiFactory(_postgres.ConnectionString);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private NorthgateApiFactory Factory()
    {
        _postgres.EnsureAvailable();
        return _factory!;
    }

    private static string Token() => Guid.NewGuid().ToString("N")[..12];

    [Fact]
    public async Task GET_search_returns_the_matching_transactions_converted_to_chf()
    {
        var factory = Factory();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Ana Quispe {token}",
            [
                (1500.0000m, "USD", TimeSpan.FromDays(3)),
                (275.5000m, "USD", TimeSpan.FromDays(2))
            ],
            cancellationToken);

        factory.Rates.GetChfRateAsync(Arg.Any<CancellationToken>()).Returns(0.79515m);

        var response = await factory.CreateClient()
            .GetAsync($"/api/transactions/search?customerName={token}&page=0&pageSize=50", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<List<TransactionDto>>(cancellationToken);

        payload.ShouldNotBeNull();
        payload.Count.ShouldBe(2);
        // Newest first.
        payload[0].Amount.ShouldBe(275.5000m);
        payload[0].AmountChf.ShouldBe(219.06382500m);
        payload[1].Amount.ShouldBe(1500.0000m);
        payload[1].AmountChf.ShouldBe(1192.725000m);
        payload.ShouldAllBe(t => t.CustomerName == $"Ana Quispe {token}");
    }

    [Fact]
    public async Task GET_search_returns_an_empty_array_when_nothing_matches()
    {
        var factory = Factory();
        var cancellationToken = TestContext.Current.CancellationToken;

        var response = await factory.CreateClient()
            .GetAsync($"/api/transactions/search?customerName={Token()}", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<List<TransactionDto>>(cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GET_search_honours_the_requested_page_size()
    {
        var factory = Factory();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Daniel Meier {token}",
            [
                (10m, "CHF", TimeSpan.FromDays(1)),
                (20m, "CHF", TimeSpan.FromDays(2)),
                (30m, "CHF", TimeSpan.FromDays(3))
            ],
            cancellationToken);

        factory.Rates.GetChfRateAsync(Arg.Any<CancellationToken>()).Returns(1m);

        var response = await factory.CreateClient()
            .GetAsync($"/api/transactions/search?customerName={token}&page=0&pageSize=2", cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<List<TransactionDto>>(cancellationToken);

        payload.ShouldNotBeNull().Count.ShouldBe(2);
    }

    [Fact]
    public async Task GET_search_returns_502_when_the_rates_service_fails()
    {
        var factory = Factory();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Bruno Ferrer {token}",
            [(100m, "EUR", TimeSpan.FromDays(1))],
            cancellationToken);

        factory.Rates
            .GetChfRateAsync(Arg.Any<CancellationToken>())
            .Returns<decimal>(_ => throw new HttpRequestException("rates are down"));

        var response = await factory.CreateClient()
            .GetAsync($"/api/transactions/search?customerName={token}", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadGateway);

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        problem.RootElement.GetProperty("code").GetString().ShouldBe("upstream_error");
    }

    [Fact]
    public async Task GET_search_does_not_call_the_rates_service_when_nothing_matched()
    {
        var factory = Factory();
        var cancellationToken = TestContext.Current.CancellationToken;

        factory.Rates.ClearReceivedCalls();

        await factory.CreateClient()
            .GetAsync($"/api/transactions/search?customerName={Token()}", cancellationToken);

        await factory.Rates.DidNotReceive().GetChfRateAsync(Arg.Any<CancellationToken>());
    }
}
