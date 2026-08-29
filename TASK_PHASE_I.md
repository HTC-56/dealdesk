# Phase I — the desk page

**ROADMAP row: "6 — The desk page"** (NOT BUILT → SHIPPED at §I9).
SPEC.md feature 6: "`GET /` — one self-contained HTML page (inline CSS/JS, no
framework, no build step, no CDN, no web fonts): worksheet form with live offer
math, appraisal list with lifecycle states, the audit trail, the three reports."

## Already shipped by the planning lane

Commits `feat(I1)`…`feat(I3)` landed the hard code. §I4 starts green at **212
tests**. What exists now, on top of Phase H:

- `src/DealDesk/Page/index.html` — the whole page, 811 lines. One `<style>`,
  one `<script>`, nothing else.
- `src/DealDesk/DealDesk.csproj` — embeds `Page/*.html` as `page.index.html`,
  beside the migrations.
- `src/DealDesk/Api/PageEndpoints.cs` — `GET /` serves it as
  `text/html; charset=utf-8`, read once and held.
- `src/DealDesk/Program.cs` — `app.MapPageEndpoints()`, mapped last.
- `tests/DealDesk.Tests/DeskPageSmokeTests.cs` — four end-to-end facts plus
  the helpers below.

**You add no `src/` code in this phase and you never edit `index.html`.**
§I4–§I7 are test files, §I8 is the README, §I9 closes.

## The page, in facts — every task below reads from this

The served body is one document: it starts `<!doctype html>` and ends
`</html>`. Its four panels are `<section id="…">` elements, in this order:
`appraisals`, `worksheet`, `audit`, `reports`.

Named elements a test can look for, spelled exactly as the page spells them:

- Report tables: `<table id="look-to-book">`, `<table id="recon-variance">`,
  `<table id="front-gross">`.
- Offer math: `<tbody id="derivation">` and `<strong id="recommended">`.
- Row bodies: `appraisal-rows`, `walk-rows`, `recon-rows`, `comp-rows`,
  `variance-rows`, `audit-rows`.
- Dropdowns, each holding one `<option>` per legal value: `status-filter`
  (all five lifecycle statuses plus a blank "all"), `next-status` (the four
  states a move can go to), `recon-category` (all eight recon categories),
  `walk-area` (all seven walk areas), `walk-severity` (minor, moderate,
  severe).

Two vocabularies are already constants your tests can loop, and the test
project sees internals: `DealDesk.Domain.Lifecycle.All` (five statuses) and
`DealDesk.Api.Vocabulary.ReconCategories` (eight categories).

The page names exactly two `/api/` prefixes — `/api/appraisals` and
`/api/reports` — because every other path is built by appending to one of them.

## Helpers you will reuse

`internal static` in `DeskPageSmokeTests.cs`, so call them as
`DeskPageSmokeTests.X`:

- `PageRoute` — the string `"/"`.
- `PageAsync(client)` — GETs the page, insists it succeeded, returns the body
  as a string. Use as
  `using var factory = new DeskAppFactory();`,
  `using var client = factory.CreateClient();`,
  `var page = await DeskPageSmokeTests.PageAsync(client);`.
- `PanelIds` — the four section ids above.
- `ReadRoutes` — the twelve GET routes the page's script reads, already
  written against seeded ids (`/api/appraisals/1/offer`, and so on).

`SeedSmokeTests.SeededClient(factory)` gives a client onto a database holding
the demo month. `OpsSmokeTests.DemoToken` is the invented bearer token, and
`new DeskAppFactory(OpsSmokeTests.DemoToken)` arms the guard.

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §I4 — How the root route behaves

Create `tests/DealDesk.Tests/DeskPageApiTests.cs`. Mirror
`tests/DealDesk.Tests/MetricsApiTests.cs`: one `[Fact]` per behaviour, each
opening its own `DeskAppFactory` and client.

`DeskPageSmokeTests` already asserts that the page is served and is one whole
document. This file asserts how the ROUTE behaves around it.

Assert these six:

1. The content type names HTML and UTF-8: the response's
   `Content.Headers.ContentType` has `MediaType` `text/html` and `CharSet`
   `utf-8`.
2. Two GETs of `/` return byte-identical bodies. The page is held in memory
   after the first read, and a page that drifted between reads would be a
   caching bug.
