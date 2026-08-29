# Phase F — the three reports

**ROADMAP row: "5 — The three reports (look-to-book, recon variance, gross by
appraiser)"** (NOT BUILT → SHIPPED at §F11). SPEC.md feature 5: the three
readings a used-car director actually watches, built as SQL views with
endpoints over them.

## Already shipped by the planning lane

Commits `feat(F1)`…`feat(F4)` landed the hard code. §F5 starts green at **128
tests**. What exists now, on top of Phase E:

- `sql/005_reports.sql` — four views: `appraisal_recon_rollup` (an internal
  building block), `report_look_to_book`, `report_recon_variance`,
  `report_front_gross`.
- `src/DealDesk/Api/ReportDtos.cs` — all six report records and the internal
  `Reports.RateBps` helper. **You add no DTOs in this phase — they are
  written.**
- `src/DealDesk/Api/ReportEndpoints.cs` — `GET /api/reports/look-to-book` and
  `GET /api/reports/recon-variance`, plus all three column consts. Already
  wired in `Program.cs`.
- `tests/DealDesk.Tests/ReportSmokeTests.cs` — four end-to-end facts, and two
  `internal static` helpers the test tasks below reuse.

**Three routes, no id, no 404.** Every report route is `GET /api/reports/...`.
None takes an appraisal id, so none of them 404s: an empty store is an empty
report with zeroed totals and a `rows` array of length 0.

**Rates are basis points.** `bookRateBps` of `2500` means 25.00%. Integers
only — the wire never carries a fraction.

**Variance is still `actual − estimate`**, positive meaning over estimate. The
reports did not redefine it.

**Served JSON is camelCase**: `openWorksheets`, `bookRateBps`, `lineCount`,
`unpostedLines`, `targetGross`, `reconVariance`, `projectedGross`, `rows`.

**Two helpers you will reuse** (both `internal static` in
`tests/DealDesk.Tests/ReportSmokeTests.cs`, so call them as
`ReportSmokeTests.X`):

- `CreateAsync(client, appraiser)` → the new draft worksheet's id. Reports
  group by appraiser, so this one lets you choose the name.
- `MoveAsync(client, id, params string[] statuses)` → walks the worksheet
  through each status in turn.

Invented demo names already in use: appraisers `A. Whitfield` and
`B. Ferreira`, `changedBy` `D. Okonjo`, `postedBy` `R. Vasquez`. Use those; no
real person, store or vendor anywhere.

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §F5 — The front-gross report route

Edit `src/DealDesk/Api/ReportEndpoints.cs`. Add ONE route inside
`MapReportEndpoints`, directly below the existing `recon-variance` block.

**The template is the `/api/reports/recon-variance` route immediately above
it** in the same file. Copy its shape and change four things: the path, the
const, the view name, and the totals.

`GET /api/reports/front-gross`

- Open a connection with `db.Open()`, exactly as the route above does. There is
  no id and no existence check — this route never 404s.
- `SELECT` the `FrontGrossColumns` const (already declared at the top of the
  file) `FROM report_front_gross ORDER BY appraiser`, into `FrontGrossRow`,
  and materialise it with `.AsList()`.
- Return `Results.Json` of a `FrontGrossReport`. Its five totals are summed
  from those same rows — never a second query:
  - `WonCount` ← sum of each row's `WonCount`
  - `TotalTargetGross` ← sum of `TargetGross`
  - `TotalReconVariance` ← sum of `ReconVariance`
  - `TotalProjectedGross` ← sum of `ProjectedGross`
  - `UnpostedLines` ← sum of `UnpostedLines`
  - `Rows` ← the list itself
- The record and every property above already exist in `ReportDtos.cs`.

Do not add a const, do not add a DTO, do not touch `Program.cs` — all three are
done.

Gate: the four commands above. Commit `ReportEndpoints.cs` only.

---

## §F6 — Rate arithmetic tests

Create `tests/DealDesk.Tests/ReportRateTests.cs`. Mirror
`tests/DealDesk.Tests/LifecycleTests.cs`: one `[Fact]` per law, plain `Assert`
calls, no database and no HTTP. This is pure arithmetic.

`using DealDesk.Api;` and `using Xunit;`. `Reports` is internal, and the test
project already has access to internals — no extra setup.

Two shapes. `Reports.RateBps(part, whole)` returns an `int`. A row is an object
initialiser: `new LookToBookRow { Appraiser = "A. Whitfield", Looked = 4,
Appraised = 2, Booked = 1, Lost = 2, OpenWorksheets = 1 }` — `BookRateBps` is
computed, so you never set it.

