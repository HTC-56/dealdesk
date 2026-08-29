# Roadmap — the v1 scoreboard

One row per SPEC.md feature. The planning lane keeps status current; row edits here
are the one permitted exception to append-only docs.

| # | Feature (SPEC.md) | Status | Phase | Note |
|---|---|---|---|---|
| 1 | .NET 8 minimal API + SQLite/Dapper + migrator | SHIPPED | A | scaffold, migrator, Money, 001_init, Phase A gates |
| 2 | Appraisal worksheet + offer math with visible derivation | SHIPPED | B–C | worksheet, child collections and the offer endpoint with its visible derivation |
| 3 | Lifecycle + append-only audit trail | SHIPPED | D | lifecycle rules, audited status moves and field revisions, append-only trail served newest-first |
| 4 | Recon actuals + variance | SHIPPED | E | line-by-line actuals with credits, and variance served per line, by category and per worksheet |
| 5 | The three reports (look-to-book, recon variance, gross by appraiser) | SHIPPED | F | all three routes with per-appraiser/category rollup, basis-point rates, store totals from rows |
| 6 | The desk page (self-contained) | SHIPPED | I | `GET /` — self-contained page: list, worksheet with live offer math, trail, reports; hero screenshot deferred |
| 7 | Ops surface (/healthz, /metrics, ledger, bearer auth) | SHIPPED | A, G | /metrics, the JSONL ledger and the bearer token |
| 8 | Seeded demo data | SHIPPED | H | seed.sql, Seeder, the `seed` verb; a demo month of twelve worksheets |
| 9 | Deploy-grade packaging (single-file publish, unit file, CI, quickstart) | SHIPPED | A, J | single-file publish, example unit and env file, CI on four gates; README quickstart from A4 |
| — | docs/PROCESS.md (the loop story) | SHIPPED | J | the loop story, with the ledger counted rather than guessed |

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
- **The anchor is the comp arithmetic mean** (Phase B,
  `src/DealDesk/Domain/OfferMath.cs`). SPEC.md names hand-entered comps as
  "the anchor" without naming the reduction. Taken as their mean to the
  nearest cent, halves away from zero — the least surprising reading — with a
  per-worksheet `anchor_override` column so the desk can type a different
  number instead. Revisit if a playtest says the desk wants a median or a
  low-comp rule.
- **Money columns carry no `_cents` suffix** (Phase B,
  `sql/002_worksheet.sql`). `estimate`, `price`, `pack`, `target_gross`,
  `anchor_override` are INTEGER cents; the bare names let snake_case matching
  land them directly on `Money` properties, keeping the no-`AS`-alias rule
  above.
- **The recon category vocabulary is fixed in the schema** (Phase B,
  `sql/002_worksheet.sql`). A CHECK constraint, not free text, because
  feature 5's recon-variance report groups by category and free text would
  make that report meaningless.
- **Phase B write endpoints are unauthenticated** (Phase B,
  `src/DealDesk/Api/AppraisalEndpoints.cs`). The static bearer token on writes
  is feature 7; `POST /api/appraisals` is open until that ships, and no other
  phase may treat that as precedent.
- **API money fields are integer cents with no suffix** (Phase C,
  `src/DealDesk/Api/WorksheetDtos.cs`). The wire carries `estimate`, `price`,
  `pack`, `targetGross`, `anchorOverride`, `recommended` as whole cents, not
  dollars: a JSON number is a double in a browser, and `1234.56` would not
  survive the round trip the offer math exists to make exact. The API records
  use `long` rather than `Money` for these — cents-at-the-boundary means no
  JSON converter and no `Money`-shaped `{"cents":…}` objects in the payload.
- **An unpriceable worksheet is 409, not 400** (Phase C,
  `src/DealDesk/Api/OfferEndpoints.cs`). `GET .../offer` on an appraisal with
  neither a comp nor an anchor override returns 409 Conflict: the request is
  well-formed, the worksheet is simply not ready to price. Every other refusal
  in the repo is a 400 from a `Validate()` message.
