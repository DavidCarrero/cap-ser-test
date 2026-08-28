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

## Tests

Two projects, both xUnit v3 on Microsoft Testing Platform:

```
tests/Northgate.Api.Tests              unit; no I/O, runs in well under a second
tests/Northgate.Api.IntegrationTests   adapters and the HTTP surface, against a real Postgres
```

```
dotnet test                                                   # everything
dotnet test tests/Northgate.Api.Tests                         # unit only
dotnet test tests/Northgate.Api.IntegrationTests              # integration and E2E only
```

The integration project starts a `postgres:17-alpine` container with Testcontainers,
provisions it from `db/001_schema.sql` and `db/002_seed_reference.sql`, and shares one
container across the assembly. Each test seeds its own rows under a unique token, so the
classes still run in parallel. Sample data (`db/003_seed_sample.sql`) is deliberately not
loaded.

Docker must be running. Without it the container-backed tests report as **skipped**
rather than failing, so on CI assert that the skip count is zero -- a green run that
executed nothing is not a green run. The tests that only exercise failure paths
(validation, unreachable database) point at a dead port on purpose and need no container.

## Layout
```
src/Northgate.Api
  Contracts        response records
  Controllers      HTTP surface
  Services         business rules
  Data             repository interface and its SQL implementation
  Infrastructure   exception handling
tests/Northgate.Api.Tests              unit
tests/Northgate.Api.IntegrationTests   Testcontainers + WebApplicationFactory
db                                     schema and seed scripts, the source of truth
```
