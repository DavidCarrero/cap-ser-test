using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

[assembly: AssemblyFixture(typeof(Northgate.Api.IntegrationTests.Support.PostgresFixture))]

namespace Northgate.Api.IntegrationTests.Support;

/// <summary>
/// One Postgres container for the whole assembly, provisioned from the checked-in
/// db/*.sql. Sample data is deliberately not loaded: each test seeds its own rows
/// under a unique token so classes can keep running in parallel.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    private bool _started;

    /// <summary>Non-null when the container could not start, e.g. Docker is not running.</summary>
    public string? SkipReason { get; private set; }

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
            _started = true;

            await ExecuteAsync(await ReadScriptAsync("001_schema.sql"));
            await ExecuteAsync(await ReadScriptAsync("002_seed_reference.sql"));
        }
        catch (Exception exception)
        {
            SkipReason = $"Postgres container unavailable ({exception.GetType().Name}: {exception.Message}). " +
                         "Start Docker to run the integration suite.";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_started)
        {
            await _container.DisposeAsync();
        }
    }

    /// <summary>Skips the calling test when there is no container to talk to.</summary>
    public void EnsureAvailable()
    {
        if (SkipReason is not null)
        {
            Assert.Skip(SkipReason);
        }
    }

    public async Task<NpgsqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    /// <summary>Seeds one customer and its transactions, and returns the customer id.</summary>
    public async Task<long> SeedCustomerAsync(
        string fullName,
        IReadOnlyList<(decimal Amount, string Currency, TimeSpan Age)> transactions,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        await using var insertCustomer = new NpgsqlCommand(
            "INSERT INTO customers (full_name, document_number, country_code) VALUES (@name, @document, 'PE') RETURNING id",
            connection);
        insertCustomer.Parameters.AddWithValue("name", fullName);
        insertCustomer.Parameters.AddWithValue("document", Guid.NewGuid().ToString("N"));

        var customerId = (long)(await insertCustomer.ExecuteScalarAsync(cancellationToken))!;

        foreach (var (amount, currency, age) in transactions)
        {
            await using var insertTransaction = new NpgsqlCommand(
                """
                INSERT INTO transactions (customer_id, amount, currency_code, created_at)
                VALUES (@customer_id, @amount, @currency, now() - @age)
                """,
                connection);
            insertTransaction.Parameters.AddWithValue("customer_id", customerId);
            insertTransaction.Parameters.AddWithValue("amount", amount);
            insertTransaction.Parameters.AddWithValue("currency", currency);
            insertTransaction.Parameters.AddWithValue("age", age);

            await insertTransaction.ExecuteNonQueryAsync(cancellationToken);
        }

        return customerId;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadScriptAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "db", fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Schema script '{fileName}' was not copied to the test output. Check the csproj None/Link item.", path);
        }

        return await File.ReadAllTextAsync(path);
    }
}
