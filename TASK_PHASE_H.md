# Phase H — seeded demo data

**ROADMAP row: "8 — Seeded demo data"** (NOT BUILT → SHIPPED at §H9).
SPEC.md feature 8: "a seed script writes a plausible month of appraisals (won,
lost, in-flight, recon variances) so the page and the reports breathe on first
run."

## Already shipped by the planning lane

Commits `feat(H1)`…`feat(H3)` landed the hard code. §H4 starts green at **184
tests**. What exists now, on top of Phase G:

- `sql/seed.sql` — the demo month. NOT a migration: `Migrator` only matches
  `NNN_name.sql`, so nothing applies it automatically.
- `src/DealDesk/Data/Seeder.cs` — `Seeder.Apply(Db)` runs it in one
  transaction and returns how many appraisals it wrote. It REFUSES (returns 0,
  writes nothing) when the `appraisal` table already holds rows.
- `src/DealDesk/Program.cs` — `dotnet run --project src/DealDesk -- seed`
  writes the month and exits without listening on a port.
- `tests/DealDesk.Tests/SeedSmokeTests.cs` — four end-to-end facts plus the
  constants and helpers below.
- `DeskAppFactory` gains `Seed()`.

**You add no `src/` code in this phase.** §H4–§H7 are test files, §H8 is the
README, §H9 closes. Every type above is finished.

## The demo month, in numbers — every task below reads from this

Twelve worksheets, ids **1..12**, three appraisers, all five lifecycle states:
**4 won, 2 lost, 2 presented, 2 appraised, 2 draft**. Row counts: 24 walk
items, 29 comps, 15 recon lines (ids 1..15), 11 recon actuals, 9 offer inputs,
24 audit entries.

- Appraisers, in the order every report sorts them: `A. Whitfield` (5
  worksheets), `B. Ferreira` (4), `C. Delacroix` (3).
- Worksheet **1** is the worked example: 3 comps averaging **1_850_000**, recon
  estimated **140_000**, pack **90_000**, target gross **150_000**, so
  `recommended` is **1_470_000**.
- Worksheet **12** has NO comps — it is the car that just landed, and the one
  whose `GET .../offer` is a 409.
- Worksheets **5** and **12** (drafts) and **8** (lost before pricing) have no
  `offer_input` row, so their offer inputs read back as zeros.
- Nine of the fifteen recon lines carry postings; **six are unposted**. Exactly
  one posting is negative — a **−15_000** supplier credit on recon line 4.
- 21 of the 24 audit entries have `field` `status`; the other three are
  revisions (`miles` on worksheet 1, `modelYear` on 4, `trimLevel` on 10).
- Worksheet **3** was appraised and then lost; worksheet **8** was lost straight
  from draft. That pair is why `report_look_to_book` reads the audit trail.

## Helpers you will reuse

`internal static` in `SeedSmokeTests.cs`, so call them as `SeedSmokeTests.X`:

- `SeededDb()` — a migrated `TempDb` carrying the demo month. `using var temp =
  SeedSmokeTests.SeededDb();` then `temp.Db.Open()`.
- `SeededClient(factory)` — a client onto a seeded app. Use as
  `using var factory = new DeskAppFactory();` then
  `using var client = SeedSmokeTests.SeededClient(factory);`.
- `ReadAsync(client, path)` — GETs a route that must succeed and returns the
  parsed `JsonDocument`. Use as `using var doc = await
  SeedSmokeTests.ReadAsync(client, "/api/appraisals/1");`.
- Constants: `Worksheets` (12), `ReconLines` (15), `AuditEntries` (24),
  `FirstAnchor`, `FirstRecommended`, `UnpriceableWorksheet` (12), `Appraisers`.

Served JSON is camelCase. Money on the wire is whole cents. Rates are basis
points (`5000` is 50.00%).

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §H4 — What the seed actually wrote

Create `tests/DealDesk.Tests/SeedDataTests.cs`. Mirror
`tests/DealDesk.Tests/AuditSchemaTests.cs`: one `[Fact]` per law, Dapper
queries against a temp database, no HTTP.

Every fact opens its own database:
`using var temp = SeedSmokeTests.SeededDb();` then
`using var connection = temp.Db.Open();`.

Assert these six:

