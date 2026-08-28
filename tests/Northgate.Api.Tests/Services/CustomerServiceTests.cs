using Northgate.Api.Contracts;
using Northgate.Api.Data;
using Northgate.Api.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Northgate.Api.Tests.Services;

public sealed class CustomerServiceTests
{
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly CustomerService _service;

    public CustomerServiceTests()
    {
        _service = new CustomerService(_customers);
    }

    [Fact]
    public async Task GetAsync_returns_the_customer_the_repository_found()
    {
        var expected = new CustomerDto(42, "Ana Quispe", "1234567", "PE");
        _customers.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns(expected);

        var actual = await _service.GetAsync(42, TestContext.Current.CancellationToken);

        actual.ShouldBe(expected);
    }

    [Fact]
    public async Task GetAsync_returns_null_when_no_customer_matches()
    {
        _customers.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((CustomerDto?)null);

        var actual = await _service.GetAsync(99, TestContext.Current.CancellationToken);

        actual.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetAsync_rejects_a_non_positive_id(int customerId)
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.GetAsync(customerId, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("customerId");
    }
}
