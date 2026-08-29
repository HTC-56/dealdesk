# Phase G — the ops surface

**ROADMAP row: "7 — Ops surface (/healthz, /metrics, ledger, bearer auth)"**
(PARTIAL → SHIPPED at §G10). SPEC.md feature 7: the scrape target, the JSONL
ledger and the static bearer token that were deferred from Phase A onward.

## Already shipped by the planning lane

Commits `feat(G1)`…`feat(G4)` landed the hard code. §G5 starts green at **156
tests**. What exists now, on top of Phase F:

- `src/DealDesk/Ops/OpsMetrics.cs` — the counter map and the Prometheus text
  renderer. `Observe`, `RequestCount`, `DurationMs`, `Render`, the static
  `Gauge`, and the internal `Escape`.
- `src/DealDesk/Ops/OpsLedger.cs` — the JSONL ledger. `Enabled`, `Path`,
  `Append` (with and without an explicit instant), and the internal static
  `Line`.
- `src/DealDesk/Ops/OpsRecording.cs` and `src/DealDesk/Ops/BearerAuth.cs` — the
  two middlewares, wired in `Program.cs`.
- `src/DealDesk/Api/OpsEndpoints.cs` — `GET /metrics`.
- `tests/DealDesk.Tests/OpsSmokeTests.cs` — four end-to-end facts and four
  `internal static` helpers the tasks below reuse.

**You add no source code in this phase.** §G5–§G8 are test files, §G9 is the
README, §G10 closes. Every type above is finished.

**Namespaces.** `OpsMetrics`, `OpsLedger` and `BearerAuth` are in
`DealDesk.Ops`. The test project already sees internals — no extra setup.

**The three rules of this surface**, restated so no task has to chase them:

1. Metrics label method and status, never the path. The ledger keeps the path
   on every line instead.
2. Reads are always open. Only POST, PUT, PATCH and DELETE are guarded, and
   only when a token is configured — `new DeskAppFactory()` arms nothing.
3. Both observers record AFTER the response reaches the caller. A test that
   reads once can read one request early, so use the polling helpers.

**Four helpers you will reuse** (`internal static` in `OpsSmokeTests.cs`, so
call them as `OpsSmokeTests.X`):

- `DemoToken` — the invented token, `"desk-demo-token"`.
- `NewWorksheet()` — a body good enough for `POST /api/appraisals`.
- `ScrapeUntilAsync(client, predicate)` — scrapes `/metrics` until the text
  satisfies the predicate; returns the text. Pass `_ => true` to scrape once.
- `LedgerLinesAsync(factory, atLeast)` — the ledger file's lines, once it holds
  at least that many.

`DeskAppFactory` now takes an optional token — `new DeskAppFactory(token)` —
and exposes `LedgerPath`.

Invented demo names already in use: appraisers `A. Whitfield` and
`B. Ferreira`, `changedBy` `D. Okonjo`, `postedBy` `R. Vasquez`. Use those.

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §G5 — Metric arithmetic tests

Create `tests/DealDesk.Tests/OpsMetricsTests.cs`. Mirror
`tests/DealDesk.Tests/ReportRateTests.cs`: one `[Fact]` per law, plain
`Assert`, no database and no HTTP. `using DealDesk.Ops;` and `using Xunit;`.

Each fact makes its own `var metrics = new OpsMetrics();` — a shared instance
would let one fact's counts leak into another's.

Assert these six:

1. A fresh `OpsMetrics` reports `RequestCount("GET", 200)` of 0 and
   `DurationMs("GET", 200)` of 0, and its `Render()` still contains
   `# TYPE dealdesk_http_requests_total counter` — an empty family still
   declares itself.
2. Two `Observe("GET", 200, 5)` calls leave `RequestCount("GET", 200)` at 2 and
   `DurationMs("GET", 200)` at 10. The count and the milliseconds are separate
   totals over the same series.
3. `Observe("get", 200, 1)` and `Observe("GET", 200, 1)` land on ONE series:
   `RequestCount("GET", 200)` is 2. The method is upper-cased before it is
   counted.
4. Status separates series. After `Observe("GET", 200, 1)` and
   `Observe("GET", 404, 1)`, `RequestCount("GET", 200)` is 1, and `Render()`
   contains both a `status="200"` line and a `status="404"` line.
