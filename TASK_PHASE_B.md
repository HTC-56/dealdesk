# Phase B — the worksheet and the offer math

**ROADMAP row: "2 — Appraisal worksheet + offer math with visible derivation"**
(NOT BUILT → PARTIAL at §B11). This is the centerpiece SPEC.md feature 2: the
vehicle basics with a check-digit VIN, the walk / recon / comps tables, and
offer math that returns its own derivation.

## Already shipped by the planning lane

Commits `feat(B1)`…`feat(B4)` landed the hard code. §B5–§B11 start green
(28 tests). What exists now, on top of Phase A:

- `src/DealDesk/Domain/Vin.cs` — `IsValid`, `TryNormalize`, `Normalize`,
  `CheckDigit`, `WithCheckDigit`. 17 characters, no I/O/Q, modulo-11 check
  digit. No lookups, ever.
- `sql/002_worksheet.sql` — tables `walk_item`, `recon_line`, `comp`,
  `offer_input`. Money columns are INTEGER cents named WITHOUT a `_cents`
  suffix (`estimate`, `price`, `pack`, `target_gross`, `anchor_override`).
  Child rows cascade-delete with their appraisal.
- `src/DealDesk/Domain/Offer.cs` — records `DerivationLine(Label, Amount,
  RunningTotal)`, `OfferInputs`, `Offer`.
- `src/DealDesk/Domain/OfferMath.cs` — `MarketAnchor(...)` and
  `Recommend(OfferInputs)`.
- `src/DealDesk/Api/AppraisalDtos.cs` — `AppraisalView`, `CreateAppraisal`
  (with `Validate()` and `NormalizedVin()`).
- `src/DealDesk/Api/AppraisalEndpoints.cs` — `GET /api/appraisals/{id:long}`,
  wired from `Program.cs`.

**Two demo VINs, one character apart.** `ZZ9ZZ99Z2Z9000042` is check-digit
VALID. `ZZ9ZZ99Z9Z9000042` (the Phase A test VIN) is INVALID. Use the pair.

**Seeds 1001–1009 are taken.** Phase B uses 2001 upward.

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §B5 — VIN tests

Create `tests/DealDesk.Tests/VinTests.cs`. Mirror `MoneyArithmeticTests.cs`:
plain `[Fact]` methods, `Assert` per case, no loops needed except where said.
`Vin` is a static class in `DealDesk.Domain`.

Assert these:

1. `ZZ9ZZ99Z2Z9000042` is valid; `ZZ9ZZ99Z9Z9000042` is not (only the ninth
   character differs — that is the check digit doing its job).
2. Lowercase input is accepted and `Normalize` returns the uppercase form.
3. A 16-character string and an 18-character string are both rejected, and so
   is `null`.
4. A VIN containing `I`, one containing `O`, and one containing `Q` are each
   rejected — the standard excludes those three letters.
5. `CheckDigit("ZZ9ZZ99Z9Z9000042")` returns `'2'`, and `WithCheckDigit` on
   that same string returns the valid VIN from assertion 1.
6. `Normalize` on an invalid VIN throws `ArgumentException`.

Gate: the four commands above. Commit the one new test file.

---

## §B6 — Offer-math property tests

Create `tests/DealDesk.Tests/OfferMathTests.cs`. Mirror `MoneyPropertyTests.cs`:
one `[Fact]` per law, each looping `Gen` cases with its own seed, asserting
inside the loop. Types live in `DealDesk.Domain`.

**Read this first or the tests will throw:** `Gen` yields negative cents, and
`Recommend` deliberately REFUSES a negative `Pack`, `TargetFrontGross` or recon
line. Wrap every generated value in `Math.Abs` before using it as one of those
three. Build inputs with `AnchorOverride = new Money(Math.Abs(a))` so no comps
are needed for the looping laws.

The laws:

1. The `Amount` values in `Derivation` sum exactly to `Recommended`
   (`Money.Sum` over them). Seed 2001.
2. Walking the derivation in order, the running sum of `Amount` equals each
   line's `RunningTotal`. Seed 2002.
3. `Recommended <= Anchor - Recon`. Seed 2003.
4. `Derivation` always has exactly 4 lines and the first line's `Amount`
   equals `Anchor`. Seed 2004.
5. `MarketAnchor` is the comp average, halves away from zero. Plain asserts,
   no loop: one comp of 100 → 100; 100 and 200 → 150; 100 and 101 → 101;
   1, 1, 2 → 1; 1, 2, 2 → 2.
6. Refusals, plain `Assert.Throws`: negative `Pack`, negative
   `TargetFrontGross`, and a negative recon line each throw
   `ArgumentOutOfRangeException`; a default `OfferInputs` (no comps, no
   override) throws `ArgumentException`.

Gate: the four commands above. Commit the one new test file.

---

## §B7 — The worksheet schema refuses bad rows

Create `tests/DealDesk.Tests/WorksheetSchemaTests.cs`. Mirror
`AppraisalConstraintTests.cs` exactly: `TempDb.CreateMigrated()`, a private
helper that inserts one parent appraisal and returns its id, then one small
helper per child insert so each test is two lines. A violated CHECK or foreign
key surfaces as `Microsoft.Data.Sqlite.SqliteException`.

Grep `sql/002_worksheet.sql` for the constraint names before writing.

Assert:

1. A `recon_line` with a category outside the vocabulary (`'wheels'`) is
   rejected; `'mechanical'` inserts fine.
2. A `recon_line` with a negative `estimate` is rejected, and one with an
   empty `description` is rejected.
3. A `comp` with `price` of 0 is rejected; a positive price inserts fine.
4. A `walk_item` with `severity` of `'catastrophic'` is rejected; `'moderate'`
   inserts fine.
5. An `offer_input` with a negative `pack` is rejected.
6. Deleting the parent appraisal row removes its `recon_line` children —
   count them before and after. (`Db.Open` turns `PRAGMA foreign_keys` on, so
   the cascade is live.)

Money columns take a plain integer of cents; you can pass `Money` directly as
a Dapper parameter — `MoneyTypeHandler` is already registered.

Gate: the four commands above. Commit the one new test file.

---

## §B8 — GET /api/appraisals (the list)

Edit `src/DealDesk/Api/AppraisalEndpoints.cs`. Add ONE endpoint inside
`MapAppraisalEndpoints`, directly below the existing
`MapGet("/api/appraisals/{id:long}", ...)`.

Mirror that sibling handler line for line — same `using var connection =
db.Open();`, the same `"SELECT " + Columns + " FROM appraisal"` shape, the same
`Results.Json(...)`. Do not add a repository class and do not touch
`Program.cs`.

Route: `GET /api/appraisals`. Behaviour:

- Returns every appraisal, newest first — `ORDER BY id DESC`.
- Accepts an optional `status` query-string parameter. When present, filter
  with `WHERE status = $status`; when absent or empty, return all rows.
- Use `connection.Query<AppraisalView>(...)` and hand the result to
  `Results.Json`. An empty table is an empty JSON array and a 200, not a 404.

Bind the optional parameter as a nullable string handler argument; minimal APIs
take it from the query string automatically. Keep the SQL a compile-time
constant — build the two query strings as literals and pick between them,
rather than concatenating a runtime variable into the SQL.

Gate: the four commands above. Commit `AppraisalEndpoints.cs` only.

---

## §B9 — POST /api/appraisals (create)

Edit `src/DealDesk/Api/AppraisalEndpoints.cs`. Add ONE endpoint below the two
GETs. The validation rules are already written — do not restate them here.

Route: `POST /api/appraisals`, taking `CreateAppraisal body` and `Db db`.
Behaviour, in order:

1. Call `body.Validate()`. If it returns a non-null message, return
   `Results.BadRequest(new { error = message })` and stop.
2. Otherwise insert one `appraisal` row. Take the VIN from
   `body.NormalizedVin()`, `status` `'draft'`, and the same timestamp for
   `created_at` and `updated_at` — `DateTimeOffset.UtcNow.ToString("O")`, as
   `AppraisalRoundTripTests.cs` does. `trim_level` falls back to an empty
   string when the body omits it.
3. Get the new id back with `RETURNING id` via `QuerySingle<long>` — the
   INSERT statement in `AppraisalRoundTripTests.cs` is the pattern.
4. Return `Results.Created($"/api/appraisals/{id}", new { id })`.

Do not add authentication; the bearer token is a later phase. Do not write an
audit row; the audit trail is a later phase.

Gate: the four commands above. Commit `AppraisalEndpoints.cs` only.

---

## §B10 — Appraisal API integration tests

Create `tests/DealDesk.Tests/AppraisalApiTests.cs`. Mirror `HealthzTests.cs`:
`using var factory = new DeskAppFactory();`, `factory.CreateClient()`, assert
on the real HTTP response, parse the body with `JsonDocument`.

**JSON is camelCase.** The served field names are `id`, `vin`, `modelYear`,
`trimLevel`, `createdAt` — not the snake_case column names.

Post bodies with `PostAsJsonAsync` (`using System.Net.Http.Json;`). Send an
anonymous object with camelCase property names.

Assert:

1. Posting a valid body — VIN `ZZ9ZZ99Z2Z9000042`, year 2019, make `Meridian`,
   model `Trailhead`, miles 74311, appraiser `A. Whitfield` — returns 201 and
   a `Location` header. Invent nothing real: keep those demo values.
2. Following that up with `GET /api/appraisals/{id}` returns 200 and the same
   VIN, with `status` `draft`.
3. Posting the same body with VIN `ZZ9ZZ99Z9Z9000042` returns 400 and a body
   carrying an `error` field.
4. Posting a body with a blank `appraiser` returns 400.
5. `GET /api/appraisals` returns 200 and an array containing the created row.
6. `GET /api/appraisals/9999` returns 404.

Each test gets its own factory — the database is per-factory, so tests never
see each other's rows.

Gate: the four commands above. Commit the one new test file.

---

## §B11 — Close Phase B

Three small things, one commit. Mirror what §A8 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it — it already
composes all four gates plus the README lint.

**2. Append a `## Phase B` section to STATUS.md** (append only — never rewrite
what is above it). A few lines: the worksheet schema, the check-digit VIN, and
the offer math with its derivation are in; give the test count from your own
`dotnet test` run. Then say what is still NOT built for this feature: the
walk / recon / comp endpoints, the offer endpoint that serves the derivation,
and the desk page. Lifecycle, audit, recon actuals and the reports are still
untouched.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 2 → `PARTIAL`, phase `B`, note: domain math + schema + appraisal
  create/read; child-collection and offer endpoints still open.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
