# PROJECT SPEC COMPLETE

The loop has finished all authorized work. Every SPEC.md v1 feature is built and
gated; DECISIONS.md carries no open questions. This is the terminal state until a
human locks new scope in DECISIONS.md.

Shipped phases: **A** (toolchain) · **B–C** (worksheet + offer math) · **D**
(lifecycle + audit) · **E** (recon actuals + variance) · **F** (the three reports)
· **G** (ops surface) · **H** (seeded demo month) · **I** (desk page) · **J**
(deploy packaging + PROCESS.md).

Coverage is in ROADMAP.md — all nine rows read SHIPPED. Verified this session:
`bash verify.sh` PASS (build, test, format, scrub), 264/264 tests, and a live run
serving `/healthz` at schema `005_reports`, the desk page, an offer derivation that
sums exactly, all three reports, and `/metrics`.

## Decisions needed

**1. Publishing** — flip the repo public, confirm the name, choose the license
(DECISIONS.md records default intent: MIT).
*Unlocks:* the CI badge in the README. `.github/workflows/ci.yml` ships and runs on
first push; the badge URL names the published repo, so the loop left the line out.
Recorded as a null result in ROADMAP.md.

**2. The README hero screenshot** — needs a human with a browser.
*Unlocks:* the last piece of SPEC.md feature 6. The loop cannot open `/` and would
not be able to verify a committed image was of this page. Recorded as a null result
in ROADMAP.md; §I8 points readers at the live page instead.

**3. A stray gitlink is tracked at `.claude/worktrees/e10-readme-recon`** — needs a
human because removing it is a deletion, which the loop is forbidden from doing.
*Blocks:* clean publication (item 1). Fix before pushing public.

- A `git worktree` created during Phase E was picked up by the sweep-commit in
  `8718f17` and committed as a **submodule reference** (mode `160000`), with no
  `.gitmodules` to go with it.
- Its target commit `eac4595` is **not reachable from `main`**, so a push sends a
  gitlink the remote cannot resolve. A stranger cloning gets a phantom empty
  directory — visible cruft in a repo whose commit history is part of the
  deliverable.
- It also degrades a SPEC.md-named gate: `scripts/scrub-check.sh` tries to read the
  directory as a file, prints `read error: 0: Is a directory`, and silently skips
  that path. The script still exits 0, so `verify.sh` reports PASS — the gate is
  degraded, not failing. Nothing else in the tree is unscanned.
- `dotnet test` on a clean clone is unaffected, so the SPEC.md "done means" bar
  still holds.
- Suggested fix, for a human to run: `git rm --cached .claude/worktrees/e10-readme-recon`,
  `git worktree remove .claude/worktrees/e10-readme-recon`, add `.claude/` to
  `.gitignore`, then re-run `bash verify.sh` to confirm gate 4 is quiet. Whether to
  also rewrite `8718f17` out of history is the human's call — it is cosmetic, and
  rewriting trades a clean history for changed hashes.

Items 1 and 2 are the two null results the loop recorded honestly rather than faking.
Item 3 is a defect the loop introduced and cannot clear itself.
