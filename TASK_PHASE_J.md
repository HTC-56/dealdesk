# Phase J — deploy-grade packaging

**ROADMAP row: "9 — Deploy-grade packaging"** (PARTIAL → SHIPPED at §J11), and
the `docs/PROCESS.md` row with it. SPEC.md feature 9: "`dotnet publish`
single-file self-contained binary; example systemd unit; GitHub Actions CI
(`dotnet build` + `dotnet test`); README quickstart …; `docs/PROCESS.md`."
The quickstart shipped in Phase A. This phase closes the rest — and the
project.

## Already shipped by the planning lane

Commits `feat(J1)`…`feat(J4)` landed the artifacts. §J5 starts green at **240
tests**. What exists now, on top of Phase I:

- `src/DealDesk/DealDesk.csproj` — a PropertyGroup conditioned on
  `'$(RuntimeIdentifier)' != ''` holding the publish shape.
- `deploy/dealdesk.service` — the example systemd unit.
- `deploy/dealdesk.env.example` — the environment file it reads.
- `.github/workflows/ci.yml` — two jobs: `gates` and `publish`.
- `tests/DealDesk.Tests/PackagingSmokeTests.cs` — four facts plus the helpers
  below.

**You add no `src/` code in this phase, and you never edit those four
artifacts.** §J5–§J8 are test files, §J9–§J10 are docs, §J11 closes.

## The helpers — every test task below uses these

`internal static` on `PackagingSmokeTests`, so call them as
`PackagingSmokeTests.X`:

- `ReadRepoFile(path)` — one repo file as text, LF endings. Paths are
  repo-relative.
- `SettingLines(path)` — that file's non-blank lines, trimmed, with `#`
  comment lines dropped. Right for the unit and the environment file.
- `ConfigurationKeys(path)` — the config keys an environment file sets, in the
  spelling Program.cs uses: `DEALDESK_Ops__LedgerPath` reads back as
  `Ops:LedgerPath`.
- Path constants: `ProjectFile`, `UnitFile`, `EnvExample`, `WorkflowFile`,
  `ProgramFile`. Also `PublishedBinary` (`"DealDesk"`) and `EnvPrefix`.

These read files off disk rather than over HTTP — feature 9's artifacts ship no
code, so there is no route to ask. No `DeskAppFactory`, no client, no `async`
in these four files.

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §J5 — What the publish actually produces

Create `tests/DealDesk.Tests/PublishProfileTests.cs`. Mirror
`tests/DealDesk.Tests/PackagingSmokeTests.cs` for structure: one `[Fact]` per
behaviour, no HTTP, read the file with
`PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.ProjectFile)`.

The smoke test asserts the five publish properties exist. This file asserts
they are in the right PLACE and that nothing else drifted — a publish property
in the wrong PropertyGroup would make every local gate build RID-specific.

To talk about "inside the conditioned group": find the index of
`Condition="'$(RuntimeIdentifier)' != ''"`, take the substring from there to
the first `</PropertyGroup>` after it, and assert against that slice.

Assert these six:

1. All five publish properties — `SelfContained`, `PublishSingleFile`,
   `IncludeNativeLibrariesForSelfExtract`, `EnableCompressionInSingleFile`,
   `PublishTrimmed` — appear inside that slice, and none of them appears
   anywhere before the condition.
2. The project pins no runtime identifier of its own: the file contains no
   `<RuntimeIdentifier>` element. The operator chooses the platform on the
   command line, which is why the properties are conditioned at all.
3. Nothing is trimmed and nothing is AOT: the slice sets `PublishTrimmed` to
   `false` and the file contains no `PublishAot`. Dapper materialises rows by
   reflection.
4. Symbols travel inside the file: the slice sets `DebugType` to `embedded`,
   so the publish emits no sidecar `.pdb`.
5. The single file still carries its own content: the file contains both
   `EmbeddedResource` includes, with logical-name prefixes `sql.` and `page.`.
   Schema, seed and desk page ride inside the binary.
6. The dependency surface did not grow: exactly two `PackageReference` lines,
   `Dapper` and `Microsoft.Data.Sqlite`. Count occurrences of
   `<PackageReference`.

Gate: the four commands above. Commit the one new test file.

---

## §J6 — The example unit would actually start

Create `tests/DealDesk.Tests/DeployUnitTests.cs`. Mirror
`PackagingSmokeTests.cs`. Read the unit with
`PackagingSmokeTests.SettingLines(PackagingSmokeTests.UnitFile)`, which has
already dropped the comment lines.

A unit file nobody in this lane can run is worth exactly as much as the care
taken over it. These six are the ones a wrong answer would break on a real
host.