1. Ids are explicit and contiguous. `SELECT MIN(id), MAX(id), COUNT(*) FROM
   appraisal` gives 1, 12 and 12; the same three numbers over `recon_line` give
   1, 15 and 15. A test may therefore name a worksheet by id.
2. Every worksheet carries exactly two walk items: no `appraisal_id` in
   `walk_item` has a count other than 2, and there are 12 distinct ones.
3. Eleven worksheets have comps and one does not — `SELECT COUNT(*) FROM comp
   WHERE appraisal_id = 12` is 0, while `COUNT(DISTINCT appraisal_id)` over
   `comp` is 11.
4. Nine of the fifteen recon lines have at least one posting, so six have none.
   Exactly one row of `recon_actual` has a negative `amount`, and it is
   −15_000 — a returned part is a credit, which is why that column has no
   non-negative CHECK.
5. The trail is 24 entries, 21 of them `field = 'status'`. Worksheet 3 HAS an
   entry whose `field` is `status` and whose `new_value` is `appraised`;
   worksheet 8 has none. Both are `lost` today, and only the trail separates
   them.
6. The month is recent and not in the future. Every `created_at` parses with
   `DateTimeOffset.Parse(value, CultureInfo.InvariantCulture)`, and each one is
   after `DateTimeOffset.UtcNow.AddDays(-31)` and at or before
   `DateTimeOffset.UtcNow`.

Gate: the four commands above. Commit the one new test file.

---

## §H5 — A seeded worksheet over HTTP

Create `tests/DealDesk.Tests/SeedWorksheetApiTests.cs`. Mirror
`tests/DealDesk.Tests/WalkItemApiTests.cs` for structure, but build no
worksheets — the seed already did. Every fact opens its own factory and client
via `SeedSmokeTests.SeededClient`.

Read list routes with `SeedSmokeTests.ReadAsync` and count with
`doc.RootElement.GetArrayLength()`.

Assert these six:

1. `GET /api/appraisals` returns 12 rows, newest first: the first row's `id` is
   12 and the last row's `id` is 1.
2. `GET /api/appraisals?status=won` returns 4 rows and every one of them has
   `status` `won`. `?status=draft` returns 2.
3. `GET /api/appraisals/1` reads back the seeded vehicle: `vin`
   `ZZ9ZZ99Z3Z9000101`, `make` `Meridian`, `model` `Trailhead`, `miles` 74311,
   `appraiser` `A. Whitfield`, `status` `won`.
4. Worksheet 1's child collections are all populated:
   `GET /api/appraisals/1/walk-items` has 2 entries,
   `/recon-lines` has 3, `/comps` has 3.
5. Store numbers: `GET /api/appraisals/1/offer-inputs` gives `pack` 90000 and
   `targetGross` 150000. `GET /api/appraisals/5/offer-inputs` — a draft nobody
   priced — gives `pack` 0 and `targetGross` 0 rather than a 404.
6. `GET /api/appraisals/10/audit` returns 4 entries newest first: the first
   entry's `field` is `status` and its `newValue` is `won`, and somewhere in
   the same trail is an entry whose `field` is `trimLevel` with `oldValue` `LT`
   and `newValue` `SE`.

Gate: the four commands above. Commit the one new test file.

---

## §H6 — The reports, appraiser by appraiser

Create `tests/DealDesk.Tests/SeedReportApiTests.cs`. Mirror
`tests/DealDesk.Tests/LookToBookApiTests.cs`. `SeedSmokeTests` already asserts
the store-wide totals; this file asserts the ROWS underneath them.

One factory and client per fact, via `SeedSmokeTests.SeededClient`. Reach a row
with `doc.RootElement.GetProperty("rows")[0]`.

Assert these six:

1. `GET /api/reports/look-to-book` has 3 rows, ordered by appraiser name:
   `A. Whitfield`, then `B. Ferreira`, then `C. Delacroix`.
2. That report's first row: `looked` 5, `appraised` 4, `booked` 2, `lost` 1,
   `openWorksheets` 2, `bookRateBps` 5000.
3. Its second row shows both the truncation and the lost-from-draft rule:
   `looked` 4, `appraised` 3 (not 4 — that worksheet was lost before anyone
   priced it), `booked` 1, and `bookRateBps` 3333, because one over three
   truncates rather than rounds.
4. Its third row: `looked` 3, `appraised` 2, `booked` 1, `lost` 0,
   `bookRateBps` 5000.
