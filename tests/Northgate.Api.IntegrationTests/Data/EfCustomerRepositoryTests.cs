using Microsoft.EntityFrameworkCore;
using Northgate.Api.Data;
using Northgate.Api.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace Northgate.Api.IntegrationTests.Data;

/// <summary>
/// The customer repository against a container provisioned from db/001_schema.sql, so a
/// drift between the DDL and the EF model fails here rather than in production.
/// </summary>
public sealed class EfCustomerRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private NorthgateDbContext? _db;

    public EfCustomerRepositoryTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public ValueTask InitializeAsync()
    {
        if (_postgres.SkipReason is null)
        {
            var options = new DbContextOptionsBuilder<NorthgateDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options;

            _db = new NorthgateDbContext(options);
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }

    private EfCustomerRepository Repository()
    {
        _postgres.EnsureAvailable();
        return new EfCustomerRepository(_db!);
    }

    // Every test seeds under its own token, so classes can run in parallel against
    // the same container without colliding on the unique document number.
    private static string Token() => Guid.NewGuid().ToString("N")[..12];

    [Fact]
    public async Task GetByIdAsync_returns_the_customer_with_every_column_mapped()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        var customerId = await _postgres.SeedCustomerAsync($"Ana Quispe {token}", [], cancellationToken);

        var customer = await repository.GetByIdAsync(customerId, cancellationToken);

        customer.ShouldNotBeNull();
        customer.CustomerId.ShouldBe(customerId);
        customer.FullName.ShouldBe($"Ana Quispe {token}");
        customer.DocumentNumber.ShouldNotBeNullOrWhiteSpace();
        // country_code is char(2); a padded value would break clients comparing to "PE".
        customer.CountryCode.ShouldBe("PE");
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_when_no_customer_has_that_id()
    {
        var repository = Repository();
        var cancellationToken = TestContext.Current.CancellationToken;

        var customer = await repository.GetByIdAsync(long.MaxValue, cancellationToken);

        customer.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_returns_only_the_requested_customer()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        var wanted = await _postgres.SeedCustomerAsync($"Bruno Ferrer {token}", [], cancellationToken);
        var other = await _postgres.SeedCustomerAsync($"Claudia Restrepo {token}", [], cancellationToken);

        var customer = await repository.GetByIdAsync(wanted, cancellationToken);

        customer.ShouldNotBeNull();
        customer.CustomerId.ShouldBe(wanted);
        customer.CustomerId.ShouldNotBe(other);
        customer.FullName.ShouldBe($"Bruno Ferrer {token}");
    }

    [Fact]
    public async Task GetByIdAsync_reads_a_customer_that_has_no_transactions()
    {
        // The customer read model is independent of the transactions join.
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        var customerId = await _postgres.SeedCustomerAsync($"Daniel Meier {token}", [], cancellationToken);

        var customer = await repository.GetByIdAsync(customerId, cancellationToken);

        customer.ShouldNotBeNull();
        customer.CustomerId.ShouldBe(customerId);
    }
}
