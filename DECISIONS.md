# Decisions

## Locked (2026-08-27, at scaffold)

- **SPEC.md is the whole product.** v1 is the nine features there, fenced by
  its non-goals. The planning lane derives phases from SPEC.md only; it never
  invents features. When every SPEC.md feature is built and gated,
  "PROJECT SPEC COMPLETE" is the desired terminal state — declare it, do not
  find more work. This project is meant to FINISH.
- **Stack**: .NET 8 minimal API, SQLite via Dapper (no EF — the SQL is the
  work sample), xUnit (+ WebApplicationFactory for in-process integration
  tests). Dependencies exactly as SPEC.md names them. The desk page is one
  hand-written self-contained HTML file — no framework, no build step, no
  external requests.
- **Domain fence is pre-registered**: generic, public-domain
  retail-automotive arithmetic only; invented demo names; zero outbound
  requests anywhere in src/; no real vendor, store, or business named
  anywhere in the repo.
- **Offer math is property-tested**: invariants (recommended value never
  exceeds anchor − recon; derivations sum exactly) live in xUnit property
  tests from the phase that builds the math.
- **First C# repo for the executor**: Phase A proves the toolchain (build,
  test, format gates, one migration + Dapper round-trip + one property
  test) before any domain feature. Structural failure at the stack is
  recorded here and the lane stands down; PROCESS.md reports it honestly.
- **Gates**: dotnet build -warnaserror, dotnet test,
  dotnet format --verify-no-changes, scrub-check — all green at every phase
  end. `verify.sh` composes them plus the README-quickstart lint.
- **Public-repo discipline from commit 1**: this repo will be published. No
  private hostnames, no real LAN IPs (docs use `localhost` / `192.0.2.x`),
  no absolute home paths, no key material, no references to other private
  projects — in files AND commit messages. `scrub-check.sh` enforces the
  file half; sessions carry the commit-message half.
- **Neutral git identity** until the publish decision (human-gated).

## Human-gated (never resolved by the loop)

- Publishing: flipping the repo public, name confirmation, license choice
  (default intent: MIT).
- Any scope beyond SPEC.md v1.

## Open Questions

*(none — SPEC.md answers v1 in full)*
