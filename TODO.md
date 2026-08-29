# Loop tasks

Ordered; each is one short session. Work the first unchecked box. Each task is
fully specced in ONE greppable section of its phase doc (`TASK_PHASE_A.md` §A1,
§A2, …) — grep your section, read it, build it.

*(Phase A is open — see below.)*

## Phase A: prove the toolchain — see TASK_PHASE_A.md

Scaffold, migrator, Money and the first tests are already committed (`feat(A1)`,
`feat(A2)`); the tree is green. **Gate for every task: `dotnet build
-warnaserror`, `dotnet test`, `dotnet format --verify-no-changes`, and
`bash scripts/scrub-check.sh` once §A3 has created it.**

- [x] A3 — Create `scripts/scrub-check.sh`: scan `git ls-files` output for home
  paths, non-documentation IPs, `.local`/`.internal` hosts and key material;
  exit 1 listing offences. It must exclude itself. Spec: §A3.
- [x] A4 — Create `README.md`: what dealdesk is, requirements, quickstart
  (build, test, run, curl `/healthz` on localhost), layout, gates. Only
  `/healthz` exists — promise nothing else. Spec: §A4.
- [x] A5 — Create `tests/DealDesk.Tests/MoneyArithmeticTests.cs`: six more
  Money laws (subtraction undoes addition, double negation, Zero identity,
  comparisons, sub-cent rejection, empty Sum). Mirror
  `MoneyPropertyTests.cs`. Spec: §A5.
- [x] A6 — Create `tests/DealDesk.Tests/MoneyStorageTests.cs`: Money survives a
  Dapper round-trip as INTEGER cents, negatives included. Mirror
  `AppraisalRoundTripTests.cs`; scratch table in the test, no new migration.
  Spec: §A6.
- [x] A7 — Create `tests/DealDesk.Tests/AppraisalConstraintTests.cs`: the four
  CHECK constraints in `sql/001_init.sql` reject bad status, negative miles, a
  16-char VIN and year 1800; all five statuses insert. Mirror
  `AppraisalRoundTripTests.cs`. Spec: §A7.
- [x] A8 — Create `verify.sh` (all four gates + README-path lint), append a
  `## Phase A` section to STATUS.md, and flip the ROADMAP row 1 to SHIPPED.
  Closes Phase A. Gate: `bash verify.sh`. Spec: §A8.

## Phase B: the worksheet and offer math — see TASK_PHASE_B.md

Phase A above is CLOSED and kept only as a style reference. Phase B is the open
one. `Vin.cs`, `sql/002_worksheet.sql`, `OfferMath.cs` and the `Api/` layer are
already committed (`feat(B1)`–`feat(B4)`); the tree is green at 28 tests.
**Gate for every task: `dotnet build -warnaserror`, `dotnet test`, `dotnet
format --verify-no-changes`, `bash scripts/scrub-check.sh`.** Demo VINs:
`ZZ9ZZ99Z2Z9000042` is valid, `ZZ9ZZ99Z9Z9000042` is not. Use seeds 2001+.

- [x] B5 — Create `tests/DealDesk.Tests/VinTests.cs`: six VIN facts (the
  valid/invalid pair, lowercase, bad lengths + null, I/O/Q, CheckDigit and
  WithCheckDigit, Normalize throws). Mirror `MoneyArithmeticTests.cs`.
  Spec: §B5.
- [x] B6 — Create `tests/DealDesk.Tests/OfferMathTests.cs`: six laws — the
  derivation sums exactly, running totals, ≤ anchor − recon, four lines, anchor
  rounding, refusals. Mirror `MoneyPropertyTests.cs`; wrap `Gen` values in
  `Math.Abs`. Spec: §B6.
- [x] B7 — Create `tests/DealDesk.Tests/WorksheetSchemaTests.cs`: the
  `sql/002_worksheet.sql` CHECK constraints on recon_line, comp, walk_item and
  offer_input, plus cascade delete. Mirror `AppraisalConstraintTests.cs`.
  Spec: §B7.
