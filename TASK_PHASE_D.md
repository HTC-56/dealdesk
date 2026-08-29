# Phase D — the lifecycle and the append-only audit trail

**ROADMAP row: "3 — Lifecycle + append-only audit trail"** (NOT BUILT → SHIPPED
at §D10). SPEC.md feature 3: draft → appraised → presented → won | lost, and
every value change recorded with who, when, old, new and a reason note in a
table nothing can rewrite.

## Already shipped by the planning lane

Commits `feat(D1)`…`feat(D3)` landed the hard code. §D4 starts green at **76
tests**. What exists now, on top of Phase C:

- `sql/003_audit.sql` — the `audit_entry` table, its four CHECK constraints,
  and two triggers that abort UPDATE and DELETE against it.
- `src/DealDesk/Domain/Lifecycle.cs` — `All`, `IsStatus`, `Canonical`,
  `NextFrom`, `IsTerminal`, `CanMove`, `Refuse`.
- `src/DealDesk/Api/AuditDtos.cs` — `AuditEntryView`, `ChangeStatus`,
  `ReviseAppraisal`, each Create/Save body with its own `Validate()`.
  **You add no DTOs in this phase — they are written.**
- `src/DealDesk/Api/LifecycleEndpoints.cs` — `POST .../status` and
  `PATCH /api/appraisals/{id}`, plus the `AuditColumns` const §D4 uses.

**The lifecycle, in one sentence.** From `draft` the worksheet goes to
`appraised` or `lost`; from `appraised` to `presented` or `lost`; from
`presented` to `won` or `lost`. `won` and `lost` are closed and move nowhere.
Moving to the state it is already in is refused.

**The two refusal codes.** A status word that is not one of the five is a
**400**. A real status the lifecycle will not move to from here — including
"already there" and "already closed" — is a **409**. Both bodies carry `error`.

**Every write names who and why.** `changedBy` and `reason` are required on
`POST .../status` and on `PATCH`; either one missing or blank is a 400.

**Served JSON is camelCase**: `appraisalId`, `oldValue`, `changedBy`,
`changedAt`.

**Gate for every task below** (all four green, or the task is not done):

```
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
bash scripts/scrub-check.sh
```

---

## §D4 — Serve the audit trail

Edit `src/DealDesk/Api/LifecycleEndpoints.cs`. Add ONE route inside
`MapLifecycleEndpoints`, directly below the existing `MapPatch` block.

**The template is the walk-item `MapGet` in
`src/DealDesk/Api/WorksheetEndpoints.cs`** (the first route in that file).
Copy its shape — open the connection, 404 when `AppraisalExists` says no,
otherwise `Query<T>` and `Results.Json` — and change the table and the type.

`GET /api/appraisals/{id:long}/audit`

- 404 when `WorksheetEndpoints.AppraisalExists(connection, id)` is false.
- Otherwise `SELECT` the columns already listed in the `AuditColumns` const
  near the bottom of this file `FROM audit_entry WHERE appraisal_id = $id`,
  into `AuditEntryView`, and hand the rows to `Results.Json`.
- **Order `BY id DESC`** — newest change first. This is the one collection in
  the repo that reads newest-first, because a trail is read from the top.

Do not add a const; `AuditColumns` is already there. Do not touch
`Program.cs`; the file is already wired. Write no UPDATE and no DELETE against
`audit_entry` — the triggers would abort it anyway.

Gate: the four commands above. Commit `LifecycleEndpoints.cs` only.

---

## §D5 — Lifecycle rule tests

Create `tests/DealDesk.Tests/LifecycleTests.cs`. Mirror
`tests/DealDesk.Tests/VinTests.cs`: one `[Fact]` per law, plain `Assert` calls,
no database and no HTTP — `Lifecycle` is pure rules.

`using DealDesk.Domain;` and `using Xunit;`.

Assert these six:

1. `Lifecycle.CanMove("draft", "appraised")` is true, and
   `CanMove("appraised", "presented")` and `CanMove("presented", "won")` are
   true — the forward chain.
2. `CanMove("draft", "won")` and `CanMove("draft", "presented")` are false —
   the chain does not skip steps.
3. `CanMove("draft", "lost")`, `CanMove("appraised", "lost")` and
   `CanMove("presented", "lost")` are all true — a deal can die at any point.
4. `IsTerminal("won")` and `IsTerminal("lost")` are true; `IsTerminal("draft")`
   is false. `NextFrom("won")` has a `Count` of 0.
5. `CanMove("DRAFT", " Appraised ")` is true — status matching is trimmed and
   case-insensitive. `Canonical(" Appraised ")` is `"appraised"`.
6. `Refuse("draft", "appraised")` is null; `Refuse("draft", "draft")`,
   `Refuse("won", "lost")` and `Refuse("draft", "sold")` are each non-null.
   (Assert non-null, not the exact wording.)

Gate: the four commands above. Commit the one new test file.

---

## §D6 — Audit schema tests

Create `tests/DealDesk.Tests/AuditSchemaTests.cs`. Mirror
`tests/DealDesk.Tests/WorksheetSchemaTests.cs` exactly: `using var temp =
TempDb.CreateMigrated();`, `using var connection = temp.Db.Open();`, its
private `CreateAppraisal(connection)` helper copied in, and
`Assert.Throws<SqliteException>` around each rejected statement.

An audit row inserts with:
`(appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)`.
A good row is `field` `'miles'`, old `'74311'`, new `'74800'`, changed_by
`'D. Okonjo'`, reason `'odometer reread'`, changed_at an ISO timestamp.

Assert these six:

1. A well-formed row inserts with no exception.
2. A row whose `reason` is `''` throws, and the message contains
   `"CHECK constraint failed"`.
3. A row whose `changed_by` is `''` throws the same way.
4. A row whose `old_value` and `new_value` are equal throws the same way —
   the trail records movement only.
5. After inserting one good row, `UPDATE audit_entry SET reason = 'x'` throws,
   and `DELETE FROM audit_entry` throws. Assert both messages contain
   `"append-only"`.
6. After inserting one good row, `DELETE FROM appraisal WHERE id = $id` throws
   — the trail is not cascaded away with its parent.

Use invented demo names only; no real make, model, person or dealership.

Gate: the four commands above. Commit the one new test file.

---

## §D7 — Status and trail over HTTP

Create `tests/DealDesk.Tests/LifecycleApiTests.cs`. Mirror
`tests/DealDesk.Tests/WalkItemApiTests.cs`: one `[Fact]` per assertion, `using
var factory = new DeskAppFactory();`, `using var client =
factory.CreateClient();`, then `var id = await
WalkItemApiTests.CreateAppraisalAsync(client);`.

Post with `PostAsJsonAsync` and an anonymous camelCase object; read with
`JsonDocument.Parse(await response.Content.ReadAsStringAsync())`.

A status body looks like
`new { status = "appraised", changedBy = "D. Okonjo", reason = "walk complete" }`.

Assert these six:

1. `POST /api/appraisals/{id}/status` to `appraised` returns 200 and the body's
   `status` is `"appraised"`.
2. After that move, `GET /api/appraisals/{id}/audit` returns 200, an array of
   length 1, whose `field` is `"status"`, `oldValue` `"draft"` and `newValue`
   `"appraised"`.
3. Moving `draft` straight to `won` returns 409 with an `error` field.
4. A body whose `status` is `"sold"` returns 400; a body with a valid status but
   no `reason` also returns 400.
5. Walking `draft` → `appraised` → `presented` → `won` all succeed, and the
   trail then has 3 entries with the newest (`newValue` `"won"`) at index 0.
6. `POST /api/appraisals/9999/status` returns 404, and
   `GET /api/appraisals/9999/audit` returns 404.

