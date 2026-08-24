# Northgate Transfers, sandbox API

A small ASP.NET Core API used for a paired technical session. Everything here is fictional and disposable.
Nothing built during the session is kept or shipped.

## Requirements

.NET 10 SDK. Check with `dotnet --version`. A local SQL Server instance is only needed if you want to run the
API against a database; the test suite does not need one.

## Running

```
dotnet build
dotnet test
dotnet run --project src/Northgate.Api
```

The connection string lives in `src/Northgate.Api/appsettings.json` under `ConnectionStrings:Northgate` and
points at a local instance by default.

## What is already here

One endpoint, `GET /api/customers/{customerId}`, and one test.

The endpoint shows the conventions used in this codebase:

- The controller is thin. It calls a service and maps the result to a response.
- Business rules live in `CustomerService`.
- Data access goes through `ICustomerRepository`. The SQL implementation calls a stored procedure with
  parameters and reads columns by name rather than by ordinal.
- Errors are returned as RFC 9457 Problem Details with a stable `code` field, either from the controller for
  expected cases or from `ProblemDetailsExceptionHandler` for everything else.
- Every async path takes a `CancellationToken`.

The test in `tests/Northgate.Api.Tests` covers the service against a stub repository, so it runs without a
database.

## Layout

```
src/Northgate.Api
  Contracts        response records
  Controllers      HTTP surface
  Services         business rules
  Data             repository interface and its SQL implementation
  Infrastructure   exception handling
tests/Northgate.Api.Tests
```
