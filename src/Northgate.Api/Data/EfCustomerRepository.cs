using Microsoft.EntityFrameworkCore;
using Northgate.Api.Contracts;

namespace Northgate.Api.Data;

public sealed class EfCustomerRepository : ICustomerRepository
{
    private readonly NorthgateDbContext _db;

    public EfCustomerRepository(NorthgateDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerDto?> GetByIdAsync(long customerId, CancellationToken cancellationToken)
    {
        return await _db.Customers
            .Where(c => c.Id == customerId)
            .Select(c => new CustomerDto(
                c.Id,
                c.FullName,
                c.DocumentNumber,
                c.CountryCode))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
