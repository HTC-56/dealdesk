# Roadmap — the v1 scoreboard

One row per SPEC.md feature. The planning lane keeps status current; row edits here
are the one permitted exception to append-only docs.

| # | Feature (SPEC.md) | Status | Phase | Note |
|---|---|---|---|---|
| 1 | .NET 8 minimal API + SQLite/Dapper + migrator | PARTIAL | A | scaffold, migrator, Money, 001_init shipped; A8 flips to SHIPPED |
| 2 | Appraisal worksheet + offer math with visible derivation | NOT BUILT | — | centerpiece; property-tested |
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
