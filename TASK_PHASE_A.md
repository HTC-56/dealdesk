# Phase A — prove the toolchain

**ROADMAP row: "1 — .NET 8 minimal API + SQLite/Dapper + migrator"** (NOT BUILT →
SHIPPED at §A8). SPEC.md's pre-registered rules require this phase before any
domain feature: SDK present, solution builds, one migration + one Dapper
round-trip + one property test green, `dotnet format` wired.

## Already shipped by the planning lane

Commits `feat(A1)` and `feat(A2)` landed the scaffold, so §A3–§A8 start from a
green tree. What exists now:

- `DealDesk.sln`, `Directory.Build.props` (net8.0, nullable, warnings-as-errors),
  `.editorconfig`, `global.json`.
- `src/DealDesk/Data/Migrator.cs` — applies `sql/NNN_*.sql` once each, recorded
  in `schema_migrations`. Scripts are embedded resources.
- `sql/001_init.sql` — the `appraisal` table (identity, lifecycle, CHECK
  constraints on status / miles / model_year / VIN length).
- `src/DealDesk/Domain/Money.cs` — whole cents in a `long`, checked arithmetic.
  `src/DealDesk/Data/MoneyTypeHandler.cs` maps it to a SQLite INTEGER.
- `src/DealDesk/Data/Db.cs` — connection factory (WAL, foreign keys on) and
  `RegisterTypeHandlers()`, which also turns on Dapper snake_case matching, so
  **queries select real column names and need no `AS` aliases**.
- `src/DealDesk/Program.cs` — migrates at startup, serves `GET /healthz`.
- Tests: `TempDb.cs`, `Gen.cs`, `DeskAppFactory.cs`, `MigratorTests.cs`,
  `AppraisalRoundTripTests.cs`, `MoneyPropertyTests.cs`, `HealthzTests.cs`.
  10 green.

