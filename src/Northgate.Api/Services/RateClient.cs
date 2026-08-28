using System.Net.Http.Json;
using Northgate.Api.Contracts;

namespace Northgate.Api.Services;

public sealed class RateClient : IRateClient
{
    private readonly HttpClient _httpClient;

    public RateClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<decimal> GetChfRateAsync(CancellationToken cancellationToken)
    {
        var rate = await _httpClient.GetFromJsonAsync<RateDto>("chf", cancellationToken);

        if (rate is null)
        {
            throw new HttpRequestException("The rates service returned an empty CHF rate.");
        }

        return rate.Value;
    }
}
