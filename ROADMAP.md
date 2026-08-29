# Roadmap — the v1 scoreboard

One row per SPEC.md feature. The planning lane keeps status current; row edits here
are the one permitted exception to append-only docs.

| # | Feature (SPEC.md) | Status | Phase | Note |
|---|---|---|---|---|
| 1 | .NET 8 minimal API + SQLite/Dapper + migrator | SHIPPED | A | scaffold, migrator, Money, 001_init, Phase A gates |
| 2 | Appraisal worksheet + offer math with visible derivation | SHIPPED | B–C | worksheet, child collections and the offer endpoint with its visible derivation |
| 3 | Lifecycle + append-only audit trail | SHIPPED | D | lifecycle rules, audited status moves and field revisions, append-only trail served newest-first |
| 4 | Recon actuals + variance | NOT BUILT | — | |
| 5 | The three reports (look-to-book, recon variance, gross by appraiser) | NOT BUILT | — | |
| 6 | The desk page (self-contained) | NOT BUILT | — | hero screenshot |
| 7 | Ops surface (/healthz, /metrics, ledger, bearer auth) | PARTIAL | A | /healthz only; /metrics, ledger, bearer auth still open |
| 8 | Seeded demo data | NOT BUILT | — | |
| 9 | Deploy-grade packaging (single-file publish, unit file, CI, quickstart) | PARTIAL | A | README quickstart in A4; publish, unit file, CI still open |
| — | docs/PROCESS.md (the loop story) | NOT BUILT | — | written near the end, when there is a ledger to excerpt |

When every row reads SHIPPED and verify.sh is green, the project is done — the
planning lane declares PROJECT SPEC COMPLETE rather than inventing scope.

## Reservations ledger — small deferred calls recorded inside phase specs

- **Test-runner packages beyond the pre-registered four** (Phase A,
  `tests/DealDesk.Tests/DealDesk.Tests.csproj`). SPEC.md names xUnit; actually
  executing `dotnet test` also needs `Microsoft.NET.Test.Sdk` and
  `xunit.runner.visualstudio`. Taken as the standard xUnit runner pair rather
  than new dependency surface — they ship no runtime code into `src/`.
- **No property-testing package** (Phase A, `tests/DealDesk.Tests/Gen.cs`).
  Properties are xUnit facts looping seeded cases from `Gen` instead of adding
  FsCheck, keeping the dependency surface exactly as pre-registered. Revisit
  only if the offer-math invariants outgrow it.
- **Migrations are embedded resources** (Phase A, `src/DealDesk/DealDesk.csproj`).
  `sql/*.sql` is compiled into the assembly so the single-file publish in
  feature 9 carries its own schema with no sidecar files.
- **Dapper snake_case matching is global** (Phase A, `src/DealDesk/Data/Db.cs`).
  `DefaultTypeMap.MatchNamesWithUnderscores = true`, so queries select real
  column names and later phases write no `AS` aliases.
- **The anchor is the comp arithmetic mean** (Phase B,
  `src/DealDesk/Domain/OfferMath.cs`). SPEC.md names hand-entered comps as
  "the anchor" without naming the reduction. Taken as their mean to the
  nearest cent, halves away from zero — the least surprising reading — with a
  per-worksheet `anchor_override` column so the desk can type a different
  number instead. Revisit if a playtest says the desk wants a median or a
  low-comp rule.
- **Money columns carry no `_cents` suffix** (Phase B,
  `sql/002_worksheet.sql`). `estimate`, `price`, `pack`, `target_gross`,
  `anchor_override` are INTEGER cents; the bare names let snake_case matching
  land them directly on `Money` properties, keeping the no-`AS`-alias rule
  above.
- **The recon category vocabulary is fixed in the schema** (Phase B,
  `sql/002_worksheet.sql`). A CHECK constraint, not free text, because
  feature 5's recon-variance report groups by category and free text would
  make that report meaningless.