- [x] B8 — Add `GET /api/appraisals` (every row, newest first, optional
  `status` filter) to `src/DealDesk/Api/AppraisalEndpoints.cs`, mirroring the
  `{id:long}` handler right above it. Spec: §B8.
- [x] B9 — Add `POST /api/appraisals` to
  `src/DealDesk/Api/AppraisalEndpoints.cs`: `Validate()` → 400, else insert a
  draft row with `RETURNING id` → 201 + Location. Spec: §B9.
- [x] B10 — Create `tests/DealDesk.Tests/AppraisalApiTests.cs`: six HTTP
  assertions over POST and GET (201, round-trip, two 400s, the list, 404).
  Mirror `HealthzTests.cs`; served JSON is camelCase. Spec: §B10.
- [x] B11 — Run `bash verify.sh`, append a `## Phase B` section to STATUS.md,
  and flip ROADMAP row 2 to PARTIAL / phase B. Closes Phase B. Gate:
  `bash verify.sh`. Spec: §B11.

## Phase C: child collections and the priced offer — see TASK_PHASE_C.md

Phases A and B above are CLOSED and kept only as style references. Phase C is
CLOSED too. `WorksheetDtos.cs`, `WorksheetEndpoints.cs` (the walk-item
pair) and `OfferEndpoints.cs` are already committed (`feat(C1)`–`feat(C3)`);
the tree is green at 64 tests. **Gate for every task: `dotnet build
-warnaserror`, `dotnet test`, `dotnet format --verify-no-changes`, `bash
scripts/scrub-check.sh`.** Money on the wire is whole cents; JSON is camelCase.

- [x] C4 — Add the recon-line pair (GET + POST
  `/api/appraisals/{id}/recon-lines`) to
  `src/DealDesk/Api/WorksheetEndpoints.cs`, mirroring the walk-item pair
  directly above it. Spec: §C4.
- [x] C5 — Add the comp pair (GET + POST `/api/appraisals/{id}/comps`) to
  `src/DealDesk/Api/WorksheetEndpoints.cs`, mirroring the recon-line pair you
  just wrote. Spec: §C5.
- [x] C6 — Create `tests/DealDesk.Tests/WorksheetApiTests.cs`: six HTTP
  assertions over recon-lines and comps (201s, two 400s, the list, a 404).
  Mirror `WalkItemApiTests.cs`. Spec: §C6.
- [x] C7 — Create `tests/DealDesk.Tests/OfferApiTests.cs`: six HTTP assertions
  over `/offer-inputs` and `/offer` that `OfferSmokeTests.cs` does not already
  cover. Mirror `OfferSmokeTests.cs`. Spec: §C7.
- [x] C8 — Edit `README.md`: add one `## The API` section listing every route
  that now exists, the cents rule, the derivation rule, and one curl example.
  Promise nothing unbuilt. Gate: `bash verify.sh`. Spec: §C8.
- [x] C9 — Run `bash verify.sh`, append a `## Phase C` section to STATUS.md,
  and flip ROADMAP row 2 to SHIPPED / phase B–C. Closes Phase C. Gate:
  `bash verify.sh`. Spec: §C9.

## Phase D: lifecycle and the audit trail — see TASK_PHASE_D.md

Phases A–C above are CLOSED and kept only as style references. Phase D is
CLOSED too. `sql/003_audit.sql`, `Domain/Lifecycle.cs`, `Api/AuditDtos.cs` and
`Api/LifecycleEndpoints.cs` are already committed (`feat(D1)`–`feat(D3)`); the
tree is green at 76 tests. **Gate for every task: `dotnet build -warnaserror`,
`dotnet test`, `dotnet format --verify-no-changes`, `bash
scripts/scrub-check.sh`.** Lifecycle: draft → appraised → presented → won |
lost, with `lost` reachable from any open state. Unknown status word = 400;
illegal move = 409. Every write needs `changedBy` and `reason`.