5. `Render()` is ordered by method then status: observe `POST` first and `GET`
   second, then assert the index of `method="GET"` in the text is LESS than the
   index of `method="POST"`. Two scrapes of an unchanged process must read the
   same way.
6. `OpsMetrics.Gauge("dealdesk_test", "help text", "status",
   new[] { KeyValuePair.Create("draft", 2L) })` contains
   `# TYPE dealdesk_test gauge` and the line `dealdesk_test{status="draft"} 2`.
   Separately, `OpsMetrics.Escape("a\"b")` returns `a\"b` — a quote in a label
   value is backslash-escaped.

Gate: the four commands above. Commit the one new test file.

---

## §G6 — Ledger file tests

Create `tests/DealDesk.Tests/OpsLedgerTests.cs`. Mirror
`tests/DealDesk.Tests/ReportRateTests.cs` for shape — one `[Fact]` per law, no
HTTP — but these facts touch a file.

Give each fact its own throwaway directory:

```
var dir = Path.Combine(Path.GetTempPath(), "dealdesk-ledger-" + Guid.NewGuid().ToString("N"));
```

and clean it up in a `finally` with `Directory.Delete(dir, recursive: true)`
inside `try { } catch (IOException) { }` — the same way `TempDb.cs`'s
`Dispose` does. Never let cleanup fail a test.

Use a fixed instant so nothing depends on a clock:
`var at = new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.FromHours(2));`

Read lines back with `File.ReadAllLines(path)` and parse one with
`JsonDocument.Parse(line)`.

Assert these six:

1. `new OpsLedger(null)` and `new OpsLedger("   ")` both report `Enabled`
   false and a null `Path`, and calling `Append("GET", "/healthz", 200, 3)` on
   one throws nothing and creates no file.
2. A ledger on a real path appends rather than rewrites: after one `Append` the
   file holds 1 line; after a second it holds 2, and the first line is
   unchanged.
3. `OpsLedger.Line("GET", "/healthz", 200, 3, at)` parses as JSON whose
   `method` is `"GET"`, `path` is `"/healthz"`, `status` is `200` and `ms` is
   `3`.
4. That same line's `ts` starts with `"2026-08-29T10:00:00"` — the instant was
   handed over at 12:00 with a +02:00 offset, and the ledger records UTC.
5. A path carrying a double quote survives: `OpsLedger.Line("GET",
   "/api/x\"y", 200, 1, at)` still parses as JSON and its `path` reads back as
   `/api/x"y`. The line is serialised, not concatenated.
6. `Append` creates a missing parent directory — point the ledger at
   `Path.Combine(dir, "nested", "ledger.jsonl")`, append three times, and
   assert the file has 3 lines and every one of them parses as JSON.

Gate: the four commands above. Commit the one new test file.

---

## §G7 — The scrape over HTTP

Create `tests/DealDesk.Tests/MetricsApiTests.cs`. Mirror
`tests/DealDesk.Tests/OpsSmokeTests.cs` for structure — `using var factory =
new DeskAppFactory();`, `using var client = factory.CreateClient();`, and
`OpsSmokeTests.ScrapeUntilAsync` for every scrape.

Build worksheets with `ReportSmokeTests.CreateAsync(client, "A. Whitfield")`
and `ReportSmokeTests.MoveAsync(client, id, "appraised", "presented", "won")`.

Each test gets its own factory, so no test sees another's counts.

Assert these six. Where a count is expected, pass a predicate that waits for
it — e.g. `scrape => scrape.Contains("…status=\"404\"} 1",
StringComparison.Ordinal)` — then assert on the returned text:

1. `GET /metrics` answers 200, its `Content.Headers.ContentType?.MediaType` is
   `"text/plain"`, and the header's `ToString()` contains `version=0.0.4`.
2. A miss is counted under its own status: after
   `GET /api/appraisals/9999` (a 404), the scrape contains
   `dealdesk_http_requests_total{method="GET",status="404"} 1`.
3. A write is counted under its own method: after one `CreateAsync`, the scrape
   contains `dealdesk_http_requests_total{method="POST",status="201"} 1`.
4. The gauge falls as well as rises. Create one worksheet and walk it to `won`,
   then assert the scrape contains `dealdesk_appraisals{status="draft"} 0` AND
   `dealdesk_appraisals{status="won"} 1`.
5. The duration family carries the same series as the count family: the scrape
   contains a
   `dealdesk_http_request_duration_ms_total{method="GET",status="200"}` line.
