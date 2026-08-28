using Microsoft.EntityFrameworkCore;
using Northgate.Api.Data;
using Northgate.Api.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace Northgate.Api.IntegrationTests.Data;

/// <summary>
/// db/001_schema.sql is the source of truth for the model. Each of these queries
/// the real DDL, so a column renamed on either side fails loudly here.
/// </summary>
public sealed class NorthgateDbContextTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private NorthgateDbContext? _db;

    public NorthgateDbContextTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    public ValueTask InitializeAsync()
    {
        if (_postgres.SkipReason is null)
        {
            _db = new NorthgateDbContext(
                new DbContextOptionsBuilder<NorthgateDbContext>()
                    .UseNpgsql(_postgres.ConnectionString)
                    .Options);
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

    private NorthgateDbContext Db()
    {
        _postgres.EnsureAvailable();
        return _db!;
    }

    [Fact]
    public async Task Every_mapped_table_can_be_queried_against_the_checked_in_schema()
    {
        var db = Db();
        var cancellationToken = TestContext.Current.CancellationToken;

        await Should.NotThrowAsync(async () =>
        {
            await db.Countries.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            await db.Currencies.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            await db.Customers.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            await db.FxRates.AsNoTracking().Take(1).ToListAsync(cancellationToken);
            await db.Transactions.AsNoTracking().Take(1).ToListAsync(cancellationToken);
        });
    }

    [Fact]
    public async Task The_reference_seed_is_reachable_through_the_model()
    {
        var db = Db();
        var cancellationToken = TestContext.Current.CancellationToken;

        var currency = await db.Currencies
            .AsNoTracking()
            .SingleAsync(c => c.Code == "CHF", cancellationToken);

        currency.Name.ShouldBe("Swiss Franc");
        currency.MinorUnit.ShouldBe((short)2);
    }

    [Fact]
    public async Task A_transaction_can_be_navigated_to_its_customer()
    {
        var db = Db();
        var token = Guid.NewGuid().ToString("N")[..12];
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Navigable {token}",
            [(42.0000m, "USD", TimeSpan.FromHours(1))],
            cancellationToken);

        var transaction = await db.Transactions
            .AsNoTracking()
            .Include(t => t.Customer)
            .SingleAsync(t => t.Customer!.FullName == $"Navigable {token}", cancellationToken);

        transaction.Customer.ShouldNotBeNull().FullName.ShouldBe($"Navigable {token}");
        transaction.CurrencyCode.ShouldBe("USD");
        transaction.Amount.ShouldBe(42.0000m);
    }
}
