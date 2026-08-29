# Roadmap — the v1 scoreboard

One row per SPEC.md feature. The planning lane keeps status current; row edits here
are the one permitted exception to append-only docs.

| # | Feature (SPEC.md) | Status | Phase | Note |
|---|---|---|---|---|
| 1 | .NET 8 minimal API + SQLite/Dapper + migrator | SHIPPED | A | scaffold, migrator, Money, 001_init, Phase A gates |
| 2 | Appraisal worksheet + offer math with visible derivation | PARTIAL | B | domain math + schema + appraisal create/read; child-collection and offer endpoints still open |
| 3 | Lifecycle + append-only audit trail | NOT BUILT | — | |
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
- **`HealthzTests` asserts the newest available migration** (Phase B,
  `tests/DealDesk.Tests/HealthzTests.cs`). It hardcoded `001_`, which adding
  `002_worksheet.sql` would have dated; it now compares against
  `Migrator.Available()[^1]` so no future migration breaks it.