- **Offer inputs upsert, and read as zeros when absent** (Phase C,
  `src/DealDesk/Api/OfferEndpoints.cs`). `offer_input` is one row per
  appraisal, so the write is `PUT` with `ON CONFLICT … DO UPDATE` rather than a
  `POST` that could duplicate. A worksheet the desk has never priced reads back
  pack 0 / target 0 rather than 404 — the appraisal exists, its store numbers
  are just untyped.
- **`lost` is reachable from every open state** (Phase D,
  `src/DealDesk/Domain/Lifecycle.cs`). SPEC.md draws the chain
  draft → appraised → presented → won | lost without saying where `lost`
  branches from. Taken as: any open state may go to `lost` (a deal dies
  whenever the customer walks), but `won` only follows `presented` — nobody
  buys a vehicle they were never shown an offer on. Both are terminal and a
  move to the state already held is refused, since it would write no audit row.
- **The audit table has no cascade, and cannot be rewritten** (Phase D,
  `sql/003_audit.sql`). Two BEFORE triggers abort UPDATE and DELETE, so the FK
  to `appraisal` deliberately omits `ON DELETE CASCADE` — deleting an appraisal
  that has a trail fails rather than taking the evidence with it. Every other
  child table in the repo cascades. dealdesk exposes no delete route, so no
  endpoint changes behaviour; revisit only if feature 9 ever needs a purge.
- **The trail records one row per field, valued as text** (Phase D,
  `sql/003_audit.sql`). One `audit_entry` row per changed FIELD rather than per
  request, so a later report can ask "when did this number move" of one column.
  `old_value`/`new_value` are TEXT whatever the source column's type is,
  because one trail carries a status word, an odometer integer and a person's
  name; nothing downstream does arithmetic on an audit value. `field` holds the
  camelCase API name (`modelYear`, not `model_year`) — the trail names what the
  caller sent.
- **VIN and status are not PATCHable; a closed worksheet still is** (Phase D,
  `src/DealDesk/Api/AuditDtos.cs`). `PATCH /api/appraisals/{id}` covers
  modelYear, make, model, trimLevel, miles and appraiser. VIN is the vehicle's
  identity — correcting it means the worksheet was opened on the wrong car —
  and status moves through the lifecycle route, which checks the transition.
  Edits to a `won`/`lost` worksheet are allowed rather than refused: SPEC.md
  states no such rule, and the trail records who changed a closed file and why.
- **Variance is `actual − estimate`, positive meaning over** (Phase E,
  `src/DealDesk/Domain/ReconVariance.cs`). SPEC.md names "variance (estimate
  vs actual)" without fixing the sign. Taken as actual minus estimate, so a
  positive number reads the way a used-car director says it out loud — "we are
  four hundred over on paint". Fixed in one place and used unchanged at every
  level (line, category, worksheet), so no two numbers on the page can
  disagree about which direction is bad.
- **Actuals post against a recon LINE, never bare against the appraisal**
  (Phase E, `sql/004_recon_actual.sql`). SPEC.md says actuals post
  "line-by-line", so the FK points at `recon_line` and there is no
  appraisal-level posting. Work nobody estimated is entered as a recon line
  with an estimate of 0 and posted against normally — the trail still shows
  what was expected alongside what was spent. Many postings per line, because
  one repair order is rarely one invoice.
- **A posting's amount is signed but never zero** (Phase E,
  `sql/004_recon_actual.sql`). `recon_line.estimate` is constrained
  non-negative; `recon_actual.amount` deliberately is not, because
  `OfferMath.cs` already recorded that a recon credit belongs in the actuals
  rather than in an estimate line. A returned part posts negative. Zero is
  refused by a CHECK, for the same reason `audit_entry` refuses a row whose
  value did not move.
