namespace Northgate.Api.Services;

public interface IRateClient
{
    Task<decimal> GetChfRateAsync(CancellationToken cancellationToken);
}
