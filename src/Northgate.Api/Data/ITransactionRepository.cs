using Northgate.Api.Contracts;

namespace Northgate.Api.Data;

public interface ITransactionRepository
{
    Task<IReadOnlyList<TransactionDto>> SearchAsync(
        string customerName,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