- [x] D4 — Add `GET /api/appraisals/{id}/audit` to
  `src/DealDesk/Api/LifecycleEndpoints.cs`, newest first (`ORDER BY id DESC`),
  mirroring the walk-item `MapGet` in `WorksheetEndpoints.cs`. The
  `AuditColumns` const already exists. Spec: §D4.
- [x] D5 — Create `tests/DealDesk.Tests/LifecycleTests.cs`: six rule assertions
  over `Domain/Lifecycle.cs` (forward chain, no skipping, lost from anywhere,
  terminals, case-insensitivity, Refuse). Mirror `VinTests.cs`. Spec: §D5.
- [x] D6 — Create `tests/DealDesk.Tests/AuditSchemaTests.cs`: six assertions
  over `sql/003_audit.sql` — good row inserts, three CHECKs reject, UPDATE and
  DELETE abort, parent delete blocked. Mirror `WorksheetSchemaTests.cs`.
  Spec: §D6.
- [x] D7 — Create `tests/DealDesk.Tests/LifecycleApiTests.cs`: six HTTP
  assertions over `POST .../status` and `GET .../audit` (200, trail entry, 409,
  two 400s, full walk, 404s). Mirror `WalkItemApiTests.cs`. Spec: §D7.
- [x] D8 — Create `tests/DealDesk.Tests/RevisionApiTests.cs`: six HTTP
  assertions over `PATCH /api/appraisals/{id}` (200, one trail entry, two
  entries, no-op writes none, two 400s, 404). Mirror `LifecycleApiTests.cs`.
  Spec: §D8.
- [x] D9 — Edit `README.md`: add the three new routes to `## The API`, one
  audit paragraph, and change the healthz sample's schema to `003_audit`.
  Promise nothing unbuilt. Gate: `bash verify.sh`. Spec: §D9.
- [x] D10 — Run `bash verify.sh`, append a `## Phase D` section to STATUS.md,
  and flip ROADMAP row 3 to SHIPPED / phase D. Closes Phase D. Gate:
  `bash verify.sh`. Spec: §D10.

## Phase E: recon actuals and variance — see TASK_PHASE_E.md

Phases A–D above are CLOSED and kept only as style references. **Phase E is the
open one.** `sql/004_recon_actual.sql`, `Domain/ReconVariance.cs`,
`Api/ReconDtos.cs` and `Api/ReconEndpoints.cs` are already committed
(`feat(E1)`–`feat(E4)`); the tree is green at 100 tests. **Gate for every task:
`dotnet build -warnaserror`, `dotnet test`, `dotnet format
--verify-no-changes`, `bash scripts/scrub-check.sh`.** Variance is
`actual − estimate`, so positive means the line ran over. A posting hangs off a
recon LINE and its amount may be negative (a credit) but never zero.

- [x] E5 — Add the actuals pair (GET + POST
  `/api/appraisals/{id}/recon-lines/{lineId}/actuals`) to
  `src/DealDesk/Api/ReconEndpoints.cs`, mirroring the recon-line pair in
  `WorksheetEndpoints.cs`. `ReconLineBelongs` already exists. Spec: §E5.
- [x] E6 — Create `tests/DealDesk.Tests/ReconActualSchemaTests.cs`: six
  assertions over `sql/004_recon_actual.sql` — good row inserts, three CHECKs
  reject, a credit is allowed, postings cascade with their line. Mirror
  `AuditSchemaTests.cs`. Spec: §E6.
- [x] E7 — Create `tests/DealDesk.Tests/ReconVarianceTests.cs`: six arithmetic
  laws over `Domain/ReconVariance.cs` (line totals, credits, unposted lines,
  worksheet totals, category rollup, refusals). Mirror `LifecycleTests.cs`;
  no database. Spec: §E7.
