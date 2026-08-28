using System.Net;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Northgate.Api.Contracts;
using Northgate.Api.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace Northgate.Api.IntegrationTests.Api;

/// <summary>
/// HTTP in, Postgres out, for the customer read path. Everything between the two
/// is the real application.
/// </summary>
public sealed class CustomersEndpointTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private NorthgateApiFactory? _factory;

    public CustomersEndpointTests(PostgresFixture postgres)
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
    public async Task GET_customer_returns_the_seeded_customer()
    {
        var factory = Factory();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        var customerId = await _postgres.SeedCustomerAsync($"Ana Quispe {token}", [], cancellationToken);

        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/customers/{customerId}", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var customer = await response.Content.ReadFromJsonAsync<CustomerDto>(cancellationToken);
        customer.ShouldNotBeNull();
        customer.CustomerId.ShouldBe(customerId);
        customer.FullName.ShouldBe($"Ana Quispe {token}");
        customer.CountryCode.ShouldBe("PE");
    }

    [Fact]
    public async Task GET_customer_returns_404_problem_details_when_it_does_not_exist()
    {
        var factory = Factory();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/customers/{long.MaxValue}", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
        problem.ShouldNotBeNull();
        problem.Title.ShouldBe("Customer not found");
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GET_customer_rejects_a_non_positive_id_before_reaching_the_database()
    {
        // CustomerService guards the id; the handler turns that into 422.
        var factory = Factory();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/customers/0", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GET_customer_returns_404_for_an_id_that_is_not_a_number()
    {
        // The :long route constraint rejects the request before the action runs.
        var factory = Factory();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/customers/not-a-number", cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