Each test gets its own factory, so no test sees another's rows.

Gate: the four commands above. Commit the one new test file.

---

## §D8 — Field revisions over HTTP

Create `tests/DealDesk.Tests/RevisionApiTests.cs`. Mirror
`tests/DealDesk.Tests/LifecycleApiTests.cs` from §D7 for structure — same
factory, same `CreateAppraisalAsync`, same `JsonDocument` reads.

Send the PATCH with `await client.PatchAsJsonAsync(url, body)` — the same
`using System.Net.Http.Json;` that gives `PostAsJsonAsync` gives this one too.

The appraisal `CreateAppraisalAsync` makes has miles `74311` and appraiser
`"A. Whitfield"`. Assert these six:

1. `PATCH /api/appraisals/{id}` with `{ miles = 74800, changedBy = "D. Okonjo",
   reason = "odometer reread" }` returns 200 and the body's `miles` is `74800`.
2. After it, the trail has one entry: `field` `"miles"`, `oldValue` `"74311"`,
   `newValue` `"74800"`, `changedBy` `"D. Okonjo"`.
3. Revising `miles` and `appraiser` in ONE call writes TWO trail entries.
4. Revising `miles` to the value it already holds returns 200 and writes NO
   trail entry (the trail stays empty).
5. A body carrying only `changedBy` and `reason` returns 400; a body with
   `miles` but no `reason` returns 400.
6. `PATCH /api/appraisals/9999` returns 404.

Gate: the four commands above. Commit the one new test file.

---

## §D9 — Document the lifecycle in the README

Edit `README.md`. Two small changes, nothing else touched.

**1.** In the existing `## The API` bullet list, append three lines after the
`GET /api/appraisals/{id}/offer` line:

- `POST /api/appraisals/{id}/status` — move the worksheet through its lifecycle
- `PATCH /api/appraisals/{id}` — revise worksheet fields, audited
- `GET /api/appraisals/{id}/audit` — the change trail, newest first

Then, after the existing "**The offer carries its derivation.**" paragraph, add
ONE new paragraph, no more:

- **Every change is audited.** `POST .../status` and `PATCH` require
  `changedBy` and `reason`, and append one `audit_entry` row per changed field
  recording who, when, old and new. The table is append-only — UPDATE and
  DELETE against it are refused by the database. The lifecycle is
  draft → appraised → presented → won | lost; `lost` is reachable from any open
  state, and a move the lifecycle refuses is a 409.

**2.** The quickstart's healthz sample output still shows
`"schema":"002_worksheet"`. Change that one line to `003_audit` — a third
migration has shipped.

Promise nothing unbuilt: no desk page, no reports, no recon actuals, no
authentication.

`verify.sh` lints README code blocks: any `./x`, `bash x` or `--project x` path
inside a fence must exist in the repo. Add no such new reference.

Gate: `bash verify.sh`. Commit `README.md` only.

---

## §D10 — Close Phase D

Three small things, one commit. Mirror what §C9 did.

**1. Run `bash verify.sh`.** It must exit 0. Do not edit it.

**2. Append a `## Phase D` section to STATUS.md** — append only, never rewrite
what is above it. A few lines: the lifecycle moves through the five states with
illegal moves refused as 409, every value change appends to an append-only
`audit_entry` inside the same transaction, and the trail is served newest-first.
Give the test count from your own `dotnet test` run. Then say what is still NOT
built: recon actuals and variance, the three reports, the desk page, the ops
surface beyond `/healthz`, seeded demo data, and deploy packaging.

**3. Edit the ROADMAP.md table rows** (row edits are allowed there):

- Row 3 → `SHIPPED`, phase `D`, note: lifecycle rules, audited status moves and
  field revisions, append-only trail served newest-first.
- Leave every other row exactly as it is.

Gate: `bash verify.sh` green. Commit `STATUS.md` and `ROADMAP.md`.
