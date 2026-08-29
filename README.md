# dealdesk

Every used-car department runs the same arithmetic on every trade: what the
vehicle needs, what the market says, what the store must make, therefore
what to put on the worksheet. dealdesk is that appraisal desk as a small,
correct .NET 8 service — every number's derivation shown, every value change
audited, and the three reports a used-car director actually watches. Domain
math is generic, public-domain retail-automotive arithmetic. Built
end-to-end by an autonomous local-model coding loop; the commit history is
part of the deliverable.

## Requirements

- .NET 8 SDK

No database server — SQLite is a file.

## Quickstart

```bash
dotnet build
dotnet test
dotnet run --project src/DealDesk
```

The service answers on `http://localhost:5000/healthz`:

```bash
curl http://localhost:5000/healthz
# {"status":"ok","schema":"002_worksheet"}
```

## Layout

- `src/DealDesk/` — .NET 8 minimal API project (Program.cs, domain models, Dapper data layer)
- `sql/` — numbered, append-only migration scripts
- `tests/DealDesk.Tests/` — xUnit tests
- `scripts/` — automation (scrub-check.sh)

## Gates

Every task must pass all four gates before committing:

```bash
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```
