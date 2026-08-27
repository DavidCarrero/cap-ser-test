using Northgate.Api.Contracts;
using Northgate.Api.Data;
using Northgate.Api.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Northgate.Api.Tests.Services;

public sealed class TransactionServiceTests
{
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IRateClient _rates = Substitute.For<IRateClient>();
    private readonly TransactionService _service;

    public TransactionServiceTests()
    {
        _service = new TransactionService(_transactions, _rates);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task SearchAsync_rejects_a_blank_customer_name(string? customerName)
    {
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SearchAsync(customerName!, 0, 50, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("customerName");
    }

    [Fact]
    public async Task SearchAsync_rejects_a_negative_page()
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.SearchAsync("Ana", -1, 50, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("page");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(TransactionService.MaxPageSize + 1)]
    public async Task SearchAsync_rejects_a_page_size_outside_the_allowed_range(int pageSize)
    {
        var exception = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _service.SearchAsync("Ana", 0, pageSize, TestContext.Current.CancellationToken));

        exception.ParamName.ShouldBe("pageSize");
    }

    [Fact]
    public async Task SearchAsync_accepts_the_largest_allowed_page_size()
    {
        _transactions
            .SearchAsync("Ana", 0, TransactionService.MaxPageSize, Arg.Any<CancellationToken>())
            .Returns([]);

        var results = await _service.SearchAsync(
            "Ana", 0, TransactionService.MaxPageSize, TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchAsync_converts_every_amount_with_the_chf_rate()
    {
        _transactions
            .SearchAsync("Ana", 0, 50, Arg.Any<CancellationToken>())
            .Returns([
                new TransactionDto { Id = 1, CustomerName = "Ana Quispe", Amount = 1500.0000m, Currency = "USD" },
                new TransactionDto { Id = 2, CustomerName = "Ana Quispe", Amount = 275.5000m, Currency = "USD" }
            ]);
        _rates.GetChfRateAsync(Arg.Any<CancellationToken>()).Returns(0.79515m);

        var results = await _service.SearchAsync("Ana", 0, 50, TestContext.Current.CancellationToken);

        results.Select(t => t.AmountChf).ShouldBe([1192.725000m, 219.06382500m]);
    }

    [Fact]
    public async Task SearchAsync_does_not_call_the_rates_service_when_nothing_matched()
    {
        _transactions
            .SearchAsync("Nobody", 0, 50, Arg.Any<CancellationToken>())
            .Returns([]);

        var results = await _service.SearchAsync("Nobody", 0, 50, TestContext.Current.CancellationToken);

        results.ShouldBeEmpty();
        await _rates.DidNotReceive().GetChfRateAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_forwards_the_paging_arguments_to_the_repository()
    {
        _transactions.SearchAsync("Ana", 3, 25, Arg.Any<CancellationToken>()).Returns([]);

        await _service.SearchAsync("Ana", 3, 25, TestContext.Current.CancellationToken);

        await _transactions.Received(1).SearchAsync("Ana", 3, 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchAsync_surfaces_a_failure_from_the_rates_service()
    {
        _transactions
            .SearchAsync("Ana", 0, 50, Arg.Any<CancellationToken>())
            .Returns([new TransactionDto { Id = 1, Amount = 100m }]);
        _rates
            .GetChfRateAsync(Arg.Any<CancellationToken>())
            .Returns<decimal>(_ => throw new HttpRequestException("rates are down"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _service.SearchAsync("Ana", 0, 50, TestContext.Current.CancellationToken));
    }
}
