using Northgate.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Northgate.Api.IntegrationTests.Support;

/// <summary>
/// The real API, in memory, against a real database. Only the outbound rates call
/// is substituted -- an integration test must not depend on a third party being up.
/// </summary>
public sealed class NorthgateApiFactory : WebApplicationFactory<Program>
{
    /// <summary>A connection string pointing at a port nothing listens on.</summary>
    public const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=northgate;Username=northgate;Password=none;Timeout=2;Command Timeout=2";

    private readonly string _connectionString;

    public NorthgateApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IRateClient Rates { get; } = Substitute.For<IRateClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:NorthgatePostgres", _connectionString);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRateClient>();
            services.AddSingleton(Rates);
        });
    }
}
