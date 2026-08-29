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
# {"status":"ok","schema":"003_audit"}
```

## The API

- `GET /healthz` — health check, returns schema version
- `POST /api/appraisals` — create a new appraisal in draft status
- `GET /api/appraisals` — list all appraisals, newest first; optional `?status=` filter
- `GET /api/appraisals/{id}` — retrieve one appraisal by id
- `GET /api/appraisals/{id}/walk-items` — condition walk lines
- `POST /api/appraisals/{id}/walk-items` — add a walk line (area must be in vocabulary)
- `GET /api/appraisals/{id}/recon-lines` — itemized recon estimate lines
- `POST /api/appraisals/{id}/recon-lines` — add a recon line (category must be in vocabulary)
- `GET /api/appraisals/{id}/comps` — hand-entered comparable sales
- `POST /api/appraisals/{id}/comps` — add a comparable sale
- `GET /api/appraisals/{id}/offer-inputs` — store numbers (pack, targetGross) plus optional anchorOverride
- `PUT /api/appraisals/{id}/offer-inputs` — upsert the store numbers
- `GET /api/appraisals/{id}/offer` — recommended trade value with its derivation
- `POST /api/appraisals/{id}/status` — move the worksheet through its lifecycle
- `PATCH /api/appraisals/{id}` — revise worksheet fields, audited
- `GET /api/appraisals/{id}/audit` — the change trail, newest first

**Money is whole cents.** Every money field on the wire — `estimate`, `price`,
`pack`, `targetGross`, `anchorOverride`, `recommended` — is an integer number
of cents. `1550000` is $15,500.00.

**The offer carries its derivation.** `GET .../offer` returns `recommended`
together with a `derivation` array of `{label, amount, runningTotal}`; the
amounts sum to `recommended`. A worksheet with no comps and no anchor
override returns 409.

**Every change is audited.** `POST .../status` and `PATCH` require `changedBy`
and `reason`, and append one `audit_entry` row per changed field recording who,
when, old and new. The table is append-only — UPDATE and DELETE against it are
refused by the database. The lifecycle is draft → appraised → presented → won |
lost; `lost` is reachable from any open state, and a move the lifecycle refuses
is a 409.

```bash
curl http://localhost:5000/api/appraisals/1/offer
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
