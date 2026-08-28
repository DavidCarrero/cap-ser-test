using Northgate.Api.Contracts;

namespace Northgate.Api.Data;

public interface ICustomerRepository
{
    Task<CustomerDto?> GetByIdAsync(long customerId, CancellationToken cancellationToken);
}
