using Microsoft.EntityFrameworkCore;
using Northgate.Api.Data;
using Northgate.Api.Infrastructure;
using Northgate.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.Services.AddDbContext<NorthgateDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("NorthgatePostgres")
            ?? throw new InvalidOperationException("Connection string 'NorthgatePostgres' is not configured.")));

builder.Services.AddScoped<ICustomerRepository, EfCustomerRepository>();
builder.Services.AddScoped<CustomerService>();

builder.Services.AddScoped<ITransactionRepository, EfTransactionRepository>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddHttpClient<IRateClient, RateClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Rates:BaseAddress"] ?? "https://api.rates.local/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseExceptionHandler();
app.MapControllers();

app.Run();

public partial class Program;