Assert these six:

1. The three sections appear in order: the index of `[Unit]` in the line array
   is less than `[Service]`, which is less than `[Install]`.
2. The type is `Type=exec`, and the unit does NOT contain `Type=notify`.
   dealdesk is a plain Kestrel host and never signals readiness to systemd; a
   `notify` unit would sit there until it timed out.
3. It runs unprivileged: the lines include `User=dealdesk`, `Group=dealdesk`
   and `NoNewPrivileges=true`, and no line is `User=root`.
4. The filesystem is closed except the one place it writes: the lines include
   `ProtectSystem=strict`, `ProtectHome=true` and `PrivateTmp=true`, and
   exactly one line starts with `ReadWritePaths=` and it names
   `/var/lib/dealdesk`.
5. A crash is recovered, not mourned: one line is `Restart=on-failure` and one
   line starts with `RestartSec=`.
6. Every path it names is absolute. For each line starting with `ExecStart=`,
   `WorkingDirectory=`, `EnvironmentFile=` or `ReadWritePaths=`, the value
   after the `=` starts with `/`. systemd refuses a relative path, so an
   example carrying one would fail on the host and pass every gate here.

Gate: the four commands above. Commit the one new test file.

---

## §J7 — The environment example configures the real app

Create `tests/DealDesk.Tests/DeployEnvTests.cs`. Mirror
`PackagingSmokeTests.cs`. Use `PackagingSmokeTests.SettingLines` and
`PackagingSmokeTests.ConfigurationKeys` on
`PackagingSmokeTests.EnvExample`, and read Program.cs with
`ReadRepoFile(PackagingSmokeTests.ProgramFile)`.

The smoke test asserts every key the example sets is a key Program.cs reads.
This file asserts the rest of the contract: the shape of the lines, what is
deliberately left unset, and where the paths point.

Assert these six:

1. Every setting line is one assignment: it contains `=`, and the name before
   the first `=` is non-empty and holds no space.
2. The keys are exactly the two the file sets uncommented — `Db:Path` and
   `Ops:LedgerPath` — in that order, and each of those two strings appears in
   Program.cs.
3. The bearer token is offered but not armed: the raw file text (use
   `ReadRepoFile`, not `SettingLines`) mentions `DEALDESK_Auth__Token`, and no
   line from `SettingLines` starts with it. Writes are open until an operator
   decides otherwise, which is what the README quickstart runs against.
4. The listen address is loopback: one setting line starts with
   `ASPNETCORE_URLS=`, its value contains `127.0.0.1`, and no line in the file
   contains `0.0.0.0`. dealdesk has no TLS; a reverse proxy belongs in front.
5. Every path it hands out is writable by the unit: for each setting line
   whose value starts with `/`, that value starts with `/var/lib/dealdesk` —
   the single path `deploy/dealdesk.service` lists in `ReadWritePaths`. Read
   the unit too and assert that string appears in both files.
6. The single file is told where to unpack: one setting line starts with
   `DOTNET_BUNDLE_EXTRACT_BASE_DIR=`. Without it the runtime unpacks the
   native SQLite library under a temporary directory that a reboot takes away.

Gate: the four commands above. Commit the one new test file.

---

## §J8 — CI runs what the repo says green means

Create `tests/DealDesk.Tests/CiWorkflowTests.cs`. Mirror
`PackagingSmokeTests.cs`. Read the workflow with
`PackagingSmokeTests.ReadRepoFile(PackagingSmokeTests.WorkflowFile)` — plain
`Contains` over the text is fine, there is no YAML parser in this repo and
this phase adds no package.

The smoke test asserts the four gate commands are present. This file asserts
CI is the same promise the README makes: nothing but the .NET 8 SDK, no write
scope, and a publish that is checked rather than assumed.

Assert these six:

1. It runs on both events: the text contains `push:` and `pull_request:`.
2. It asks for no write access: the text contains `permissions:` and
   `contents: read`, and does not contain `contents: write`.
3. Both jobs exist and name the same runner: the text contains `gates:`,
   `publish:` and two occurrences of `runs-on: ubuntu-latest`.
4. Nothing is installed but the SDK: the text contains `actions/checkout@v4`
   and `actions/setup-dotnet@v4` and `8.0.x`, and contains none of
   `apt-get`, `docker`, `npm`, `pip`.
5. The gates run in the repo's order: the index of `dotnet build -warnaserror`
   is less than `dotnet test`, which is less than
   `dotnet format --verify-no-changes`, which is less than
   `bash scripts/scrub-check.sh`.
