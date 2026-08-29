# Phase C — finish the worksheet: child collections and the priced offer

**ROADMAP row: "2 — Appraisal worksheet + offer math with visible derivation"**
(PARTIAL → SHIPPED at §C9). Phase B built the tables and the math; Phase C puts
the rest of the worksheet on the wire — the recon lines and comps a desk types,
and the offer endpoint that hands back the recommended value with every step
that produced it.

## Already shipped by the planning lane

Commits `feat(C1)`…`feat(C3)` landed the hard code. §C4 starts green at **64
tests**. What exists now, on top of Phase B:

- `src/DealDesk/Api/WorksheetDtos.cs` — `WalkItemView`/`CreateWalkItem`,
  `ReconLineView`/`CreateReconLine`, `CompView`/`CreateComp`,
  `OfferInputsView`/`SaveOfferInputs`, `OfferView`/`DerivationLineView`. Every
  Create body already has its own `Validate()`. **You do not add DTOs in this
  phase — they are all written.**
- `src/DealDesk/Api/WorksheetEndpoints.cs` — the walk-item pair
  (`GET`/`POST /api/appraisals/{id}/walk-items`) plus the shared
  `AppraisalExists(connection, id)` and `Timestamp()` helpers.
- `src/DealDesk/Api/OfferEndpoints.cs` — `GET`/`PUT .../offer-inputs` and
  `GET .../offer`.
- `tests/DealDesk.Tests/WalkItemApiTests.cs` and `OfferSmokeTests.cs`.

**Money on the wire is whole CENTS**, in fields with no suffix: `estimate`,
`price`, `pack`, `targetGross`, `anchorOverride`, `amount`, `runningTotal`,
`recommended`. `1_550_000` is $15,500.00. Nothing anywhere divides by 100.

**Served JSON is camelCase**: `appraisalId`, `modelYear`, `createdAt`.

**Reuse the appraisal helper.** `WalkItemApiTests.CreateAppraisalAsync(client)`
is `internal static` and returns the new id — every test file below calls it
instead of writing its own POST body.

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §C4 — The recon-line collection

Edit `src/DealDesk/Api/WorksheetEndpoints.cs`. Add TWO routes inside
`MapWorksheetEndpoints`, directly below the existing walk-item `MapPost`.

**The walk-item pair immediately above is the template.** Copy its shape and
change the table, the DTOs and the column list. Nothing else differs.

Add one more `private const string` beside `WalkItemColumns`, listing the
`recon_line` columns in schema order:
`id, appraisal_id, category, description, estimate, created_at`.

`GET /api/appraisals/{id:long}/recon-lines`
- 404 when `AppraisalExists` says no.
- Otherwise `SELECT` those columns `FROM recon_line WHERE appraisal_id = $id
  ORDER BY id;` into `ReconLineView` and hand to `Results.Json`.

`POST /api/appraisals/{id:long}/recon-lines`, taking `CreateReconLine body`
- `body.Validate()` first → `Results.BadRequest(new { error })`. Validate
  BEFORE opening the connection, exactly as the walk-item POST does.
- Then 404 when the appraisal is missing.
- Then INSERT `(appraisal_id, category, description, estimate, created_at)` and
  return the stored row with `RETURNING`, via `QuerySingle<ReconLineView>`.
  Category comes from `body.CanonicalCategory()`, the timestamp from
  `Timestamp()`.
- `Results.Created($"/api/appraisals/{id}/recon-lines/{row.Id}", row)`.

`body.Estimate` is already a `long` of cents — pass it straight through, no
`Money` conversion. Do not touch `Program.cs`; the file is already wired.

Gate: the four commands above. Commit `WorksheetEndpoints.cs` only.

---

## §C5 — The comp collection

Edit `src/DealDesk/Api/WorksheetEndpoints.cs` again. Add TWO more routes below
the recon-line pair you just wrote. **That recon-line pair is the template** —
same shape, different table.

Add a `private const string` for the `comp` columns in schema order:
`id, appraisal_id, label, model_year, miles, price, note, created_at`.

`GET /api/appraisals/{id:long}/comps` — 404 for an unknown appraisal, otherwise
the rows `WHERE appraisal_id = $id ORDER BY id;` as `CompView`.

`POST /api/appraisals/{id:long}/comps`, taking `CreateComp body` — `Validate()`
to a 400, 404 for an unknown appraisal, then INSERT
`(appraisal_id, label, model_year, miles, price, note, created_at)` with
`RETURNING`, and `Results.Created` on
`$"/api/appraisals/{id}/comps/{row.Id}"`.

Two small differences from the recon pair:

- `CreateComp` has no `Canonical...()` helper — a comp label is free text, so
  pass `body.Label` through as it is.
- `note` is optional: use `body.Note ?? string.Empty`, the way the walk-item
  POST handles its own note.

Comps are hand-entered by design. Do not add any lookup, decode or fetch.

Gate: the four commands above. Commit `WorksheetEndpoints.cs` only.

---

## §C6 — Recon-line and comp API tests