Assert these six:

1. `RateBps(1, 2)` is `5000` and `RateBps(1, 4)` is `2500` — basis points are
   hundredths of a percent.
2. `RateBps(3, 3)` is `10_000` — all of it. `RateBps(0, 7)` is `0`.
3. `RateBps(1, 3)` is `3333`, not `3334` — the result truncates toward zero
   rather than rounding.
4. `RateBps(5, 0)` is `0` and throws nothing — a whole of zero is a month with
   no rate yet, not a division to refuse.
5. A `LookToBookRow` with `Booked` 1 and `Appraised` 2 has `BookRateBps` 5000,
   and its `Appraised`, not its `Looked`, is the denominator: a row with
   `Looked` 4, `Appraised` 2, `Booked` 1 is 5000, never 2500.
6. A `LookToBookReport` with `Booked` 3 and `Appraised` 4 has `BookRateBps`
   7500, and a default `LookToBookReport` (no properties set) has
   `BookRateBps` 0 and an empty `Rows`.

Gate: the four commands above. Commit the one new test file.

---

## §F7 — Look-to-book over HTTP

Create `tests/DealDesk.Tests/LookToBookApiTests.cs`. Mirror
`tests/DealDesk.Tests/ReportSmokeTests.cs` for structure — `using var factory =
new DeskAppFactory();`, `using var client = factory.CreateClient();`, then
`ReportSmokeTests.CreateAsync(client, "A. Whitfield")` and
`ReportSmokeTests.MoveAsync(client, id, "appraised", "presented", "won")`.

Read responses with `JsonDocument.Parse(await
response.Content.ReadAsStringAsync())`.

The route is `GET /api/reports/look-to-book`. Assert things
`ReportSmokeTests.cs` does not already cover — it covers one appraiser with
four worksheets. Cover the multi-appraiser and ordering behaviour instead.

Assert these six:

1. Two appraisers with worksheets produce a `rows` array of length 2, and
   `rows[0].appraiser` is `"A. Whitfield"` — rows come back in name order, not
   creation order. (Create the `B. Ferreira` worksheet FIRST so creation order
   and name order differ.)
2. Each row's `looked` counts only that appraiser's own worksheets: give
   `A. Whitfield` two and `B. Ferreira` one, and assert `2` and `1`.
3. The top-level `looked` equals the sum of the rows' `looked`.
4. A worksheet walked to `won` shows `booked` 1 and `openWorksheets` 0 on its
   appraiser's row; the appraiser's `bookRateBps` is `10000`.
5. A worksheet left in `draft` counts in `looked` but not in `appraised`, and
   its row's `bookRateBps` is `0`.
6. A worksheet walked `appraised` then `lost` counts in `appraised` AND in
   `lost`, while one walked straight to `lost` from draft counts in `lost` but
   NOT in `appraised`. Assert `appraised` is 1 and `lost` is 2 for an appraiser
   holding both.

Each test gets its own factory, so no test sees another's rows.

Gate: the four commands above. Commit the one new test file.

---

## §F8 — The recon variance report over HTTP

Create `tests/DealDesk.Tests/ReconVarianceReportApiTests.cs`. Mirror
`tests/DealDesk.Tests/LookToBookApiTests.cs` from §F7 for structure — same
factory, same `JsonDocument` reads.

You need recon lines and postings. Copy in two private helpers built the same
way §F7's tests call the API:

- `ReconLineAsync(client, appraisalId, category, estimate)` — POST to
  `/api/appraisals/{id}/recon-lines` with
  `new { category, description = "seeded line", estimate }`, return the body's
  `id`.
- `PostActualAsync(client, appraisalId, lineId, amount)` — POST to
  `/api/appraisals/{id}/recon-lines/{lineId}/actuals` with
  `new { amount, description = "shop invoice", postedBy = "R. Vasquez" }`.

The route is `GET /api/reports/recon-variance`. It reports across EVERY
worksheet, not one. `ReportSmokeTests.cs` already covers two cars with paint
and tires; cover these instead.

Assert these six:

1. A store with worksheets but no recon lines returns 200 with `rows` length 0
   and `totalEstimate` 0.
2. One `mechanical` line of `100_000` and nothing posted: `rows[0].category` is
   `"mechanical"`, `estimate` is `100000`, `actual` is `0`, `variance` is
   `-100000` and `unpostedLines` is `1`.
3. Posting `140_000` against that line flips its `variance` to `40000` and its
   `unpostedLines` to `0` — positive means the category ran over.
