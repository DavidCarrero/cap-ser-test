using Microsoft.EntityFrameworkCore;
using Northgate.Api.Data;
using Northgate.Api.IntegrationTests.Support;
using Shouldly;
using Xunit;

namespace Northgate.Api.IntegrationTests.Data;

/// <summary>
/// The repository against a container provisioned from db/001_schema.sql, so a
/// drift between the DDL and the EF model fails here rather than in production.
/// </summary>
public sealed class EfTransactionRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private NorthgateDbContext? _db;

    public EfTransactionRepositoryTests(PostgresFixture postgres)
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

    private EfTransactionRepository Repository()
    {
        _postgres.EnsureAvailable();
        return new EfTransactionRepository(_db!);
    }

    // Every test searches for its own token, so classes can run in parallel against
    // the same container without seeing rows seeded by another test.
    private static string Token() => Guid.NewGuid().ToString("N")[..12];

    [Fact]
    public async Task SearchAsync_matches_part_of_the_name_ignoring_case()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Ana Quispe {token}",
            [(1500.0000m, "USD", TimeSpan.FromDays(3))],
            cancellationToken);

        var results = await repository.SearchAsync(token.ToUpperInvariant(), 0, 50, cancellationToken);

        results.ShouldHaveSingleItem().CustomerName.ShouldBe($"Ana Quispe {token}");
    }

    [Fact]
    public async Task SearchAsync_reads_every_column_into_the_dto()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        var customerId = await _postgres.SeedCustomerAsync(
            $"Bruno Ferrer {token}",
            [(8400.2500m, "EUR", TimeSpan.FromDays(5))],
            cancellationToken);

        var transaction = (await repository.SearchAsync(token, 0, 50, cancellationToken)).ShouldHaveSingleItem();

        transaction.Id.ShouldBeGreaterThan(0);
        transaction.CustomerId.ShouldBe(customerId);
        transaction.CustomerName.ShouldBe($"Bruno Ferrer {token}");
        // numeric(19,4) must survive as an exact decimal, not a rounded double.
        transaction.Amount.ShouldBe(8400.2500m);
        transaction.Currency.ShouldBe("EUR");
        transaction.CreatedAt.ShouldBeInRange(
            DateTime.UtcNow.AddDays(-5).AddMinutes(-5),
            DateTime.UtcNow.AddDays(-5).AddMinutes(5));
        transaction.AmountChf.ShouldBe(0m, "the repository does not convert, the service does");
    }

    [Fact]
    public async Task SearchAsync_returns_the_newest_transaction_first()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Claudia Restrepo {token}",
            [
                (100m, "USD", TimeSpan.FromDays(3)),
                (200m, "USD", TimeSpan.FromDays(1)),
                (300m, "USD", TimeSpan.FromDays(2))
            ],
            cancellationToken);

        var results = await repository.SearchAsync(token, 0, 50, cancellationToken);

        results.Select(t => t.Amount).ShouldBe([200m, 300m, 100m]);
    }

    [Fact]
    public async Task SearchAsync_pages_without_repeating_or_dropping_a_row()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Daniel Meier {token}",
            [
                (10m, "CHF", TimeSpan.FromDays(1)),
                (20m, "CHF", TimeSpan.FromDays(2)),
                (30m, "CHF", TimeSpan.FromDays(3)),
                (40m, "CHF", TimeSpan.FromDays(4))
            ],
            cancellationToken);

        var first = await repository.SearchAsync(token, 0, 2, cancellationToken);
        var second = await repository.SearchAsync(token, 1, 2, cancellationToken);

        first.Select(t => t.Amount).ShouldBe([10m, 20m]);
        second.Select(t => t.Amount).ShouldBe([30m, 40m]);
        first.Select(t => t.Id).Intersect(second.Select(t => t.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_breaks_a_tie_on_created_at_so_paging_is_stable()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        // Three rows sharing one instant: without the id tiebreaker the two pages
        // could overlap or skip a row.
        var sameInstant = TimeSpan.FromDays(2);
        await _postgres.SeedCustomerAsync(
            $"Tied {token}",
            [(1m, "USD", sameInstant), (2m, "USD", sameInstant), (3m, "USD", sameInstant)],
            cancellationToken);

        var first = await repository.SearchAsync(token, 0, 2, cancellationToken);
        var second = await repository.SearchAsync(token, 1, 2, cancellationToken);

        first.Count.ShouldBe(2);
        second.Count.ShouldBe(1);
        first.Select(t => t.Id).Intersect(second.Select(t => t.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_returns_nothing_when_no_customer_matches()
    {
        var repository = Repository();

        var results = await repository.SearchAsync(
            Token(), 0, 50, TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_treats_a_percent_sign_in_the_input_as_a_literal()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Ana Quispe {token}",
            [(100m, "USD", TimeSpan.FromDays(1))],
            cancellationToken);

        // Unescaped, the trailing % would be a wildcard and this would match.
        var results = await repository.SearchAsync($"{token}%", 0, 50, cancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_treats_an_underscore_in_the_input_as_a_literal()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync(
            $"Ana Quispe {token}",
            [(100m, "USD", TimeSpan.FromDays(1))],
            cancellationToken);

        // Unescaped, _ matches any single character and this would still match.
        var results = await repository.SearchAsync($"{token[..^1]}_", 0, 50, cancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_does_not_return_a_customer_without_transactions()
    {
        var repository = Repository();
        var token = Token();
        var cancellationToken = TestContext.Current.CancellationToken;

        await _postgres.SeedCustomerAsync($"Lonely {token}", [], cancellationToken);

        var results = await repository.SearchAsync(token, 0, 50, cancellationToken);

        results.ShouldBeEmpty();
    }
}
