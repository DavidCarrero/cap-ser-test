using Northgate.Api.Contracts;
using Northgate.Api.Data;

namespace Northgate.Api.Services;

public sealed class TransactionService
{
    public const int MaxPageSize = 200;

    private readonly ITransactionRepository _transactions;
    private readonly IRateClient _rates;

    public TransactionService(ITransactionRepository transactions, IRateClient rates)
    {
        _transactions = transactions;
        _rates = rates;
    }

    public async Task<IReadOnlyList<TransactionDto>> SearchAsync(
        string customerName,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("A customer name is required.", nameof(customerName));
        }

        if (page < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        var results = await _transactions.SearchAsync(customerName, page, pageSize, cancellationToken);

        if (results.Count == 0)
        {
            return results;
        }

        var rate = await _rates.GetChfRateAsync(cancellationToken);

        // O(n)
        foreach (var transaction in results)
        {
            transaction.AmountChf = transaction.Amount * rate;
        }

        return results;
    }
}
