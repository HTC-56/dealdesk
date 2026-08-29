# Loop tasks

Ordered; each is one short session. Work the first unchecked box. Each task is
fully specced in ONE greppable section of its phase doc (`TASK_PHASE_A.md` §A1,
§A2, …) — grep your section, read it, build it.

*(Phase A is open — see below.)*


Completed phases through **Phase H** are in `TODO_ARCHIVE.md` — do not read it unless a task names it.
## Phase I: the desk page — see TASK_PHASE_I.md

Phases A–H above are CLOSED and kept only as style references. **Phase I is the
open one.** `src/DealDesk/Page/index.html`, `Api/PageEndpoints.cs`, the csproj
embed and `tests/DeskPageSmokeTests.cs` are already committed
(`feat(I1)`–`feat(I3)`); the tree is green at 212 tests. **Gate for every task:
`dotnet build -warnaserror`, `dotnet test`, `dotnet format
--verify-no-changes`, `bash scripts/scrub-check.sh`.** You add no `src/` code
this phase and you never edit `index.html`. The page is served at `/` as
`text/html`; its four panels are `<section id="…">` elements named
`appraisals`, `worksheet`, `audit`, `reports`. Reuse
`DeskPageSmokeTests.PageAsync()`, `.PanelIds` and `.ReadRoutes`.

- [x] I4 — Create `tests/DealDesk.Tests/DeskPageApiTests.cs`: six facts about
  the root route (content type, two identical GETs, three 404s, open under the
  token, counted in `/metrics`, not truncated). Mirror `MetricsApiTests.cs`.
  Spec: §I4.
- [x] I5 — Create `tests/DealDesk.Tests/DeskPageSelfContainedTests.cs`: six
  `DoesNotContain` facts pinning zero external requests (no `://`, no script
  src, no link/@import, no font/img, one inline block of each, inline
  behaviour). Mirror `DeskPageSmokeTests.cs`. Spec: §I5.
- [x] I6 — Create `tests/DealDesk.Tests/DeskPagePanelTests.cs`: six facts that
  each panel carries what SPEC feature 6 says (panel order, five statuses,
  eight recon categories, the derivation, the trail columns, three report
  tables). Mirror `DeskPageSmokeTests.cs`. Spec: §I6.
- [x] I7 — Create `tests/DealDesk.Tests/DeskPageRouteTests.cs`: six facts
  pairing page to API (exactly two `/api/` prefixes, the route literals, three
  writes that land, one read-back). Mirror `DeskPageSmokeTests.cs`. Spec: §I7.
- [x] I8 — Edit `README.md`: add `GET /` as the first `## The API` bullet, one
  browser sentence in the quickstart, and one desk-page paragraph. The healthz
  sample stays `005_reports`. Gate: `bash verify.sh`. Spec: §I8.
- [x] I9 — Run `bash verify.sh`, append a `## Phase I` section to STATUS.md,
  and flip ROADMAP row 6 to SHIPPED / phase I. Closes Phase I. Gate:
  `bash verify.sh`. Spec: §I9.

## Phase J: deploy-grade packaging — see TASK_PHASE_J.md

Phases A–I above are CLOSED and kept only as style references. **Phase J is the
open one**, and it closes the project. The csproj publish properties,
`deploy/dealdesk.service`, `deploy/dealdesk.env.example`,
`.github/workflows/ci.yml` and `tests/DealDesk.Tests/PackagingSmokeTests.cs`
are already committed (`feat(J1)`–`feat(J4)`); the tree is green at 240 tests.
**Gate for every task: `dotnet build -warnaserror`, `dotnet test`, `dotnet
format --verify-no-changes`, `bash scripts/scrub-check.sh`.** You add no `src/`
code this phase and you never edit those four artifacts. §J5–§J8 read files off
disk rather than over HTTP — no factory, no client, no `async`. Reuse
`PackagingSmokeTests.ReadRepoFile()`, `.SettingLines()`, `.ConfigurationKeys()`
and its path constants.

- [x] J5 — Create `tests/DealDesk.Tests/PublishProfileTests.cs`: six facts
  about the publish shape read off the csproj (the five properties sit inside
  the RID-conditioned group, no pinned RID, no trim, embedded symbols, both
  embedded resources, two packages). Mirror `PackagingSmokeTests.cs`.
  Spec: §J5.
- [ ] J6 — Create `tests/DealDesk.Tests/DeployUnitTests.cs`: six facts about
  `deploy/dealdesk.service` (section order, `Type=exec`, unprivileged user,
  one writable path, restart on failure, every path absolute). Mirror
  `PackagingSmokeTests.cs`. Spec: §J6.
- [ ] J7 — Create `tests/DealDesk.Tests/DeployEnvTests.cs`: six facts about
  `deploy/dealdesk.env.example` (one assignment per line, the two live keys,
  the token left commented, loopback only, paths under the unit's writable
  directory, the bundle extract dir). Mirror `PackagingSmokeTests.cs`.
  Spec: §J7.
- [ ] J8 — Create `tests/DealDesk.Tests/CiWorkflowTests.cs`: six facts about
  `.github/workflows/ci.yml` (both triggers, read-only permissions, two jobs,
  nothing installed but the SDK, the gates in order, the publish checked).
  Mirror `PackagingSmokeTests.cs`. Spec: §J8.
- [ ] J9 — Create `docs/PROCESS.md`: the loop story in one page — the two
  lanes, the shape of a phase, the ledger counted with the command in the
  spec, what went wrong, what was deferred. Name no vendor and no model.
  Gate: `bash verify.sh`. Spec: §J9.
- [ ] J10 — Edit `README.md`: add a `## Deploy` section after `## Layout`, two
  `## Layout` bullets, one PROCESS.md sentence in the intro. No `./` paths in
  fenced blocks — `verify.sh` lints those. No CI badge. Gate: `bash verify.sh`.
  Spec: §J10.
- [ ] J11 — Run `bash verify.sh`, append a `## Phase J` section to STATUS.md,
  and flip ROADMAP row 9 and the `docs/PROCESS.md` row to SHIPPED. Closes
  Phase J and the project. Gate: `bash verify.sh`. Spec: §J11.