- [x] E8 — Create `tests/DealDesk.Tests/ReconActualApiTests.cs`: six HTTP
  assertions over the actuals pair (201, list order, two 400s, a credit, two
  404s). Mirror `WalkItemApiTests.cs`. Spec: §E8.
- [x] E9 — Create `tests/DealDesk.Tests/ReconVarianceApiTests.cs`: six HTTP
  assertions over `GET .../recon-variance` (empty worksheet, unposted lines,
  a posted line, totals, byCategory, 404). Mirror `ReconActualApiTests.cs`.
  Spec: §E9.
- [x] E10 — Edit `README.md`: add the three new routes to `## The API`, one
  recon-variance paragraph, and change the healthz sample's schema to
  `004_recon_actual`. Promise nothing unbuilt. Gate: `bash verify.sh`.
  Spec: §E10.
- [x] E11 — Run `bash verify.sh`, append a `## Phase E` section to STATUS.md,
  and flip ROADMAP row 4 to SHIPPED / phase E. Closes Phase E. Gate:
  `bash verify.sh`. Spec: §E11.

## Phase F: the three reports — see TASK_PHASE_F.md

Phases A–E above are CLOSED and kept only as style references. **Phase F is the
open one.** `sql/005_reports.sql`, `Api/ReportDtos.cs`, `Api/ReportEndpoints.cs`
(two of the three routes) and `tests/ReportSmokeTests.cs` are already committed
(`feat(F1)`–`feat(F4)`); the tree is green at 128 tests. **Gate for every task:
`dotnet build -warnaserror`, `dotnet test`, `dotnet format
--verify-no-changes`, `bash scripts/scrub-check.sh`.** Report routes are
`GET /api/reports/...`, take no id and never 404 — an empty store is an empty
report. Rates travel as basis points (`2500` is 25.00%). Reuse
`ReportSmokeTests.CreateAsync(client, appraiser)` and
`ReportSmokeTests.MoveAsync(client, id, statuses…)`.

- [x] F5 — Add `GET /api/reports/front-gross` to
  `src/DealDesk/Api/ReportEndpoints.cs`, mirroring the `recon-variance` route
  directly above it. `FrontGrossColumns` and `FrontGrossReport` already exist.
  Spec: §F5.
- [x] F6 — Create `tests/DealDesk.Tests/ReportRateTests.cs`: six laws over
  `Reports.RateBps` and the computed `BookRateBps` (basis points, truncation,
  a whole of zero). Mirror `LifecycleTests.cs`; no database. Spec: §F6.
- [x] F7 — Create `tests/DealDesk.Tests/LookToBookApiTests.cs`: six HTTP
  assertions over `GET /api/reports/look-to-book` (two appraisers, name order,
  per-row counts, rates). Mirror `ReportSmokeTests.cs`. Spec: §F7.
- [x] F8 — Create `tests/DealDesk.Tests/ReconVarianceReportApiTests.cs`: six
  HTTP assertions over `GET /api/reports/recon-variance` (empty, unposted,
  over, summed postings, a credit, category order). Mirror
  `LookToBookApiTests.cs`. Spec: §F8.
- [x] F9 — Create `tests/DealDesk.Tests/FrontGrossApiTests.cs`: six HTTP
  assertions over `GET /api/reports/front-gross` (won only, plan intact,
  overage, under, unposted, two appraisers). Mirror
  `ReconVarianceReportApiTests.cs`. Spec: §F9.
- [x] F10 — Edit `README.md`: add the three report routes to `## The API`, one
  reports paragraph, and change the healthz sample's schema to `005_reports`.
  Promise nothing unbuilt. Gate: `bash verify.sh`. Spec: §F10.
- [x] F11 — Run `bash verify.sh`, append a `## Phase F` section to STATUS.md,
  and flip ROADMAP row 5 to SHIPPED / phase F. Closes Phase F. Gate:
  `bash verify.sh`. Spec: §F11.

## Phase G: the ops surface — see TASK_PHASE_G.md

