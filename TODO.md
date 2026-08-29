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

Phases A and B above are CLOSED and kept only as style references. **Phase C is
the open one.** `WorksheetDtos.cs`, `WorksheetEndpoints.cs` (the walk-item
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
- [ ] C6 — Create `tests/DealDesk.Tests/WorksheetApiTests.cs`: six HTTP
  assertions over recon-lines and comps (201s, two 400s, the list, a 404).
  Mirror `WalkItemApiTests.cs`. Spec: §C6.
- [ ] C7 — Create `tests/DealDesk.Tests/OfferApiTests.cs`: six HTTP assertions
  over `/offer-inputs` and `/offer` that `OfferSmokeTests.cs` does not already
  cover. Mirror `OfferSmokeTests.cs`. Spec: §C7.
- [x] C8 — Edit `README.md`: add one `## The API` section listing every route
  that now exists, the cents rule, the derivation rule, and one curl example.
  Promise nothing unbuilt. Gate: `bash verify.sh`. Spec: §C8.
- [ ] C9 — Run `bash verify.sh`, append a `## Phase C` section to STATUS.md,
  and flip ROADMAP row 2 to SHIPPED / phase B–C. Closes Phase C. Gate:
  `bash verify.sh`. Spec: §C9.