4. Two postings against one line (`90_000` then `50_000`) sum into one
   `actual` of `140000`, and `lineCount` stays `1`.
5. A negative posting (a credit) reduces `actual`: `90_000` then `-15_000`
   leaves `actual` at `75000`.
6. Lines in three categories — `detail`, `body`, `tires` — come back in that
   name order: `body`, `detail`, `tires`. Also assert the top-level
   `totalEstimate` equals the sum of the three rows' `estimate`.

Gate: the four commands above. Commit the one new test file.

---

## §F9 — Front gross over HTTP

Create `tests/DealDesk.Tests/FrontGrossApiTests.cs`. Mirror
`tests/DealDesk.Tests/ReconVarianceReportApiTests.cs` from §F8 — same factory,
same `JsonDocument` reads, and copy in its two recon helpers.

The route is `GET /api/reports/front-gross`, the one §F5 built. It counts WON
worksheets only. Set the planned gross with
`client.PutAsJsonAsync($"/api/appraisals/{id}/offer-inputs", new { pack = 0,
targetGross = 150_000 })`.

The rule in one sentence: `projectedGross = targetGross − reconVariance`, so
recon running over eats the planned gross.

Assert these six:

1. A store whose only worksheet is still in `draft` returns 200 with `rows`
   length 0 and `wonCount` 0 — the report counts won worksheets only.
2. One won worksheet with `targetGross` `150_000` and no recon at all:
   `rows[0].appraiser` is its appraiser, `wonCount` is 1, `targetGross` is
   `150000`, `reconVariance` is `0`, `projectedGross` is `150000`.
3. Add a `100_000` recon line and post `140_000` against it: `reconVariance`
   becomes `40000` and `projectedGross` drops to `110000`.
4. Recon that came in UNDER adds to the projection — a `100_000` line with
   `60_000` posted gives `reconVariance` `-40000` and `projectedGross`
   `190000`.
5. A won worksheet with a recon line and NO postings has `unpostedLines` 1, so
   the projection is visibly provisional.
6. Two appraisers each with a won worksheet produce `rows` length 2 in name
   order, and the top-level `totalProjectedGross` equals the sum of the two
   rows' `projectedGross`.

Remember `MoveAsync(client, id, "appraised", "presented", "won")` — `won` only
follows `presented`.

Gate: the four commands above. Commit the one new test file.

---

## §F10 — Document the reports in the README

Edit `README.md`. Two small changes, nothing else touched.

**1.** In the existing `## The API` bullet list, append three lines after the
`GET /api/appraisals/{id}/recon-variance` line:

- `GET /api/reports/look-to-book` — appraised vs won, per appraiser
- `GET /api/reports/recon-variance` — estimate vs actual by recon category, across every worksheet
- `GET /api/reports/front-gross` — planned front gross less recon overage, per appraiser

Then, after the existing "**Recon actuals carry their variance.**" paragraph,
add ONE new paragraph, no more:

- **The three reports read across every worksheet.** Each is a SQL view in
  `sql/005_reports.sql` with an endpoint over it, and each returns store-wide
  totals together with the rows they came from. Look-to-book rates booked
  against appraised — a worksheet abandoned in draft was never a real look at
  the car. Front gross is the gross the worksheet planned, less what recon ran
  over; dealdesk carries no retail selling price. Rates are basis points:
  `bookRateBps` of `2500` is 25.00%.

**2.** The quickstart's healthz sample output still shows
`"schema":"004_recon_actual"`. Change that one line to `005_reports` — a fifth
migration has shipped.

Promise nothing unbuilt: no desk page, no seeded demo data, no authentication,
no `/metrics`.

`verify.sh` lints README code blocks: any `./x`, `bash x` or `--project x`
path inside a fence must exist in the repo. Add no such new reference.

Gate: `bash verify.sh`. Commit `README.md` only.

---

## §F11 — Close Phase F

Three small things, one commit. Mirror what §E11 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it.

**2. Append a `## Phase F` section to STATUS.md** — append only, never rewrite
what is above it. A few lines: the three reports are SQL views with endpoints
over them; look-to-book rates booked against appraised and reads the audit
trail to tell an early-lost worksheet from an appraised one; recon variance
rolls every worksheet up by category; front gross is planned gross less recon
overage, over won worksheets only; rates travel as basis points. Give the test
count from your own `dotnet test` run. Then say what is still NOT built: the
desk page, the ops surface beyond `/healthz`, seeded demo data, and deploy
packaging.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 5 → `SHIPPED`, phase `F`, note: three SQL views with endpoints —
  look-to-book, recon variance by category, front gross by appraiser.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
