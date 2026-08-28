using Northgate.Api.Contracts;
using Northgate.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Northgate.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController : ControllerBase
{
    private readonly TransactionService _transactions;

    public TransactionsController(TransactionService transactions)
    {
        _transactions = transactions;
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> Search(
        [FromQuery] string customerName,
        CancellationToken cancellationToken,
        [FromQuery] int page = 0,
        [FromQuery] int pageSize = 50)
    {
        var results = await _transactions.SearchAsync(customerName, page, pageSize, cancellationToken);

        return Ok(results);
    }
}