**Gate for every task below** (all four, all green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh     # from §A3 onward
```

---

## §A3 — scripts/scrub-check.sh

Create `scripts/scrub-check.sh`. This repo will be published; this script is the
file half of that discipline (DECISIONS.md, "Public-repo discipline").

Behaviour: scan the tracked files only — enumerate them with `git ls-files` so
`bin/`, `obj/` and `*.db` are never scanned. For each offence, print the file,
the line number, and what matched. Exit 1 if there was any offence, 0 if clean.
Start the script with `set -euo pipefail`.

What counts as an offence. **Spell these patterns out in the script from the
descriptions — do not copy tokens off this page**, or the script will flag this
very file:

1. An absolute path rooted in a user's home directory: a leading slash, then
   either the Unix home-root directory name (h-o-m-e) or the macOS one
   (U-s-e-r-s), then another slash and a name.
2. An IP address that is not a documentation address. Match dotted quads, then
   allow only `127.0.0.1`, `0.0.0.0`, and anything starting `192.0.2.` —
   everything else is an offence.
3. A private hostname: a hostname label, a dot, then either l-o-c-a-l or
   i-n-t-e-r-n-a-l at the end of the name. Require the label before the dot so
   ordinary prose does not match.
4. Key material: a PEM private-key header — five dashes, then B-E-G-I-N, then a
   run of capitals and spaces, ending in the two words that close such a header.
   Match the whole header, never the trailing words on their own. Plus these
   secret-bearing names, case-insensitively: the letters a-p-i then an optional
   underscore then k-e-y; the word secret, an underscore, then k-e-y; and the
   word password immediately followed by an equals sign.

Two things that will bite if you skip them:

- **The script must not flag itself.** Exclude `scripts/scrub-check.sh` from the
  file list before scanning — it is the one file that legitimately contains
  every pattern.
- Everything else tracked must pass as-is, including `.editorconfig`,
  `.gitignore`, `README.md` and the phase docs. If one of those trips a
  pattern, your pattern is too loose — tighten it; do not add an exclusion.

Verify by running it: it must exit 0 on the tree as it stands. Then temporarily
add an offending line to a scratch file to confirm it exits 1, and remove that
scratch file before committing. Do not commit the scratch file.

Gate: the four commands above. Commit `scripts/scrub-check.sh` only.

---

## §A4 — README.md quickstart

Create `README.md` at the repo root. It does not exist yet.

Sections, in this order:

1. **Title + one paragraph.** What dealdesk is, drawn from the first paragraph
   of `SPEC.md` — an appraisal desk for a used-car department as a small .NET 8
   service, every number's derivation shown, every value change audited. Grep
   SPEC.md for its opening lines rather than reading it whole.
2. **Requirements** — the .NET 8 SDK, nothing else. No database server: SQLite
   is a file.
3. **Quickstart** — a fenced `bash` block with, in order: `dotnet build`,
   `dotnet test`, `dotnet run --project src/DealDesk`. Then say the service
   answers on `http://localhost:5000/healthz` and show a `curl` of it in a
   second fenced block with its JSON response.
4. **Layout** — a short list of `src/DealDesk/`, `sql/`, `tests/`, `scripts/`
   and one line each on what lives there.
5. **Gates** — the four gate commands above, in one fenced block.

Hard constraints: `localhost` is the only host you may write; no other IP or
hostname anywhere. Every command you show must actually work from a clean
clone — run each one before you commit. Do not describe endpoints or features
that do not exist yet (there is only `/healthz` today); the worksheet, reports
and desk page arrive in later phases.

Gate: the four commands above. Commit `README.md` only.

---

## §A5 — More Money laws

Create `tests/DealDesk.Tests/MoneyArithmeticTests.cs`. Do not edit
`MoneyPropertyTests.cs` — mirror it: one `[Fact]` per law, each looping the
cases from `Gen` with a distinct seed, asserting inside the loop.

Assert these six laws:

1. Subtraction undoes addition: for generated pairs `(a, b)`, adding `b` then
   subtracting `b` returns the original value.
2. Negating twice returns the original value.
3. `Money.Zero` is the identity for addition — adding it changes nothing.
4. The comparison operators (`<`, `>`, `<=`, `>=`) agree with comparing the two
   raw `Cents` values the same way.
5. `Money.FromDollars` rejects an amount finer than one cent — passing `10.005m`
   throws `ArgumentOutOfRangeException`. (One plain `Assert.Throws`, no loop.)
6. `Money.Sum` of an empty sequence is `Money.Zero`.

Use `Gen.Cents` for single values and `Gen.CentPairs` for pairs; pick seeds not
already used in `MoneyPropertyTests.cs`. `Money` lives in `DealDesk.Domain`.

Gate: the four commands above. Commit the one new test file.

---

## §A6 — Money survives SQLite

Create `tests/DealDesk.Tests/MoneyStorageTests.cs`. This proves
`MoneyTypeHandler` — that a `Money` written through Dapper comes back bit-for-bit.

Mirror `AppraisalRoundTripTests.cs` for the shape: `using var temp =
TempDb.CreateMigrated();`, `using var connection = temp.Db.Open();`, then plain
parameterised SQL through Dapper. The type handler is already registered — the
`Db` constructor does it, so there is nothing to set up.

Create a scratch table inside the test itself with a `CREATE TABLE` execute
(one INTEGER column for the amount, plus an id). **This is a test-local table,
not a migration — do not add a file to `sql/`.**

Assert:

1. For the cases from `Gen.Cents` with a fresh seed, inserting a `Money` and
   selecting it back as `Money` returns an equal value.
2. A negative amount round-trips too — recon credits are negative money.
3. Reading the same column as a plain `long` gives exactly the `Cents` of the
   value that was written, proving it is stored as INTEGER cents and not as
   text or a float.

Gate: the four commands above. Commit the one new test file.

---

## §A7 — The schema refuses bad rows

Create `tests/DealDesk.Tests/AppraisalConstraintTests.cs`. `sql/001_init.sql`
carries four CHECK constraints; nothing tests them yet.

Mirror `AppraisalRoundTripTests.cs` — same `TempDb.CreateMigrated()` setup and
the same INSERT statement shape and invented demo values (VIN
`ZZ9ZZ99Z9Z9000042`, make `Meridian`, model `Trailhead`, appraiser
`A. Whitfield`). Put the insert in one small private helper the tests call with
the field being tested varied, so each test is two lines.

A violated CHECK surfaces as `Microsoft.Data.Sqlite.SqliteException`. Assert:

1. A status outside the lifecycle — `"sold"` — is rejected.
2. Negative miles are rejected.
3. A VIN of 16 characters is rejected (the column requires exactly 17).
4. A `model_year` of `1800` is rejected.
5. Each of the five valid statuses — draft, appraised, presented, won, lost —
   inserts without throwing.

Gate: the four commands above. Commit the one new test file.

---

## §A8 — verify.sh, STATUS.md, ROADMAP.md (closes Phase A)

Three small things, one commit.

**1. Create `verify.sh` at the repo root**, executable (`chmod +x`), starting
with `set -euo pipefail`. It runs, in order and stopping at the first failure:
`dotnet build -warnaserror`, `dotnet test`,
`dotnet format --verify-no-changes`, `bash scripts/scrub-check.sh`. Then the
README-quickstart lint: for every repo-relative path README.md mentions in a
fenced block after `--project`, `bash `, or `./`, assert that path exists in the
repo, and fail naming any that do not. Echo a clear PASS line at the end. Run
it; it must exit 0.

**2. Append a `## Phase A` section to STATUS.md** (append only — do not rewrite
what is there). Say in a few lines: the toolchain is proven — .NET 8 SDK builds
the solution warnings-clean, xUnit runs including one in-process
WebApplicationFactory test, the migrator applies `001_init.sql` and is
idempotent, `Money` is property-tested, and `verify.sh` composes every gate.
Give the test count from your own `dotnet test` run. Note what is NOT built yet:
every domain feature — worksheet, offer math, audit, recon, reports, desk page.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 1 → `SHIPPED`, phase `A`.
- Row 7 → leave `PARTIAL` (only `/healthz` exists), phase `A`.
- Row 9 → leave `PARTIAL` (README quickstart only), phase `A`.

Gate: `bash verify.sh` green — that is now the whole gate. Commit `verify.sh`,
`STATUS.md`, `ROADMAP.md`.