3. The page is not a catch-all: `GET /desk`, `GET /index.html` and
   `GET /api/nope` each answer 404. Only the exact root is the page.
4. The page is open when the token is armed. Boot
   `new DeskAppFactory(OpsSmokeTests.DemoToken)`, send no `Authorization`
   header, and `GET /` still answers 200 — a reader is never guarded.
5. The page is counted like any other request. After one `GET /`, use
   `OpsSmokeTests.ScrapeUntilAsync` to wait for a `/metrics` scrape containing
   `dealdesk_http_requests_total{method="GET",status="200"}`. Both observers
   record after the response, which is why you poll rather than read once.
6. The page is not empty and not truncated: its length is over 10_000
   characters, and it contains both `</style>` and `</script>`.

Gate: the four commands above. Commit the one new test file.

---

## §I5 — The page fetches nothing from anywhere

Create `tests/DealDesk.Tests/DeskPageSelfContainedTests.cs`. Mirror
`tests/DealDesk.Tests/DeskPageSmokeTests.cs` for structure. One factory and
client per fact; read the body with `DeskPageSmokeTests.PageAsync`.

This is the file that pins SPEC.md's hardest rule about feature 6. dealdesk
makes zero outbound requests, and a page that pulled a stylesheet, a font or a
script from somewhere would break that in the one place a reviewer looks —
their browser's network tab. Every assertion is a `DoesNotContain` over the
served body, so it stays true however the page is later edited.

Assert these six:

1. No scheme anywhere: the body contains no `://`. That one substring covers
   every CDN, every font host and every absolute link at once.
2. No external script: no `<script src` (compare case-insensitively).
3. No external stylesheet: no `<link ` and no `@import` (case-insensitive).
4. No web font and no image: no `@font-face`, no `<img`, no `srcset`
   (case-insensitive).
5. Exactly one of each inline block. Counting occurrences of `<style>`,
   `</style>`, `<script>` and `</script>` gives 1 every time.
6. The behaviour really is inline: the body contains `document.getElementById`
   and `fetch(`, so the page is driven by its own script rather than by markup
   that would need one fetched from elsewhere.

Counting a substring: `page.Split("<style>").Length - 1`, or a loop over
`IndexOf` — either is fine. Use `StringComparison.Ordinal` for the exact
spellings and `OrdinalIgnoreCase` for the tag names.

Gate: the four commands above. Commit the one new test file.

---

## §I6 — The four panels, and the words on them

Create `tests/DealDesk.Tests/DeskPagePanelTests.cs`. Mirror
`tests/DealDesk.Tests/DeskPageSmokeTests.cs`. One factory and client per fact;
read the body with `DeskPageSmokeTests.PageAsync`.

The smoke test already asserts the four `<section id="…">` panels exist. This
file asserts that each one carries what SPEC.md feature 6 says it carries — a
panel with the right id and none of the right controls would pass the smoke
test and still be an empty page.

Assert these six:

1. The panels appear in reading order: `IndexOf` of `<section id="appraisals">`
   is less than `worksheet`, which is less than `audit`, which is less than
   `reports`. All four are found (no `-1`).
2. Every lifecycle status is offered as a filter. For each value in
   `DealDesk.Domain.Lifecycle.All`, the body contains
   `<option value="draft">` — the same shape with that status's word. All five,
   so the list panel can show every state.
3. Every recon category is typeable. For each value in
   `DealDesk.Api.Vocabulary.ReconCategories`, the body contains
   `<option>mechanical` — the same shape with that category's word. All eight.
4. The offer math is on the page with its derivation: the body contains
   `<tbody id="derivation">` and `<strong id="recommended">`, and the four
   step labels `Market anchor (comp average)`, `Less recon estimate`,
   `Less pack` and `Less target front gross`.
5. The audit panel names the trail's columns. Inside the body are the header
   cells `<th>Field</th>`, `<th>From</th>`, `<th>To</th>`, `<th>By</th>` and
   `<th>Reason</th>`.
6. All three reports have a table: the body contains
   `<table id="look-to-book">`, `<table id="recon-variance">` and
   `<table id="front-gross">`.

Gate: the four commands above. Commit the one new test file.

---

## §I7 — The page and the API name the same routes