Create `tests/DealDesk.Tests/WorksheetApiTests.cs`. Mirror
`WalkItemApiTests.cs`: one `[Fact]` per assertion, `using var factory = new
DeskAppFactory();`, `using var client = factory.CreateClient();`, then
`var id = await WalkItemApiTests.CreateAppraisalAsync(client);`.

Post bodies with `PostAsJsonAsync` and an anonymous camelCase object; read the
response with `JsonDocument.Parse(await response.Content.ReadAsStringAsync())`.

Assert these six:

1. POSTing a recon line — category `mechanical`, description `timing belt`,
   estimate `45000` — returns 201, and the body's `category` is `mechanical`
   and `estimate` is `45000`.
2. POSTing a recon line with category `wheels` returns 400 and a body carrying
   an `error` field. (`wheels` is not in the vocabulary; `tires` is.)
3. POSTing a recon line with `estimate` of `-1` returns 400.
4. POSTing a comp — label `2019 Trailhead LT`, modelYear 2019, miles 71000,
   price `1550000` — returns 201 and the body's `price` is `1550000`.
5. After that POST, `GET /api/appraisals/{id}/comps` returns 200, an array of
   length 1, whose `label` is `2019 Trailhead LT`.
6. `GET /api/appraisals/9999/comps` returns 404.

Each test gets its own factory, so no test sees another's rows. Use the demo
names above and invent nothing real — no actual make, model or dealership.

Gate: the four commands above. Commit the one new test file.

---

## §C7 — Offer endpoint API tests

Create `tests/DealDesk.Tests/OfferApiTests.cs`. Mirror `OfferSmokeTests.cs` for
structure, but write comps over HTTP with the endpoint from §C5 — you do not
need the `SeedComp` helper, and you must not copy it.

`OfferSmokeTests.cs` already covers the happy path, the anchor override, the
409 and the upsert. These six are the ones it does not:

1. `GET /api/appraisals/{id}/offer-inputs` on a brand-new appraisal returns 200
   with `pack` 0, `targetGross` 0, and `anchorOverride` null. (Use
   `JsonValueKind.Null` on that property.)
2. `PUT .../offer-inputs` with `pack` of `-1` returns 400 and a body with an
   `error` field.
3. `PUT .../offer-inputs` with `anchorOverride` of `0` returns 400 — the
   override must be positive when present.
4. `PUT /api/appraisals/9999/offer-inputs` returns 404, and
   `GET /api/appraisals/9999/offer` returns 404.
5. With one comp of `1000000` posted and offer inputs of pack `0`,
   targetGross `0`, the offer's `recommended` is `1000000` and `compCount`
   is 1.
6. Raising `targetGross` from `0` to `200000` on that same worksheet drops
   `recommended` by exactly `200000` — read the offer, PUT again, read it
   again, compare.

Use `PutAsJsonAsync` for the PUTs (`using System.Net.Http.Json;`).

Gate: the four commands above. Commit the one new test file.

---

## §C8 — Document the worksheet API in the README

Edit `README.md`. Add ONE new `## The API` section between the existing
`## Quickstart` and `## Layout` sections. Append-style: change nothing that is
already there except inserting this section.

List every route that now exists, one line each, as a markdown table or a plain
bullet list — route, verb, one clause of what it does:

- `GET /healthz`
- `GET|POST /api/appraisals`, `GET /api/appraisals/{id}`
- `GET|POST /api/appraisals/{id}/walk-items`
- `GET|POST /api/appraisals/{id}/recon-lines`
- `GET|POST /api/appraisals/{id}/comps`
- `GET|PUT /api/appraisals/{id}/offer-inputs`
- `GET /api/appraisals/{id}/offer`

Then two short paragraphs, no more:

- **Money is whole cents.** Every money field on the wire — `estimate`,
  `price`, `pack`, `targetGross`, `anchorOverride`, `recommended` — is an
  integer number of cents. `1550000` is $15,500.00.
- **The offer carries its derivation.** `GET .../offer` returns `recommended`
  together with a `derivation` array of `{label, amount, runningTotal}`; the
  amounts sum to `recommended`. A worksheet with no comps and no anchor
  override returns 409.

Finally add ONE fenced `bash` block showing a `curl` that reads an offer, on
`http://localhost:5000`. **Do not promise the desk page, the audit trail, the
reports or authentication — none of them exist yet.**

`verify.sh` lints README code blocks: any `./x`, `bash x` or `--project x` path
inside a fence must exist in the repo. `curl` lines are fine; do not add a
`bash` or `./` reference to a file that is not there.

Gate: `bash verify.sh`. Commit `README.md` only.

---

## §C9 — Close Phase C

Three small things, one commit. Mirror what §B11 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it.

**2. Append a `## Phase C` section to STATUS.md** — append only, never rewrite
what is above it. A few lines: the walk / recon / comp collections are on the
wire, the offer endpoint serves the recommended value with its derivation, and
the store numbers upsert. Give the test count from your own `dotnet test` run.
Then say what is still NOT built: lifecycle and the audit trail, recon actuals
and variance, the three reports, the desk page, the ops surface beyond
`/healthz`, seeded demo data, and deploy packaging.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 2 → `SHIPPED`, phase `B–C`, note: worksheet, child collections and the
  offer endpoint with its visible derivation.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
