using Northgate.Api.Contracts;
using Northgate.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Northgate.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly CustomerService _customers;

    public CustomersController(CustomerService customers)
    {
        _customers = customers;
    }

    [HttpGet("{customerId:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDto>> GetById(int customerId, CancellationToken cancellationToken)
    {
        var customer = await _customers.GetAsync(customerId, cancellationToken);

        if (customer is null)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Customer not found",
                Instance = HttpContext.Request.Path
            };

            problem.Extensions["code"] = "customer_not_found";

            return NotFound(problem);
        }

        return Ok(customer);
    }
}
