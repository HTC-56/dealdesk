# dealdesk — v1 spec

Every used-car department runs the same arithmetic on every trade: what the
vehicle needs, what the market says, what the store must make, therefore
what to put on the worksheet. dealdesk is that appraisal desk as a small,
correct .NET 8 service — every number's derivation shown, every value change
audited, and the three reports a used-car director actually watches. Domain
math is generic, public-domain retail-automotive arithmetic. Built
end-to-end by an autonomous local-model coding loop; the commit history is
part of the deliverable (see `docs/PROCESS.md` when it lands).

## v1 features (all of these, nothing more)

1. **.NET 8 minimal API on SQLite via Dapper.** Plain SQL, no EF — the SQL
   is part of the work sample. Numbered migration scripts applied by a tiny
   in-repo migrator.
2. **The appraisal worksheet.** Vehicle basics (year/make/model/trim/miles;
   VIN validated by the public check-digit algorithm, never decoded via any
   external service), condition walk as line items, itemized recon estimate,
   hand-entered market comps as the anchor, and offer math: market anchor −
   recon − pack − target front gross ⇒ recommended trade value. The
   derivation is returned with the number — no magic totals.
3. **Worksheet lifecycle + audit.** draft → appraised → presented → won |
   lost. Every value change records who, when, old, new, and a reason note
   in an append-only audit table; the API serves the trail with the
   worksheet.
4. **Recon actuals.** After acquisition, actual recon costs post against
   the estimate line-by-line; variance is first-class data, not a report
   afterthought.
5. **The three reports** (SQL views + endpoints): look-to-book (appraised
   vs won), recon variance (estimate vs actual, by line category), front
   gross by appraiser.
6. **The desk page.** `GET /` — one self-contained HTML page (inline
   CSS/JS, no framework, no build step, no CDN, no web fonts): worksheet
   form with live offer math, appraisal list with lifecycle states, the
   audit trail, the three reports. The README hero screenshot.
7. **Ops surface.** `/healthz`, `/metrics` (hand-rolled Prometheus text —
   no metrics package), JSONL ops ledger, static bearer token on write
   endpoints.
8. **Seeded demo data.** A seed script writes a plausible month of
   appraisals (won, lost, in-flight, recon variances) so the page and the
   reports breathe on first run.
9. **Deploy-grade packaging.** `dotnet publish` single-file self-contained
   binary; example systemd unit; GitHub Actions CI (`dotnet build` +
   `dotnet test`); README quickstart (seed + appraise a vehicle + read the
   audit trail in 5 minutes); `docs/PROCESS.md`.

## Pre-registered rules

- Dependencies named in full: Dapper, Microsoft.Data.Sqlite, xUnit,
  Microsoft.AspNetCore.Mvc.Testing (integration tests). A task that adds
  anything else must name it and why.
- Domain math is generic industry arithmetic from public vocabulary; the
  offer-math module carries xUnit property tests for its invariants (e.g.,
  recommended value never exceeds anchor − recon; derivations sum exactly).
- First C# repo for the build loop: Phase A proves — SDK present, solution
  builds, one migration + one Dapper round-trip + one property test green,
  `dotnet format --verify-no-changes` wired — before any domain feature is
  built. If the executor fails structurally at C#, that is recorded in
  DECISIONS.md, the lane stands down, and PROCESS.md reports it honestly.
- Integration tests run in-process via WebApplicationFactory on a temp
  SQLite file; CI needs nothing but the SDK.

## Non-goals (v1 refuses these)

- No external VIN decode, no market-data feeds, no scraping, no book values
  pulled from anywhere — comps are hand-entered by design.
- No DMS/CRM integration, no desking beyond the trade worksheet (no
  payments, no F&I, no lender math).
- No multi-store, no users/roles beyond the bearer token + appraiser name
  field, no EF, no Blazor/React/SPA, no SignalR.
- No AI anywhere in this repo — this one proves the stack and the domain,
  nothing else.

## Stack & shape

- Layout: `src/DealDesk/` (Api, Domain, Data, Page), `sql/` (numbered
  migrations, seed), `tests/DealDesk.Tests/` (unit + property +
  integration), `deploy/` (unit file, config example), `scripts/`
  (scrub-check.sh), `docs/PROCESS.md`.

## Gates

- `dotnet build -warnaserror` + `dotnet test` +
  `dotnet format --verify-no-changes` green at every phase end.
- `bash scripts/scrub-check.sh` green from phase 1: greps the tree for
  private hostnames, non-documentation IPs, absolute home paths, and key
  material. Docs use `localhost` and `192.0.2.x` only.
- `verify.sh` = all of the above + README-quickstart lint (commands shown
  in the README must exist in the repo).

## Done means

A stranger with the .NET 8 SDK: `dotnet test` green on a clean clone. The
README quickstart seeds the demo month, appraises a vehicle end-to-end
(walk, recon, comps, offer with visible derivation), changes a value and
reads it in the audit trail, and opens the three reports. The desk page
breathes. Single-file publish runs under the example unit. CI badge green.
PROCESS.md tells the story in one page.