Create `tests/DealDesk.Tests/DeskPageRouteTests.cs`. Mirror
`tests/DealDesk.Tests/DeskPageSmokeTests.cs`.

A page can only be trusted if the routes it names are the routes that exist.
The smoke test proves the twelve read routes answer; this file closes the loop
in the other direction — the page names nothing else, and its writes land where
it says they do.

Use `System.Text.RegularExpressions.Regex.Matches(page, "/api/[a-z0-9-]+")` to
collect the path fragments the page mentions.

Assert these six:

1. The page mentions exactly two distinct `/api/` fragments, and they are
   `/api/appraisals` and `/api/reports`. Every deeper path is built by
   appending to one of those two.
2. The routes the page reads are spelled out in it. All three report paths
   appear whole — `/api/reports/look-to-book`, `/api/reports/recon-variance`,
   `/api/reports/front-gross` — and each per-worksheet suffix appears as its
   own literal: `/walk-items`, `/recon-lines`, `/comps`, `/offer-inputs`,
   `/offer`, `/recon-variance`, `/audit`.
3. The write paths the page names really accept writes. On a seeded client
   (`SeedSmokeTests.SeededClient`), `POST /api/appraisals/1/walk-items` with
   area `exterior` answers 201.
4. Same for the store numbers: `PUT /api/appraisals/1/offer-inputs` with
   `pack` 90000 and `targetGross` 150000 answers 200.
5. Same for the lifecycle: `POST /api/appraisals/5/status` with status
   `appraised`, a `changedBy` and a `reason` answers 200. Worksheet 5 is a
   draft in the demo month, so the move is legal.
6. The page reads back what it wrote. In one fact, on one seeded client: POST a
   walk item to worksheet 1, then `GET /api/appraisals/1/walk-items` returns 3
   entries — the seed wrote two.

Post JSON with `client.PostAsJsonAsync` (`System.Net.Http.Json`), as
`WalkItemApiTests.cs` does.

Gate: the four commands above. Commit the one new test file.

---

## §I8 — Document the desk page in the README

Edit `README.md`. Three changes, nothing else touched.

**1.** In the `## The API` list, add one bullet as the FIRST entry, above the
`GET /healthz` line. It names the route `GET /` and describes it as the desk
page: worksheet, appraisal list, audit trail and the three reports. Match the
formatting of the bullets already there.

**2.** Right under the `curl http://localhost:5000/healthz` block in
`## Quickstart`, add one sentence: open `http://localhost:5000/` in a browser
for the desk page. Do not add a fenced block for it — `verify.sh` lints paths
inside fences, and a URL is not a repo path.

**3.** After the demo-month paragraph (the one beginning
"`dotnet run --project src/DealDesk -- seed` writes a demo month"), add ONE new
paragraph, no more. Say: `GET /` serves the desk page — one hand-written HTML
file with its CSS and its JavaScript inline, no framework, no build step, no
CDN and no web font, so the page makes no request to anywhere but this service.
Say it shows the appraisal list with lifecycle states, one worksheet with its
walk, recon lines, comps and store numbers, the offer derivation recomputed
live as the desk types, the audit trail, and all three reports. Say the live
number is a preview and `GET .../offer` on the server is the authority. Say
money is typed in dollars and travels as whole cents.

Do NOT change the healthz sample output — this phase adds no migration, so
`005_reports` is still current. Promise nothing unbuilt: deploy packaging
(single-file publish, unit file, CI) and `docs/PROCESS.md` are still open.

Gate: `bash verify.sh`. Commit `README.md` only.

---

## §I9 — Close Phase I

Three small things, one commit. Mirror what §H9 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it.

**2. Append a `## Phase I` section to STATUS.md** — append only, never rewrite
what is above it. A few lines: `GET /` serves the desk page, one self-contained
HTML file with inline CSS and JS and zero external requests, carrying the
appraisal list with its lifecycle states, a worksheet with walk, recon, comps
and store numbers whose offer derivation recomputes live as the desk types, the
audit trail, and all three reports. Give the test count from your own
`dotnet test` run. Then say what is still NOT built: deploy packaging
(single-file publish, example unit file, CI) and `docs/PROCESS.md`.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 6 → `SHIPPED`, phase `I`, note: `GET /` — self-contained page: list,
  worksheet with live offer math, trail, reports; hero screenshot deferred.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
