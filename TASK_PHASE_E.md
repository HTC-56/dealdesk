# Phase E — recon actuals and variance

**ROADMAP row: "4 — Recon actuals + variance"** (NOT BUILT → SHIPPED at §E11).
SPEC.md feature 4: after the store acquires the vehicle, real invoices post
against the recon estimate line by line, and the gap between the two is data
in its own right rather than something a report recomputes later.

## Already shipped by the planning lane

Commits `feat(E1)`…`feat(E4)` landed the hard code. §E5 starts green at **100
tests**. What exists now, on top of Phase D:

- `sql/004_recon_actual.sql` — the `recon_actual` table, its three CHECK
  constraints and its index.
- `src/DealDesk/Domain/ReconVariance.cs` — `ReconEstimateLine`,
  `ReconPosting`, `ReconLineVariance`, `ReconCategoryVariance`,
  `ReconVarianceSummary`, and `ReconVariance.Summarise`.
- `src/DealDesk/Api/ReconDtos.cs` — `ReconActualView`, `PostReconActual` (with
  its `Validate()`), and the three variance views. **You add no DTOs in this
  phase — they are written.**
- `src/DealDesk/Api/ReconEndpoints.cs` — `GET .../recon-variance`, plus the
  `ReconActualColumns` const and the `ReconLineBelongs` helper §E5 uses.
  Already wired in `Program.cs`.

**Variance, in one sentence.** `variance = actual − estimate`, so a POSITIVE
variance means the line ran over what was estimated. That sign is the same on
every line, every category and the worksheet total.

**A posting hangs off a recon LINE.** The path is
`/api/appraisals/{id}/recon-lines/{lineId}/actuals`. Work nobody estimated is
entered as a recon line with an estimate of 0 and then posted against normally.

**Amount is signed cents and never zero.** A negative posting is a supplier
credit or a returned part, and it is legal. Zero is refused — by
`PostReconActual.Validate()` as a 400, and by the database as a CHECK.

**Served JSON is camelCase**: `reconLineId`, `postedBy`, `postingCount`,
`byCategory`, `totalVariance`.

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §E5 — Post and list recon actuals

Edit `src/DealDesk/Api/ReconEndpoints.cs`. Add TWO routes inside
`MapReconEndpoints`, directly below the existing `recon-variance` block.

**The template is the recon-line pair in
`src/DealDesk/Api/WorksheetEndpoints.cs`** — the `MapGet` and `MapPost` for
`/api/appraisals/{id:long}/recon-lines`. Copy their shape and change the table,
the type and the ownership check.

`GET /api/appraisals/{id:long}/recon-lines/{lineId:long}/actuals`

- 404 when `ReconLineBelongs(connection, id, lineId)` is false. That helper is
  already in this file, near the bottom. Use it instead of `AppraisalExists` —
  it checks both that the line exists and that it belongs to this appraisal.
- Otherwise `SELECT` the `ReconActualColumns` const `FROM recon_actual WHERE
  recon_line_id = $lineId ORDER BY id`, into `ReconActualView`, and hand the
  rows to `Results.Json`. Oldest first, like every collection except the audit
  trail.

`POST /api/appraisals/{id:long}/recon-lines/{lineId:long}/actuals`

- Body type is `PostReconActual`. Call its `Validate()` first: non-null means
  `Results.BadRequest(new { error })`, exactly as the recon-line POST does.
- Then 404 when `ReconLineBelongs` is false.
- Then `INSERT INTO recon_actual (recon_line_id, amount, description,
  posted_by, posted_at)` with `RETURNING` the same six columns, into
  `ReconActualView`.
- `posted_at` is `WorksheetEndpoints.Timestamp()`, the same call the recon-line
  POST uses for `created_at`.
- Return `Results.Created` with a location of
  `/api/appraisals/{id}/recon-lines/{lineId}/actuals/{row.Id}`.

Do not add a const and do not add a DTO; both exist. Do not touch
`Program.cs`; the file is already wired.

Gate: the four commands above. Commit `ReconEndpoints.cs` only.

---

## §E6 — Recon actual schema tests

Create `tests/DealDesk.Tests/ReconActualSchemaTests.cs`. Mirror
`tests/DealDesk.Tests/AuditSchemaTests.cs` exactly: `using var temp =
TempDb.CreateMigrated();`, `using var connection = temp.Db.Open();`, its
private `CreateAppraisal(connection)` helper copied in, and
`Assert.Throws<SqliteException>` around each rejected statement.