- **A worksheet with no recon lines reports zeros, not a 409** (Phase E,
  `src/DealDesk/Api/ReconEndpoints.cs`). `GET .../recon-variance` on an
  appraisal with no recon lines returns 200 and zeros, unlike `GET .../offer`,
  which 409s with no anchor. An empty sum is genuinely zero; an anchor cannot
  be invented from no comps. `unpostedLines` is what tells a reader recon is
  unfinished, so a zero total is never mistaken for a finished one.
- **The category rollup is ordered by category name** (Phase E,
  `src/DealDesk/Domain/ReconVariance.cs`). Ordinal by name rather than by
  entry order or by size, so two worksheets carrying the same categories
  report them in the same order however their lines were typed — feature 5's
  recon-variance report reads across worksheets and needs that stability.
- **Look-to-book rates booked against APPRAISED, read off the audit trail**
  (Phase F, `sql/005_reports.sql`). SPEC.md names the report "appraised vs
  won" without saying which count is the denominator. Taken as booked ÷
  appraised — a worksheet abandoned in draft was never a real look at the car
  — with `looked` carried beside it so a reader can see both. Whether a
  worksheet ever reached `appraised` is not always readable from its current
  status, because `lost` is reachable straight from `draft`; for those the
  view asks `audit_entry` whether the status ever moved into `appraised`.
  That is the first report to depend on the trail, and the reason it records
  one row per field with the new value in it.
- **A rate on the wire is basis points, never a fraction** (Phase F,
  `src/DealDesk/Api/ReportDtos.cs`). `bookRateBps` of `2500` is 25.00%.
  Integer hundredths of a percent for the same reason money is integer cents:
  a JSON number is a double in a browser. Truncated toward zero, and a whole
  of zero answers 0 rather than throwing — an appraiser with nothing
  appraised has no rate yet, which is a fact about the month.
- **Front gross is the PLANNED gross less recon overage** (Phase F,
  `sql/005_reports.sql`). SPEC.md names "front gross by appraiser", but its
  non-goals refuse desking beyond the trade worksheet, so dealdesk holds no
  retail selling price to subtract a cost from. Taken as
  `offer_input.target_gross` — the number the offer math already subtracts to
  reach the recommended trade value — minus the worksheet's recon variance,
  over won worksheets only. `unposted_lines` travels with it, because a
  projection built on recon that has not finished posting is provisional.
  Revisit if a playtest says the desk wants a realised gross instead, which
  would need a selling price this v1 does not collect.
- **The reports do not redefine variance** (Phase F, `sql/005_reports.sql`).
  `report_recon_variance` and `report_front_gross` both use
  `actual − estimate` over every line, exactly as Phase E fixed it, so an
  unposted line reads as a full-estimate negative in the report just as it
  does on the worksheet. Restricting the report to posted lines would have
  read better but would have put two variance definitions in the repo;
  `unposted_lines` is carried beside the money instead.
- **An unset token leaves writes open** (Phase G,
  `src/DealDesk/Ops/BearerAuth.cs`). SPEC.md names "static bearer token on
  write endpoints" without saying what an unconfigured deployment does. Taken
  as: no `Auth:Token`, no guard — a fresh clone runs the README's five-minute
  path with no configuration step, exactly as it has since Phase B, and a
  deployment that sets the value gets the guard. Refusing every write until
  something is configured would mean the quickstart opened with a token to
  invent. Both a missing and a wrong token answer 401 rather than 403: the
  credential is what is wrong, and dealdesk has exactly one to offer. Revisit
  if the publish decision ever puts this on a network that is not localhost.
- **Metric labels carry no request path** (Phase G,
  `src/DealDesk/Ops/OpsMetrics.cs`). Series are labelled by method and status
  only. `/api/appraisals/{id}` as a label would mint a time series per
  worksheet, and unbounded cardinality is how a hand-rolled scrape target
  turns into memory the process never gives back. The path is kept on every
  line of the JSONL ledger instead, where growth is a file on disk — that
  division is the reason the repo carries both.
