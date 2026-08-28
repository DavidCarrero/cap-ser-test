using Northgate.Api.Contracts;
using Northgate.Api.Data;

namespace Northgate.Api.Services;

public sealed class TransactionService
{
    public const int MaxPageSize = 200;

    /// <summary>The currency every amount is reported in, as ISO 4217.</summary>
    private const string ChfCurrencyCode = "CHF";

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

        foreach (var transaction in results)
        {
            transaction.AmountChf = transaction.Amount * ConversionFactor(transaction.Currency, rate);
        }

        return results;
    }

    /// <summary>
    /// An amount already denominated in CHF converts one-to-one. Applying the quoted
    /// rate to it would restate the amount as though it were foreign currency, which
    /// is how 12,000 CHF used to be reported as 9,541.80 CHF.
    /// </summary>
    /// <remarks>
    /// Every other currency still shares the single quote the rates service returns.
    /// Converting EUR and USD correctly needs a per-pair lookup, which the fx_rates
    /// table already models but the rates service does not yet expose.
    /// </remarks>
    private static decimal ConversionFactor(string currency, decimal chfRate) =>
        string.Equals(currency, ChfCurrencyCode, StringComparison.OrdinalIgnoreCase)
            ? 1m
            : chfRate;
}
