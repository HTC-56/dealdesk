# Status

Repo scaffolded 2026-08-27. Nothing built yet. SPEC.md is the product;
DECISIONS.md locks the fence; ROADMAP.md is the scoreboard. The planning lane
authors Phase A from SPEC.md (Phase A must prove the toolchain — build,
test, format gates, one migration + Dapper round-trip + one property test —
before any domain feature is built; this is the executor's first C# repo).

Per-phase sections append below as phases ship.

## Phase A

The toolchain is proven. The .NET 8 SDK builds the solution warnings-clean. xUnit
runs 28 tests including one in-process WebApplicationFactory test. The migrator
applies `001_init.sql` and is idempotent. `Money` is property-tested across three
tests. `verify.sh` composes every gate (build, test, format, scrub) plus
README-quickstart path lint.

What is NOT built yet: every domain feature — worksheet, offer math, audit, recon,
reports, desk page.

## Phase B

Phase B is closed. The worksheet schema (002_worksheet.sql) adds recon_line,
comparable_sale, walk_item, and offer_input tables with CHECK constraints on
status, price parity, and category enums. VIN validation checks format,
checksum digit, and throws on I/O/Q characters. OfferMath computes trade-in
allowance, recon difference, target gross, and the full price derivation
(anchor → target gross → price → pack → net). The appraisal endpoints accept
POST (create draft, 201 + Location) and GET (list newest-first, optional
status filter). Test count is now 56.

What is still NOT built: the walk / recon / comp endpoints, the offer endpoint
that serves the derivation, the desk page. Lifecycle, audit, recon actuals and
the reports remain untouched.

## Phase C

Phase C is closed. The worksheet's child collections — walk, recon-line, and
comparable-sale — are all on the wire (GET + POST each). The offer endpoint
(`GET .../offer`) serves the recommended trade value together with its full
derivation array, and the store numbers upsert cleanly. Test count is now 76.

What is still NOT built: lifecycle and the audit trail, recon actuals and
variance, the three reports (look-to-book, recon variance, gross by appraiser),
the desk page, the ops surface beyond `/healthz`, seeded demo data, and deploy
packaging.

## Phase D

Phase D is closed. The lifecycle engine enforces the five-state chain
draft → appraised → presented → won | lost with `lost` reachable from any open
state, illegal moves refused as 409, and unknown status words rejected as 400.
Every value change appends to an append-only `audit_entry` table (one row per
field, values as text) inside the same transaction as the UPDATE. The trail is
served newest-first via `GET .../audit`. Field-level revisions (PATCH) cover
model year, make, model, trim, miles and appraiser; VIN and status are not
PATCHable. Test count is now 100.

What is still NOT built: recon actuals and variance, the three reports
(look-to-book, recon variance, gross by appraiser), the desk page, the ops
surface beyond `/healthz`, seeded demo data, and deploy packaging.

## Phase E

Phase E is closed. Recon actuals now post line-by-line against a recon estimate
line — many postings per line, a negative amount is a credit, zero is refused.
`GET .../recon-variance` serves `actual − estimate` per line, by category, and
for the worksheet, with `unpostedLines` flagging unfinished recon. Test count is
now 124.

What is still NOT built: the three reports (look-to-book, recon variance, gross
by appraiser), the desk page, the ops surface beyond `/healthz`, seeded demo
data, and deploy packaging.

## Phase F

Phase F is closed. Three SQL views (`report_look_to_book`,
`report_recon_variance`, `report_front_gross`) summarise across worksheets.
Three report routes — `GET /api/reports/look-to-book`,
`GET /api/reports/recon-variance`, `GET /api/reports/front-gross` — serve
store-level totals with per-appraiser or per-category breakdowns. Rates travel
as basis points; money as whole cents. Report routes take no id and never 404.
Test count is now 152.

What is still NOT built: the desk page, the ops surface beyond `/healthz`,
seeded demo data, and deploy packaging.

## Phase G

Phase G is closed. `/metrics` serves hand-rolled Prometheus text with request
counters (`http_requests_total` by method + status) and a `dealdesk_appraisals`
gauge showing count per status; every request appends a JSON line to the JSONL
ops ledger (which keeps the request path the metric labels omit), and a static
bearer token guards POST, PUT, PATCH and DELETE once `DEALDESK_Auth__Token` is
set, while reads stay open; an unset token leaves writes open so a fresh clone
still runs its quickstart. Test count is now 180.

What is still NOT built: the desk page, seeded demo data, and deploy packaging.

## Phase H

Phase H is closed. `dotnet run --project src/DealDesk -- seed` writes a demo month of twelve worksheets across three appraisers, covering all five lifecycle states, with recon lines in every category, postings including one credit, and six lines still unposted, so all three reports read real numbers on a fresh clone; the seed refuses to run into a database that already holds worksheets. Test count is now 208.

What is still NOT built: the desk page and deploy packaging.