- **The ledger observes and never refuses** (Phase G,
  `src/DealDesk/Ops/OpsLedger.cs`). Write failures are swallowed: losing an
  ops line is not worth failing an appraisal the database has already
  committed. Both observers also record AFTER the response reaches the caller,
  so no request is ever delayed to be written down — which is why the ops
  tests poll rather than read once. Timestamps are `DateTimeOffset` round-trip
  UTC, matching `created_at` and the audit trail rather than inventing a
  second time format.
- **`/metrics` reads its gauges at scrape time and emits the zeros** (Phase G,
  `src/DealDesk/Api/OpsEndpoints.cs`). The HTTP counters come from memory, but
  `dealdesk_appraisals` is a `GROUP BY status` against SQLite on each scrape —
  a worksheet count that drifted from the database would be worse than one
  that costs a query. All five lifecycle statuses are emitted even at zero,
  because a series that vanishes when its count empties reads on a dashboard
  as a failed scrape rather than a store that sold nothing. `COUNT(*) AS
  count` is the one aliased column in the repo; every other query selects real
  column names.
- **The seed is embedded beside the migrations but is not one** (Phase H,
  `src/DealDesk/Data/Seeder.cs`). SPEC.md says "a seed script"; the csproj
  already embeds `sql/*.sql`, so `seed.sql` ships inside the assembly for the
  single-file publish exactly as the schema does — but `Migrator`'s name
  pattern only matches `NNN_*.sql`, so nothing applies it automatically. A
  migration must run on every database, including a real one; demo data must
  run on none of them unless somebody asks. Asking is
  `dotnet run --project src/DealDesk -- seed`.
- **Seeding is refused, not repeated** (Phase H, `src/DealDesk/Data/Seeder.cs`).
  SPEC.md does not say what a second run does. Taken as: a non-empty
  `appraisal` table means the seed writes nothing and returns 0. Two demo
  months on top of each other would make every report read as a store that did
  twice the business it did, and "already seeded" is the ordinary state of a
  database somebody has demoed — not an error worth throwing over.
- **Seeded ids are explicit, 1..12 and 1..15** (Phase H, `sql/seed.sql`).
  Because the script only ever runs into an empty table, the worksheets and
  recon lines carry literal ids. That is what lets the README quickstart name a
  worksheet, the desk page link one, and a test assert against one without
  first querying for its id.
- **The demo month is relative to the moment it is seeded** (Phase H,
  `sql/seed.sql`). Timestamps are `strftime` offsets from `now`, not fixed
  dates, so a demo run in any month reads as "this month" rather than as a
  fixed month receding into the past. The cost is that no test may assert an
  exact timestamp; §H4 asserts the window instead.
- **The `seed` verb is handled before the host builder** (Phase H,
  `src/DealDesk/Program.cs`). `WebApplication.CreateBuilder(args)` adds
  command-line configuration, which understands only `--switch value` pairs and
  refuses a bare verb. So the branch reads its database path from the
  environment through its own `ConfigurationBuilder` and returns before the
  host exists — which also means the seed never opens a port.
- **The page previews the offer math rather than trusting it** (Phase I,
  `src/DealDesk/Page/index.html`). SPEC.md feature 6 says "live offer math",
  which needs the number to move as the desk types — a round trip per
  keystroke would not be live. So the page re-implements `OfferMath.cs`'s four
  steps in its own script, and says in the line under the total which number
  is showing: a preview from the form, or the value `GET .../offer` last
  served. The server stays the only authority. Two copies of one piece of
  arithmetic is a real cost; the alternative was either a stale total or a
  fetch on every keypress, and the derivation is four labelled subtractions
  that the property tests already pin on the server side.
- **Dollars are typed, cents are sent** (Phase I,
  `src/DealDesk/Page/index.html`). Every money field on the page is entered in
  dollars and converted to whole cents by string arithmetic — a regex, a
  split and integer multiplication — never by `parseFloat`. The API's rule
  that a JSON number is a double in a browser is exactly as true inside the
  browser, so nothing this page renders was ever a floating-point dollar.