Add a second private helper beside it, `CreateReconLine(connection,
appraisalId)`, built the same way: `INSERT INTO recon_line (appraisal_id,
category, description, estimate, created_at) … RETURNING id`, with category
`'paint'`, description `'respray rear quarter'`, estimate `120000`.

A posting inserts with
`(recon_line_id, amount, description, posted_by, posted_at)`. A good one is
amount `90000`, description `'body shop invoice 4471'`, posted_by
`'R. Vasquez'`, posted_at an ISO timestamp.

Assert these six:

1. A well-formed posting inserts with no exception.
2. A posting whose `amount` is `0` throws, and the message contains
   `"CHECK constraint failed"`.
3. A posting whose `description` is `''` throws the same way.
4. A posting whose `posted_by` is `''` throws the same way.
5. A posting whose `amount` is `-5000` inserts with NO exception — a credit is
   legal, unlike a recon estimate line.
6. After inserting two postings, `DELETE FROM recon_line WHERE id = $lineId`
   leaves `SELECT COUNT(*) FROM recon_actual` at 0 — the postings cascade with
   their line.

Use invented demo names only; no real make, model, person or vendor.

Gate: the four commands above. Commit the one new test file.

---

## §E7 — Variance arithmetic tests

Create `tests/DealDesk.Tests/ReconVarianceTests.cs`. Mirror
`tests/DealDesk.Tests/LifecycleTests.cs`: one `[Fact]` per law, plain `Assert`
calls, no database and no HTTP — `ReconVariance` is pure arithmetic.

`using DealDesk.Domain;` and `using Xunit;`.

Two shapes you will build. An estimate line is an object initialiser with four
required properties — `new ReconEstimateLine { LineId = 1, Category = "paint",
Description = "respray rear quarter", Estimate = new Money(120_000) }`. A
posting is positional — `new ReconPosting(1, new Money(90_000))`. Call
`ReconVariance.Summarise(lines, postings)` with two lists.

Build ONE fixture used by facts 1–5: lines `(1, "paint", 120_000)`,
`(2, "tires", 64_000)`, `(3, "paint", 35_000)`; postings `(1, 90_000)`,
`(1, 45_000)`, `(2, 60_000)`, `(2, -5_000)`. Line 3 gets nothing.

Assert these six:

1. Line 1 has `Actual` `135_000`, `Variance` `15_000` and `PostingCount` 2 —
   two postings summed, and it ran over.
2. Line 2 has `Actual` `55_000` and `Variance` `-9_000` — the negative posting
   is a credit and reduces the actual.
3. Line 3 has `Actual` 0, `Variance` `-35_000` and `Posted` false.
4. `TotalEstimate` is `219_000`, `TotalActual` is `190_000`, `TotalVariance` is
   `-29_000`, and `UnpostedLines` is 1.
5. `ByCategory` has 2 entries in name order: `[0].Category` is `"paint"` with
   `LineCount` 2, `Estimate` `155_000` and `Actual` `135_000`; `[1].Category`
   is `"tires"`.
6. `Summarise` with an empty line list and an empty posting list returns empty
   `Lines` and a `TotalVariance` of `Money.Zero`. `Summarise` with the three
   lines above plus a posting naming line `42` throws `ArgumentException`.

Money values compare with `Assert.Equal(new Money(135_000), line.Actual)`, or
compare `.Cents` — either is fine.

Gate: the four commands above. Commit the one new test file.

---

## §E8 — Posting actuals over HTTP

Create `tests/DealDesk.Tests/ReconActualApiTests.cs`. Mirror
`tests/DealDesk.Tests/WalkItemApiTests.cs`: one `[Fact]` per assertion, `using
var factory = new DeskAppFactory();`, `using var client =
factory.CreateClient();`, then `var id = await
WalkItemApiTests.CreateAppraisalAsync(client);`.

Post with `PostAsJsonAsync` and an anonymous camelCase object; read with
`JsonDocument.Parse(await response.Content.ReadAsStringAsync())`.

Add one private helper, `CreateReconLineAsync(client, appraisalId)`: POST to
`/api/appraisals/{id}/recon-lines` with
`new { category = "paint", description = "respray rear quarter", estimate = 120_000 }`
and return the response body's `id`. A posting body is
`new { amount = 90_000, description = "body shop invoice 4471", postedBy = "R. Vasquez" }`.

The route is `/api/appraisals/{id}/recon-lines/{lineId}/actuals`.

