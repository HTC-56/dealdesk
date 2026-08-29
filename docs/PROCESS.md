# Process — how dealdesk was built

## What this is

dealdesk was built by two lanes running on the same machine.

A planning lane wrote each phase spec from a single product document and
implemented the parts a spec cannot carry — the initial scaffold, the
migration and data layer, the property tests that prove arithmetic without
a browser.

A local execution lane picked the first unchecked task in `TODO.md`, built
it, ran four gates (`dotnet build -warnaserror`, `dotnet test`, `dotnet
format --verify-no-changes`, `bash scripts/scrub-check.sh`), and committed.
One task per session, always.

## The shape of a phase

`TASK_PHASE_<letter>.md` holds one greppable section per task; `TODO.md`
holds one short pointer line each; the last task of every phase runs
`verify.sh`, appends to `STATUS.md` and flips the `ROADMAP.md` row.
Phases ran A through J.

Each phase had a gate: build warnings-clean, all tests passing, format
clean, scrub clean. If a gate stayed red after two fix attempts the task
was marked blocked and the session ended.

## What the ledger says

The loop ran **59 sessions** across both lanes:

- **Loop lane:** 49 sessions — 45 ended in a commit, 1 ran out of turns,
  3 ended no-op.
- **Plan lane:** 10 sessions — 10 ended in a commit.

A session ending without a commit is a recorded outcome, not a hidden one.
The ledger records every session, including the ones that did not produce
a commit.

The repo contains **152 commits** and **264 tests**.

## What the loop got wrong

The ledger records one session that ran out of turns and three that ended
no-op — the executor produced something that passed every gate but did not
advance the task. The phase specs got shorter and more concrete over time
as a result, favouring greppable sections and explicit gate commands over
narrative.

## What was deferred, and why

The README hero screenshot (the loop has no browser) and the CI badge
(its URL needs a repo name only a human can confirm). See
`ROADMAP.md`'s reservations ledger for the rest.
