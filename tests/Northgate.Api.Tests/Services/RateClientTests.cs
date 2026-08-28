using System.Net;
using System.Text;
using Northgate.Api.Services;
using Shouldly;
using Xunit;

namespace Northgate.Api.Tests.Services;

public sealed class RateClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static RateClient ClientFor(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.rates.local/") });

    [Fact]
    public async Task GetChfRateAsync_returns_the_rate_from_the_payload()
    {
        var client = ClientFor(new StubHandler(HttpStatusCode.OK, """{"value":0.79515}"""));

        var rate = await client.GetChfRateAsync(TestContext.Current.CancellationToken);

        rate.ShouldBe(0.79515m);
    }

    [Fact]
    public async Task GetChfRateAsync_calls_the_chf_endpoint()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"value":1}""");

        await ClientFor(handler).GetChfRateAsync(TestContext.Current.CancellationToken);

        handler.LastRequestUri.ShouldBe(new Uri("https://api.rates.local/chf"));
    }

    [Fact]
    public async Task GetChfRateAsync_fails_when_the_service_returns_an_error_status()
    {
        var client = ClientFor(new StubHandler(HttpStatusCode.InternalServerError, "boom"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetChfRateAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetChfRateAsync_fails_when_the_service_returns_a_null_body()
    {
        var client = ClientFor(new StubHandler(HttpStatusCode.OK, "null"));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GetChfRateAsync(TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("empty CHF rate");
    }
}
