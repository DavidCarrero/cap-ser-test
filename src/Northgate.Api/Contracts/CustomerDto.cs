namespace Northgate.Api.Contracts;

/// <summary>Read model for a row of <c>customers</c>.</summary>
public sealed record CustomerDto(
    long CustomerId,
    string FullName,
    string DocumentNumber,
    string CountryCode);
