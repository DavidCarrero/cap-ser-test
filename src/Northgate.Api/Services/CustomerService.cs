using Northgate.Api.Contracts;
using Northgate.Api.Data;

namespace Northgate.Api.Services;

public sealed class CustomerService
{
    private readonly ICustomerRepository _customers;

    public CustomerService(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<CustomerDto?> GetAsync(int customerId, CancellationToken cancellationToken)
    {
        if (customerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(customerId));
        }

        return await _customers.GetByIdAsync(customerId, cancellationToken);
    }
}