6. A scraper needs no token: with `new DeskAppFactory(OpsSmokeTests.DemoToken)`
   the `/metrics` request still answers 200.

Gate: the four commands above. Commit the one new test file.

---

## §G8 — The bearer token over HTTP

Create `tests/DealDesk.Tests/BearerAuthApiTests.cs`. Mirror
`tests/DealDesk.Tests/OpsSmokeTests.cs` — same factory shape, same
`JsonDocument` reads.

Boot the guarded app with `using var factory = new
DeskAppFactory(OpsSmokeTests.DemoToken);` and post
`OpsSmokeTests.NewWorksheet()` bodies. Attach the token with:

```
client.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", OpsSmokeTests.DemoToken);
```

(`using System.Net.Http.Headers;`). To send a deliberately wrong header, assign
a different value to that same property.

Assert these six:

1. `POST /api/appraisals` with no `Authorization` header is 401, and the JSON
   body's `error` is a non-empty string.
2. The same POST carrying the token `"not-the-token"` is 401 too — a wrong
   credential is refused exactly like a missing one.
3. The guard is not POST-only. With the token attached, create a worksheet
   (201); then clear the header and `PUT /api/appraisals/{id}/offer-inputs`
   with `new { pack = 0, targetGross = 150_000 }` — that is 401. Re-attach the
   token and the same PUT succeeds.
4. Reads stay open. With no header at all,
   `PATCH /api/appraisals/{id}` (body `new { miles = 80_000, changedBy =
   "D. Okonjo", reason = "odometer" }`) is 401, while
   `GET /api/appraisals/{id}` is 200.
5. A near miss is still a miss: the token `"desk-demo-token-extra"` is 401
   even though it starts with the right value. So is the right value sent under
   the wrong scheme — `new AuthenticationHeaderValue("Basic",
   OpsSmokeTests.DemoToken)`.
6. An unset token leaves writes open: with `new DeskAppFactory()` and no header
   at all, the same `POST /api/appraisals` answers 201.

Gate: the four commands above. Commit the one new test file.

---

## §G9 — Document the ops surface in the README

Edit `README.md`. Two small changes, nothing else touched.

**1.** In the existing `## The API` bullet list, add one line directly after
the `GET /healthz` line:

- `GET /metrics` — Prometheus text: request counters and worksheet counts by status

**2.** After the existing "**Three reports summarise the store.**" paragraph,
add ONE new paragraph, no more:

- **The ops surface is three things.** `GET /metrics` serves hand-rolled
  Prometheus text — request counters labelled by method and status, and a
  `dealdesk_appraisals` gauge counting worksheets by lifecycle status. Every
  request also appends one JSON line to the ops ledger (`ledger.jsonl` by
  default, moved with `DEALDESK_Ops__LedgerPath`), which keeps the request path
  the metric labels deliberately leave out. Setting `DEALDESK_Auth__Token`
  arms a static bearer token on writes: POST, PUT, PATCH and DELETE then need
  an `Authorization: Bearer` header and answer 401 without one, while reads,
  `/healthz` and `/metrics` stay open. Leave it unset and writes are open, as
  the quickstart above runs them.

Do NOT change the healthz sample output — this phase adds no migration, so
`005_reports` is still current. Promise nothing unbuilt: no desk page, no
seeded demo data, no deploy packaging.

`verify.sh` lints README code blocks: any `./x`, `bash x` or `--project x`
path inside a fence must exist in the repo. Add no such new reference.

Gate: `bash verify.sh`. Commit `README.md` only.

---

## §G10 — Close Phase G

Three small things, one commit. Mirror what §F11 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it.

**2. Append a `## Phase G` section to STATUS.md** — append only, never rewrite
what is above it. A few lines: `/metrics` serves hand-rolled Prometheus text
with request counters and a worksheet gauge; every request appends a JSON line
to the ops ledger, which keeps the path the metric labels omit; a static bearer
token guards POST, PUT, PATCH and DELETE once `DEALDESK_Auth__Token` is set,
and reads stay open; an unset token leaves writes open so a fresh clone still
runs its quickstart. Give the test count from your own `dotnet test` run. Then
say what is still NOT built: the desk page, seeded demo data, and deploy
packaging.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 7 → `SHIPPED`, phase `A, G`, note: /healthz, hand-rolled /metrics, JSONL
  ops ledger, static bearer token on writes.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
