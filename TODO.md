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

- [ ] A3 — Create `scripts/scrub-check.sh`: scan `git ls-files` output for home
  paths, non-documentation IPs, `.local`/`.internal` hosts and key material;
  exit 1 listing offences. It must exclude itself. Spec: §A3.
- [ ] A4 — Create `README.md`: what dealdesk is, requirements, quickstart
  (build, test, run, curl `/healthz` on localhost), layout, gates. Only
  `/healthz` exists — promise nothing else. Spec: §A4.
- [ ] A5 — Create `tests/DealDesk.Tests/MoneyArithmeticTests.cs`: six more
  Money laws (subtraction undoes addition, double negation, Zero identity,
  comparisons, sub-cent rejection, empty Sum). Mirror
  `MoneyPropertyTests.cs`. Spec: §A5.
- [ ] A6 — Create `tests/DealDesk.Tests/MoneyStorageTests.cs`: Money survives a
  Dapper round-trip as INTEGER cents, negatives included. Mirror
  `AppraisalRoundTripTests.cs`; scratch table in the test, no new migration.
  Spec: §A6.
- [ ] A7 — Create `tests/DealDesk.Tests/AppraisalConstraintTests.cs`: the four
  CHECK constraints in `sql/001_init.sql` reject bad status, negative miles, a
  16-char VIN and year 1800; all five statuses insert. Mirror
  `AppraisalRoundTripTests.cs`. Spec: §A7.
- [ ] A8 — Create `verify.sh` (all four gates + README-path lint), append a
  `## Phase A` section to STATUS.md, and flip the ROADMAP row 1 to SHIPPED.
  Closes Phase A. Gate: `bash verify.sh`. Spec: §A8.