Phases A–F above are CLOSED and kept only as style references. **Phase G is the
open one.** `Ops/OpsMetrics.cs`, `Ops/OpsLedger.cs`, `Ops/OpsRecording.cs`,
`Ops/BearerAuth.cs`, `Api/OpsEndpoints.cs` and `tests/OpsSmokeTests.cs` are
already committed (`feat(G1)`–`feat(G4)`); the tree is green at 156 tests.
**Gate for every task: `dotnet build -warnaserror`, `dotnet test`, `dotnet
format --verify-no-changes`, `bash scripts/scrub-check.sh`.** You add no source
code this phase — every type is finished. Metrics label method and status,
never the path. Reads are never guarded. Both observers record AFTER the
response reaches the caller, so use `OpsSmokeTests.ScrapeUntilAsync` and
`OpsSmokeTests.LedgerLinesAsync` rather than reading once.

- [x] G5 — Create `tests/DealDesk.Tests/OpsMetricsTests.cs`: six laws over
  `OpsMetrics` (empty family, separate totals, method case, status series,
  ordering, Gauge + Escape). Mirror `ReportRateTests.cs`; no database.
  Spec: §G5.
- [x] G6 — Create `tests/DealDesk.Tests/OpsLedgerTests.cs`: six facts over
  `OpsLedger` (disabled, appends, Line fields, UTC ts, a quoted path, parent
  directory). Mirror `ReportRateTests.cs`; own temp dir per fact. Spec: §G6.
- [x] G7 — Create `tests/DealDesk.Tests/MetricsApiTests.cs`: six HTTP
  assertions over `GET /metrics` (content type, a 404 counted, a POST counted,
  the gauge moving, the duration family, no token needed). Mirror
  `OpsSmokeTests.cs`. Spec: §G7.
- [x] G8 — Create `tests/DealDesk.Tests/BearerAuthApiTests.cs`: six HTTP
  assertions over the token (401 without, 401 wrong, PUT guarded, reads open,
  near miss, unset token open). Mirror `OpsSmokeTests.cs`. Spec: §G8.
- [x] G9 — Edit `README.md`: add `GET /metrics` to `## The API` and one ops
  paragraph. The healthz sample stays `005_reports` — no migration this phase.
  Gate: `bash verify.sh`. Spec: §G9.
- [x] G10 — Run `bash verify.sh`, append a `## Phase G` section to STATUS.md,
  and flip ROADMAP row 7 to SHIPPED / phase A, G. Closes Phase G. Gate:
  `bash verify.sh`. Spec: §G10.

## Phase H: seeded demo data — see TASK_PHASE_H.md

Phases A–G above are CLOSED and kept only as style references. **Phase H is the
open one.** `sql/seed.sql`, `Data/Seeder.cs`, the `seed` verb in `Program.cs`
and `tests/SeedSmokeTests.cs` are already committed (`feat(H1)`–`feat(H3)`);
the tree is green at 184 tests. **Gate for every task: `dotnet build
-warnaserror`, `dotnet test`, `dotnet format --verify-no-changes`, `bash
scripts/scrub-check.sh`.** You add no `src/` code this phase. The demo month is
twelve worksheets (ids 1..12) across three appraisers — 4 won, 2 lost, 2
presented, 2 appraised, 2 draft — with 15 recon lines, 6 of them unposted.
Every number a task asserts is listed in TASK_PHASE_H.md's "The demo month, in
numbers". Reuse `SeedSmokeTests.SeededDb()`, `SeedSmokeTests.SeededClient()`
and `SeedSmokeTests.ReadAsync()`.

- [x] H4 — Create `tests/DealDesk.Tests/SeedDataTests.cs`: six facts over the
  seeded database (contiguous ids, two walk items each, the comp-less
  worksheet, the one credit, the trail, recent timestamps). Mirror
  `AuditSchemaTests.cs`; no HTTP. Spec: §H4.