- **NULL RESULT — no hero screenshot is committed** (Phase I). SPEC.md
  feature 6 ends "The README hero screenshot", and feature 9 wants it in the
  README. The loop has no browser and cannot open the page, so a screenshot
  would be an image nobody in this lane could verify was of this page. §I8
  tells a reader to open `/` instead, and the binary is left for a human with
  a browser. This is the one part of feature 6 the loop did not ship.
- **The single-file publish is not trimmed** (Phase J,
  `src/DealDesk/DealDesk.csproj`). SPEC.md feature 9 asks for a single-file
  self-contained binary and says nothing about size. `PublishTrimmed` is
  explicitly `false`: Dapper materialises rows by reflection and the minimal
  API binds the DTO records the same way, so a trimmed publish would build
  clean, pass every gate, and fail at the first query — the worst failure mode
  available. The cost is a 47 MB executable, paid once at deploy time.
  `EnableCompressionInSingleFile` takes it there from 93 MB.
- **No runtime identifier is pinned; the operator names the platform** (Phase
  J, `src/DealDesk/DealDesk.csproj`). Every publish property sits in a
  PropertyGroup conditioned on `'$(RuntimeIdentifier)' != ''`, so
  `dotnet build`, `dotnet test` and `dotnet format` never build RID-specific
  and the three local gates are untouched by any of it. A pinned RID would
  have made the repo's gates platform-flavoured to serve one deployment.
  Verified end to end on arm64 — the published file seeds the demo month,
  migrates to `005_reports`, and serves `/healthz`, `/`, the offer derivation,
  all three reports and `/metrics`; the linux-x64 publish cross-compiles from
  the same command and CI is what runs it.
- **The example unit is `Type=exec`, never `notify`** (Phase J,
  `deploy/dealdesk.service`). dealdesk is a plain Kestrel host and does not
  signal readiness to systemd; a `notify` unit would sit there until it timed
  out. Signalling properly would mean taking
  `Microsoft.Extensions.Hosting.Systemd`, which SPEC.md's pre-registered
  dependency list does not name, and a deploy example is not worth widening
  the dependency surface for. The unit is told the truth instead.
- **The environment example arms nothing** (Phase J,
  `deploy/dealdesk.env.example`). `DEALDESK_Auth__Token` ships commented out
  rather than carrying a placeholder value: Phase G already recorded that an
  unset token leaves writes open so a fresh clone runs the README quickstart
  with no configuration step, and a shipped placeholder is a credential
  somebody forgets to change. Every other key in the file is set, and the
  smoke test asserts each one is a key `Program.cs` actually reads — an
  example that configures something the app ignores would be worse than no
  example.
- **The single file unpacks into the state directory, not /tmp** (Phase J,
  `deploy/dealdesk.env.example`). `IncludeNativeLibrariesForSelfExtract` means
  the host extracts the SQLite native library at first run;
  `DOTNET_BUNDLE_EXTRACT_BASE_DIR` points that at `/var/lib/dealdesk/bundle`,
  the one path the unit's `ReadWritePaths` allows. Left at its default it
  lands under a temporary directory that `PrivateTmp=true` makes per-boot, so
  every restart would re-extract 47 MB.
- **NULL RESULT — no CI badge is added to the README** (Phase J). SPEC.md's
  "done means" ends "CI badge green", and `.github/workflows/ci.yml` ships. A
  badge URL names the published repository, and flipping this repo public
  (with its name and its licence) is human-gated in DECISIONS.md. The workflow
  is committed and will run on the first push to a remote; the badge line is
  left for the human who names the repo. This is the second and last part of
  the spec the loop did not ship, after the hero screenshot.
- **`HealthzTests` asserts the newest available migration** (Phase B,
  `tests/DealDesk.Tests/HealthzTests.cs`). It hardcoded `001_`, which adding
  `002_worksheet.sql` would have dated; it now compares against
  `Migrator.Available()[^1]` so no future migration breaks it.
