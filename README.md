# Northgate Transfers, sandbox API

A small ASP.NET Core API used for a paired technical session. Everything here is fictional and disposable.
Nothing built during the session is kept or shipped.

## Requirements

.NET 10 SDK. Check with `dotnet --version`.

A container runtime (Docker or Podman) is needed to run the API against a database and to run the
integration suite. The unit suite needs neither.

## Running

```
dotnet build
dotnet test
dotnet run --project src/Northgate.Api
```

The connection string lives in `src/Northgate.Api/appsettings.json` under
`ConnectionStrings:NorthgatePostgres` and points at a local PostgreSQL instance. The committed value
carries no password: the local one belongs in `src/Northgate.Api/appsettings.Development.json`, which is
gitignored precisely so credentials never reach the remote.

To start a database matching that connection string:

```
podman run -d --name capital-services-pg   -e POSTGRES_USER=capital -e POSTGRES_PASSWORD=<your-local-password>   -e POSTGRES_DB=capital_services -p 5460:5432 postgres:17-alpine
psql "$CONN" -f db/001_schema.sql -f db/002_seed_reference.sql -f db/003_seed_sample.sql
```

## What is already here

Two endpoints:

- `GET /api/customers/{customerId}` — a single customer.
- `GET /api/transactions/search?customerName=&page=&pageSize=` — paged search, with each amount
  converted to CHF. `pageSize` is capped at 200.

They show the conventions used in this codebase:

- The controller is thin. It calls a service and maps the result to a response.
- Business rules live in `CustomerService`.
- Data access goes through `ICustomerRepository` and `ITransactionRepository`. The EF Core
  implementations project straight to DTOs, so no entities are materialized.
- The schema is defined by `db/*.sql`, which is the source of truth. The EF model is mapped to match
  it and there are deliberately no migrations, so only one definition can drift.
- Money is `numeric` in the database and `decimal` in C#, never floating point.
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
  Data             repository ports, their EF Core adapters, entities and the DbContext
  Infrastructure   exception handling
tests/Northgate.Api.Tests              unit
tests/Northgate.Api.IntegrationTests   Testcontainers + WebApplicationFactory
db                                     schema and seed scripts, the source of truth
```
