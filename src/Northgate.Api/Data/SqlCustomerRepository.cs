using System.Data;
using Northgate.Api.Contracts;
using Microsoft.Data.SqlClient;

namespace Northgate.Api.Data;

public sealed class SqlCustomerRepository : ICustomerRepository
{
    private readonly string _connectionString;

    public SqlCustomerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Northgate")
            ?? throw new InvalidOperationException("Connection string 'Northgate' is not configured.");
    }

    public async Task<CustomerDto?> GetByIdAsync(long customerId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await using var command = new SqlCommand("usp_Customer_GetById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@CustomerId", SqlDbType.BigInt).Value = customerId;

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CustomerDto(
            reader.GetInt64(reader.GetOrdinal("CustomerId")),
            reader.GetString(reader.GetOrdinal("FullName")),
            reader.GetString(reader.GetOrdinal("DocumentNumber")),
            reader.GetString(reader.GetOrdinal("CountryCode")));
    }
}
