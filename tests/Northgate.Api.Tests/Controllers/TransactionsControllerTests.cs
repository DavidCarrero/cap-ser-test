using Northgate.Api.Contracts;
using Northgate.Api.Controllers;
using Northgate.Api.Data;
using Northgate.Api.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Northgate.Api.Tests.Controllers;

// TransactionService is a concrete, sealed collaborator, so the controller is
// exercised against a real one wired to substituted ports.
public sealed class TransactionsControllerTests
{
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IRateClient _rates = Substitute.For<IRateClient>();

    private TransactionsController Controller() =>
        new(new TransactionService(_transactions, _rates));

    [Fact]
    public async Task Search_returns_200_with_the_converted_transactions()
    {
        _transactions
            .SearchAsync("Ana", 0, 50, Arg.Any<CancellationToken>())
            .Returns([new TransactionDto { Id = 1, CustomerName = "Ana Quispe", Amount = 100m, Currency = "USD" }]);
        _rates.GetChfRateAsync(Arg.Any<CancellationToken>()).Returns(0.8m);

        var result = await Controller().Search("Ana", TestContext.Current.CancellationToken, 0, 50);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        var payload = ok.Value.ShouldBeAssignableTo<IReadOnlyList<TransactionDto>>();
        payload.ShouldHaveSingleItem().AmountChf.ShouldBe(80m);
    }

    [Fact]
    public async Task Search_lets_a_validation_failure_reach_the_exception_handler()
    {
        // The controller has no try/catch: the middleware turns this into a problem document.
        await Assert.ThrowsAsync<ArgumentException>(
            () => Controller().Search("  ", TestContext.Current.CancellationToken, 0, 50));
    }
}
