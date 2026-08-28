using Microsoft.EntityFrameworkCore;
using Northgate.Api.Contracts;

namespace Northgate.Api.Data;

public sealed class EfTransactionRepository : ITransactionRepository
{
    private readonly NorthgateDbContext _db;

    public EfTransactionRepository(NorthgateDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TransactionDto>> SearchAsync(
        string customerName,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var pattern = $"%{EscapeLikeWildcards(customerName)}%";

        // Projecting straight to the DTO keeps this a single SELECT of exactly the
        // six columns needed -- no entity materialization, so AsNoTracking is
        // redundant here.
        return await _db.Transactions
            .Where(t => EF.Functions.ILike(t.Customer!.FullName, pattern, @"\"))
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip(page * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionDto
            {
                Id = t.Id,
                CustomerId = t.CustomerId,
                CustomerName = t.Customer!.FullName,
                Amount = t.Amount,
                Currency = t.CurrencyCode,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    // %, _ and the backslash are wildcards inside ILIKE; a user searching for "100%" means the literal characters.
    private static string EscapeLikeWildcards(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
