# Status

Repo scaffolded 2026-08-27. Nothing built yet. SPEC.md is the product;
DECISIONS.md locks the fence; ROADMAP.md is the scoreboard. The planning lane
authors Phase A from SPEC.md (Phase A must prove the toolchain — build,
test, format gates, one migration + Dapper round-trip + one property test —
before any domain feature is built; this is the executor's first C# repo).

Per-phase sections append below as phases ship.

## Phase A

The toolchain is proven. The .NET 8 SDK builds the solution warnings-clean. xUnit
runs 28 tests including one in-process WebApplicationFactory test. The migrator
applies `001_init.sql` and is idempotent. `Money` is property-tested across three
tests. `verify.sh` composes every gate (build, test, format, scrub) plus
README-quickstart path lint.

What is NOT built yet: every domain feature — worksheet, offer math, audit, recon,
reports, desk page.
