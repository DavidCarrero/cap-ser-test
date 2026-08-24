using Northgate.Api.Contracts;
using Northgate.Api.Data;
using Northgate.Api.Services;
using Xunit;

namespace Northgate.Api.Tests;

public sealed class CustomerServiceTests
{
    private sealed class StubCustomerRepository : ICustomerRepository
    {
        private readonly CustomerDto? _result;

        public StubCustomerRepository(CustomerDto? result)
        {
            _result = result;
        }

        public Task<CustomerDto?> GetByIdAsync(int customerId, CancellationToken cancellationToken)
            => Task.FromResult(_result);
    }

    [Fact]
    public async Task GetAsync_ReturnsCustomer_WhenRepositoryFindsOne()
    {
        var expected = new CustomerDto(42, "Ana Quispe", "1234567", "PE");
        var service = new CustomerService(new StubCustomerRepository(expected));

        var actual = await service.GetAsync(42, CancellationToken.None);

        Assert.Equal(expected, actual);
    }
}