6. The publish is checked, not assumed: the text contains `dotnet publish`,
   `-c Release`, `-r linux-x64`, and a step that counts the output files
   (`find out -type f | wc -l`).

Gate: the four commands above. Commit the one new test file.

---

## §J9 — docs/PROCESS.md, the loop story

Create `docs/PROCESS.md` — one page, no more than ~120 lines. SPEC.md's
opening promises it: "Built end-to-end by an autonomous local-model coding
loop; the commit history is part of the deliverable."

First run these three and write down what they print — the numbers must be
measured, never guessed:

```
git rev-list --count HEAD
dotnet test --no-build
awk -F'\t' 'NR>1{n[$2]++; r[$2"/"$4]++} END {for (k in n) print k, n[k]; for (k in r) print k, r[k]}' loop-ledger.tsv
```

Write these sections, in this order:

1. **What this is** — dealdesk was built by two lanes. A planning lane wrote
   each phase spec and implemented the parts a spec cannot carry; a local
   execution lane picked the first unchecked task in TODO.md, built it, ran
   four gates, and committed. One task per session, always.
2. **The shape of a phase** — `TASK_PHASE_<letter>.md` holds one greppable
   section per task; TODO.md holds one short pointer line each; the last task
   of every phase runs `verify.sh`, appends to STATUS.md and flips the
   ROADMAP.md row. Phases ran A through J.
3. **What the ledger says** — the numbers you measured above: sessions per
   lane, how many ended in a commit, and how many did not. Say plainly that a
   session ending without a commit is a recorded outcome, not a hidden one.
4. **What the loop got wrong** — be specific and short. Sessions that ran out
   of turns or ended no-op are in the ledger; the phase specs got shorter and
   more concrete over time because of it.
5. **What was deferred, and why** — the README hero screenshot (the loop has
   no browser) and the CI badge (its URL needs a repo name only a human can
   confirm). Point at ROADMAP.md's reservations ledger for the rest.

Rules for the prose: call the executor "a local model" or "the local
execution lane" — name no vendor, no product and no model. Say nothing about
what dealdesk cost. No absolute paths. Keep it to one page a reader finishes.

Gate: `bash verify.sh`. Commit `docs/PROCESS.md` only.

---

## §J10 — Document deployment in the README

Edit `README.md`. Three changes, nothing else touched.

**1.** Add a new `## Deploy` section, placed after `## Layout` and before
`## Gates`. It says: `dotnet publish -c Release -r linux-x64` produces one
self-contained executable that carries the .NET runtime, the migrations, the
seed script and the desk page, so there is nothing to copy beside it; another
runtime identifier gives another platform. Then: `deploy/dealdesk.service` is
an example systemd unit and `deploy/dealdesk.env.example` is the environment
file it reads, naming every configuration key the service understands. Say the
example binds to loopback and that a reverse proxy belongs in front of it.
Include the publish command in a fenced block.

**⚠ `verify.sh` lints fenced code blocks for repo paths.** Any word in a
fenced block starting `./` must be a path that exists in the repo. Write
`out/DealDesk`, never `./out/DealDesk`. Paths without the `./` prefix are not
linted.

**2.** In `## Layout`, add two bullets matching the four already there:
`deploy/` — example systemd unit and environment file; `docs/` —
`PROCESS.md`, how the loop built this.

**3.** Add one sentence at the end of the intro paragraph pointing at
`docs/PROCESS.md`, replacing nothing.

Do NOT add a CI badge — its URL needs the published repo name, which is a
human-gated decision. Do NOT change the healthz sample output: this phase adds
no migration, so `005_reports` is still current.

Gate: `bash verify.sh`. Commit `README.md` only.

---

## §J11 — Close Phase J, and the project

Three small things, one commit. Mirror what §I9 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it.

**2. Append a `## Phase J` section to STATUS.md** — append only, never rewrite
what is above it. A few lines: `dotnet publish -c Release -r linux-x64` yields
one self-contained executable carrying its own runtime, schema, seed and desk
page; `deploy/` holds an example systemd unit and the environment file it
reads; `.github/workflows/ci.yml` runs the four gates and the publish on every
push; `docs/PROCESS.md` tells the loop story. Give the test count from your own
`dotnet test` run. Then say what is still NOT built: nothing — every SPEC.md
feature is shipped, with the hero screenshot and the CI badge deferred to a
human, as ROADMAP.md's reservations record.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 9 → `SHIPPED`, phase `A, J`, note: `single-file publish, example unit
  and env file, CI on four gates; README quickstart from A4`.
- The `docs/PROCESS.md` row → `SHIPPED`, phase `J`, note: `the loop story,
  with the ledger counted rather than guessed`.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
