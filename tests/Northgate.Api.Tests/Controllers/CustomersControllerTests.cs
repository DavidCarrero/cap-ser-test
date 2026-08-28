using Northgate.Api.Contracts;
using Northgate.Api.Controllers;
using Northgate.Api.Data;
using Northgate.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Northgate.Api.Tests.Controllers;

// CustomerService is a concrete, sealed collaborator, so the controller is
// exercised against a real one wired to a substituted repository.
public sealed class CustomersControllerTests
{
    private const string RequestPath = "/api/customers/42";

    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();

    private static CustomersController Controller(CustomerService service) =>
        new(service)
        {
            // Instance is read off the request, so the controller needs a context.
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { Request = { Path = RequestPath } }
            }
        };

    private CustomersController Controller() => Controller(new CustomerService(_customers));

    [Fact]
    public async Task GetById_returns_200_with_the_customer()
    {
        var expected = new CustomerDto(42, "Ana Quispe", "1234567", "PE");
        _customers.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await Controller().GetById(42, TestContext.Current.CancellationToken);

        var ok = result.Result.ShouldBeOfType<OkObjectResult>();
        ok.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetById_returns_404_when_the_customer_does_not_exist()
    {
        _customers.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns((CustomerDto?)null);

        var result = await Controller().GetById(42, TestContext.Current.CancellationToken);

        var notFound = result.Result.ShouldBeOfType<NotFoundObjectResult>();
        notFound.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetById_describes_the_miss_as_a_problem_document()
    {
        _customers.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns((CustomerDto?)null);

        var result = await Controller().GetById(42, TestContext.Current.CancellationToken);

        var problem = result.Result
            .ShouldBeOfType<NotFoundObjectResult>()
            .Value
            .ShouldBeOfType<ProblemDetails>();

        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Title.ShouldBe("Customer not found");
        problem.Instance.ShouldBe(RequestPath);
        // The machine-readable code is what clients branch on, so it is part of the contract.
        problem.Extensions["code"].ShouldBe("customer_not_found");
    }

    [Fact]
    public async Task GetById_passes_the_requested_id_through_to_the_repository()
    {
        _customers.GetByIdAsync(Arg.Any<long>(), Arg.Any<CancellationToken>()).Returns((CustomerDto?)null);

        await Controller().GetById(7, TestContext.Current.CancellationToken);

        await _customers.Received(1).GetByIdAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_accepts_an_id_beyond_the_range_of_int()
    {
        // customers.id is bigint; the route constraint is :long for this reason.
        const long id = (long)int.MaxValue + 1;
        var expected = new CustomerDto(id, "Daniel Meier", "756.1234", "CH");
        _customers.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns(expected);

        var result = await Controller().GetById(id, TestContext.Current.CancellationToken);

        result.Result.ShouldBeOfType<OkObjectResult>().Value.ShouldBe(expected);
    }

    [Fact]
    public async Task GetById_lets_a_validation_failure_reach_the_exception_handler()
    {
        // The controller has no try/catch: the middleware turns this into a problem document.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Controller().GetById(0, TestContext.Current.CancellationToken));
    }
}
