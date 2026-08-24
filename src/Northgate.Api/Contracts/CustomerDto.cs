namespace Northgate.Api.Contracts;

public sealed record CustomerDto(
    int CustomerId,
    string FullName,
    string DocumentNumber,
    string CountryCode);