Assert these six:

1. POST returns 201, and the body's `amount` is `90000` and `reconLineId` is
   the line id.
2. After two postings (`90_000` then `45_000`), GET returns an array of length
   2 with `90000` at index 0 — oldest first.
3. A body with `amount = 0` returns 400 with an `error` field; a body with a
   blank `description` returns 400 too.
4. A body with `amount = -5_000` returns 201 — a credit is legal.
5. GET and POST on `/api/appraisals/9999/recon-lines/1/actuals` both return
   404.
6. A recon line created on a SECOND appraisal returns 404 when addressed under
   the FIRST appraisal's id — a line from another worksheet is not reachable.

Each test gets its own factory, so no test sees another's rows.

Gate: the four commands above. Commit the one new test file.

---

## §E9 — Variance over HTTP

Create `tests/DealDesk.Tests/ReconVarianceApiTests.cs`. Mirror
`tests/DealDesk.Tests/ReconActualApiTests.cs` from §E8 for structure — same
factory, same `CreateAppraisalAsync`, same `JsonDocument` reads, and copy its
`CreateReconLineAsync` helper in (give it category and estimate parameters
this time, so a test can make a `tires` line too).

The route is `GET /api/appraisals/{id}/recon-variance`. It always returns 200
for a real appraisal — there is no 409 here.

Assert these six:

1. An appraisal with no recon lines returns 200 with `totalEstimate` 0,
   `totalVariance` 0, and a `lines` array of length 0.
2. Two lines and no postings: `totalEstimate` is their sum, `totalActual` is 0,
   `unpostedLines` is 2, and `lines[0].posted` is false.
3. After posting `90_000` and `45_000` against a `120_000` paint line, that
   line's `actual` is `135000`, `variance` is `15000` and `postingCount` is 2.
4. On the same worksheet, `totalVariance` equals `totalActual` minus
   `totalEstimate`.
5. Two `paint` lines and one `tires` line produce a `byCategory` array of
   length 2, with `byCategory[0].category` `"paint"` and its `lineCount` 2.
6. `GET /api/appraisals/9999/recon-variance` returns 404.

Post the actuals through the §E8 route, not straight into SQLite.

Gate: the four commands above. Commit the one new test file.

---

## §E10 — Document recon actuals in the README

Edit `README.md`. Two small changes, nothing else touched.

**1.** In the existing `## The API` bullet list, append three lines after the
`GET /api/appraisals/{id}/audit` line:

- `GET /api/appraisals/{id}/recon-lines/{lineId}/actuals` — posted actual costs for one estimate line
- `POST /api/appraisals/{id}/recon-lines/{lineId}/actuals` — post an actual cost against that line
- `GET /api/appraisals/{id}/recon-variance` — estimate vs actual, per line and by category

Then, after the existing "**Every change is audited.**" paragraph, add ONE new
paragraph, no more:

- **Recon actuals carry their variance.** Actual costs post against a recon
  estimate line, many per line, and `GET .../recon-variance` serves
  `variance = actual − estimate` for every line, rolled up by category and for
  the worksheet. A positive variance means the line ran over estimate. A
  posting may be negative — that is a credit — but never zero, and
  `unpostedLines` says how many lines nothing has been spent against yet.

**2.** The quickstart's healthz sample output still shows
`"schema":"003_audit"`. Change that one line to `004_recon_actual` — a fourth
migration has shipped.

Promise nothing unbuilt: no desk page, no reports, no seeded demo data, no
authentication.

`verify.sh` lints README code blocks: any `./x`, `bash x` or `--project x`
path inside a fence must exist in the repo. Add no such new reference.

Gate: `bash verify.sh`. Commit `README.md` only.

---

## §E11 — Close Phase E

Three small things, one commit. Mirror what §D10 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it.

**2. Append a `## Phase E` section to STATUS.md** — append only, never rewrite
what is above it. A few lines: actual recon costs now post line-by-line
against the estimate, credits post negative and zero is refused, and
`GET .../recon-variance` serves `actual − estimate` per line, by category and
for the worksheet with `unpostedLines` flagging unfinished recon. Give the test
count from your own `dotnet test` run. Then say what is still NOT built: the
three reports (look-to-book, recon variance, gross by appraiser), the desk
page, the ops surface beyond `/healthz`, seeded demo data, and deploy
packaging.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 4 → `SHIPPED`, phase `E`, note: line-by-line actuals with credits, and
  variance served per line, by category and per worksheet.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