5. `GET /api/reports/front-gross` has 3 rows. The first is `A. Whitfield` with
   `wonCount` 2, `targetGross` 330000, `reconVariance` 10500, `projectedGross`
   319500, `unpostedLines` 0 — recon ran over, so the projection is below the
   plan.
6. Its second row runs the other way: `B. Ferreira`, `reconVariance` −14000 and
   `projectedGross` 164000, which is ABOVE that row's `targetGross` of 150000 —
   and `unpostedLines` is 1, which is what says the projection is provisional.

Gate: the four commands above. Commit the one new test file.

---

## §H7 — Per-worksheet variance over the seeded month

Create `tests/DealDesk.Tests/SeedVarianceApiTests.cs`. Mirror
`tests/DealDesk.Tests/ReconVarianceApiTests.cs`. Route:
`GET /api/appraisals/{id}/recon-variance`. Its body carries `totalEstimate`,
`totalActual`, `totalVariance`, `unpostedLines`, a `lines` array and a
`byCategory` array. Variance is `actual − estimate`, so positive means the line
ran over.

One factory and client per fact, via `SeedSmokeTests.SeededClient`.

Assert these six:

1. Worksheet 1 totals: `totalEstimate` 140000, `totalActual` 145500,
   `totalVariance` 5500, `unpostedLines` 0.
2. Worksheet 1's `byCategory` has 3 rows in name order — `detail`,
   `mechanical`, `tires` — with variances 0, 7500 and −2000. One worksheet
   carries both signs.
3. Worksheet 2's `body` category proves the credit was subtracted, not ignored:
   two postings (90000 and −15000) against a 60000 estimate give `actual` 75000
   and `variance` 15000. Find it in `byCategory`.
4. Worksheet 6 is unfinished recon, not money saved: `totalVariance` is −14000
   and `unpostedLines` is 1. Its `lines` array has 3 entries and exactly one has
   `posted` false.
5. Worksheet 4 has nothing posted at all: `totalActual` 0, `totalVariance`
   −150000, `unpostedLines` 2.
6. Worksheet 12 has no recon lines, so the route answers 200 with
   `totalEstimate` 0, `totalVariance` 0 and an empty `lines` array — never a
   404. An empty sum is genuinely zero.

Gate: the four commands above. Commit the one new test file.

---

## §H8 — Document the demo month in the README

Edit `README.md`. Two changes, nothing else touched.

**1.** In the `## Quickstart` fenced block, add the seed command as a new line
between `dotnet test` and `dotnet run --project src/DealDesk`:

```
dotnet run --project src/DealDesk -- seed
```

**2.** After the existing "**The ops surface is three things.**" paragraph, add
ONE new paragraph, no more. Say: `dotnet run --project src/DealDesk -- seed`
writes a demo month — twelve worksheets across three invented appraisers,
covering all five lifecycle states, with fifteen recon lines across every
category, some posted against and some not, so the reports show real numbers on
first run. Say every name, VIN and price is invented, and that dealdesk never
looks a VIN up anywhere. Say the seed refuses to run a second time: a database
that already holds worksheets is left exactly as it is, so nobody can write two
demo months over each other.

Do NOT change the healthz sample output — this phase adds no migration, so
`005_reports` is still current. Promise nothing unbuilt: there is still no desk
page and no deploy packaging.

`verify.sh` lints README code blocks: any `./x`, `bash x` or `--project x` path
inside a fence must exist in the repo. `src/DealDesk` does, so the new line is
fine.

Gate: `bash verify.sh`. Commit `README.md` only.

---

## §H9 — Close Phase H

Three small things, one commit. Mirror what §G10 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it.

**2. Append a `## Phase H` section to STATUS.md** — append only, never rewrite
what is above it. A few lines: `dotnet run --project src/DealDesk -- seed`
writes a demo month of twelve worksheets across three appraisers, covering all
five lifecycle states, with recon lines in every category, postings including
one credit, and six lines still unposted, so all three reports read real
numbers on a fresh clone; the seed refuses to run into a database that already
holds worksheets. Give the test count from your own `dotnet test` run. Then say
what is still NOT built: the desk page and deploy packaging.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 8 → `SHIPPED`, phase `H`, note: sql/seed.sql, the `seed` verb, a demo
  month of twelve worksheets.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