- **Phase B write endpoints are unauthenticated** (Phase B,
  `src/DealDesk/Api/AppraisalEndpoints.cs`). The static bearer token on writes
  is feature 7; `POST /api/appraisals` is open until that ships, and no other
  phase may treat that as precedent.
- **API money fields are integer cents with no suffix** (Phase C,
  `src/DealDesk/Api/WorksheetDtos.cs`). The wire carries `estimate`, `price`,
  `pack`, `targetGross`, `anchorOverride`, `recommended` as whole cents, not
  dollars: a JSON number is a double in a browser, and `1234.56` would not
  survive the round trip the offer math exists to make exact. The API records
  use `long` rather than `Money` for these — cents-at-the-boundary means no
  JSON converter and no `Money`-shaped `{"cents":…}` objects in the payload.
- **An unpriceable worksheet is 409, not 400** (Phase C,
  `src/DealDesk/Api/OfferEndpoints.cs`). `GET .../offer` on an appraisal with
  neither a comp nor an anchor override returns 409 Conflict: the request is
  well-formed, the worksheet is simply not ready to price. Every other refusal
  in the repo is a 400 from a `Validate()` message.
- **Offer inputs upsert, and read as zeros when absent** (Phase C,
  `src/DealDesk/Api/OfferEndpoints.cs`). `offer_input` is one row per
  appraisal, so the write is `PUT` with `ON CONFLICT … DO UPDATE` rather than a
  `POST` that could duplicate. A worksheet the desk has never priced reads back
  pack 0 / target 0 rather than 404 — the appraisal exists, its store numbers
  are just untyped.
- **`lost` is reachable from every open state** (Phase D,
  `src/DealDesk/Domain/Lifecycle.cs`). SPEC.md draws the chain
  draft → appraised → presented → won | lost without saying where `lost`
  branches from. Taken as: any open state may go to `lost` (a deal dies
  whenever the customer walks), but `won` only follows `presented` — nobody
  buys a vehicle they were never shown an offer on. Both are terminal and a
  move to the state already held is refused, since it would write no audit row.
- **The audit table has no cascade, and cannot be rewritten** (Phase D,
  `sql/003_audit.sql`). Two BEFORE triggers abort UPDATE and DELETE, so the FK
  to `appraisal` deliberately omits `ON DELETE CASCADE` — deleting an appraisal
  that has a trail fails rather than taking the evidence with it. Every other
  child table in the repo cascades. dealdesk exposes no delete route, so no
  endpoint changes behaviour; revisit only if feature 9 ever needs a purge.
- **The trail records one row per field, valued as text** (Phase D,
  `sql/003_audit.sql`). One `audit_entry` row per changed FIELD rather than per
  request, so a later report can ask "when did this number move" of one column.
  `old_value`/`new_value` are TEXT whatever the source column's type is,
  because one trail carries a status word, an odometer integer and a person's
  name; nothing downstream does arithmetic on an audit value. `field` holds the
  camelCase API name (`modelYear`, not `model_year`) — the trail names what the
  caller sent.
- **VIN and status are not PATCHable; a closed worksheet still is** (Phase D,
  `src/DealDesk/Api/AuditDtos.cs`). `PATCH /api/appraisals/{id}` covers
  modelYear, make, model, trimLevel, miles and appraiser. VIN is the vehicle's
  identity — correcting it means the worksheet was opened on the wrong car —
  and status moves through the lifecycle route, which checks the transition.
  Edits to a `won`/`lost` worksheet are allowed rather than refused: SPEC.md
  states no such rule, and the trail records who changed a closed file and why.
- **`HealthzTests` asserts the newest available migration** (Phase B,
  `tests/DealDesk.Tests/HealthzTests.cs`). It hardcoded `001_`, which adding
  `002_worksheet.sql` would have dated; it now compares against
  `Migrator.Available()[^1]` so no future migration breaks it.