- [x] H5 — Create `tests/DealDesk.Tests/SeedWorksheetApiTests.cs`: six HTTP
  assertions over a seeded worksheet (list order, status filter, worksheet 1's
  fields and children, offer inputs, the audit trail). Mirror
  `WalkItemApiTests.cs`. Spec: §H5.
- [x] H6 — Create `tests/DealDesk.Tests/SeedReportApiTests.cs`: six HTTP
  assertions over the per-appraiser ROWS of look-to-book and front-gross (name
  order, three rate rows, both signs of recon variance). Mirror
  `LookToBookApiTests.cs`. Spec: §H6.
- [x] H7 — Create `tests/DealDesk.Tests/SeedVarianceApiTests.cs`: six HTTP
  assertions over `GET .../recon-variance` on seeded worksheets (totals, the
  category order, the credit, unposted lines, an empty worksheet). Mirror
  `ReconVarianceApiTests.cs`. Spec: §H7.
- [x] H8 — Edit `README.md`: add the seed command to the quickstart block and
  one demo-data paragraph after the ops paragraph. The healthz sample stays
  `005_reports` — no migration this phase. Gate: `bash verify.sh`. Spec: §H8.
- [x] H9 — Run `bash verify.sh`, append a `## Phase H` section to STATUS.md,
  and flip ROADMAP row 8 to SHIPPED / phase H. Closes Phase H. Gate:
  `bash verify.sh`. Spec: §H9.

## Phase I: the desk page — see TASK_PHASE_I.md

Phases A–H above are CLOSED and kept only as style references. **Phase I is the
open one.** `src/DealDesk/Page/index.html`, `Api/PageEndpoints.cs`, the csproj
embed and `tests/DeskPageSmokeTests.cs` are already committed
(`feat(I1)`–`feat(I3)`); the tree is green at 212 tests. **Gate for every task:
`dotnet build -warnaserror`, `dotnet test`, `dotnet format
--verify-no-changes`, `bash scripts/scrub-check.sh`.** You add no `src/` code
this phase and you never edit `index.html`. The page is served at `/` as
`text/html`; its four panels are `<section id="…">` elements named
`appraisals`, `worksheet`, `audit`, `reports`. Reuse
`DeskPageSmokeTests.PageAsync()`, `.PanelIds` and `.ReadRoutes`.

- [x] I4 — Create `tests/DealDesk.Tests/DeskPageApiTests.cs`: six facts about
  the root route (content type, two identical GETs, three 404s, open under the
  token, counted in `/metrics`, not truncated). Mirror `MetricsApiTests.cs`.
  Spec: §I4.
- [x] I5 — Create `tests/DealDesk.Tests/DeskPageSelfContainedTests.cs`: six
  `DoesNotContain` facts pinning zero external requests (no `://`, no script
  src, no link/@import, no font/img, one inline block of each, inline
  behaviour). Mirror `DeskPageSmokeTests.cs`. Spec: §I5.
- [x] I6 — Create `tests/DealDesk.Tests/DeskPagePanelTests.cs`: six facts that
  each panel carries what SPEC feature 6 says (panel order, five statuses,
  eight recon categories, the derivation, the trail columns, three report
  tables). Mirror `DeskPageSmokeTests.cs`. Spec: §I6.
- [x] I7 — Create `tests/DealDesk.Tests/DeskPageRouteTests.cs`: six facts
  pairing page to API (exactly two `/api/` prefixes, the route literals, three
  writes that land, one read-back). Mirror `DeskPageSmokeTests.cs`. Spec: §I7.
- [ ] I8 — Edit `README.md`: add `GET /` as the first `## The API` bullet, one
  browser sentence in the quickstart, and one desk-page paragraph. The healthz
  sample stays `005_reports`. Gate: `bash verify.sh`. Spec: §I8.
- [ ] I9 — Run `bash verify.sh`, append a `## Phase I` section to STATUS.md,
  and flip ROADMAP row 6 to SHIPPED / phase I. Closes Phase I. Gate:
  `bash verify.sh`. Spec: §I9.
