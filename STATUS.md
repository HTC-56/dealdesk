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

## Phase B

Phase B is closed. The worksheet schema (002_worksheet.sql) adds recon_line,
comparable_sale, walk_item, and offer_input tables with CHECK constraints on
status, price parity, and category enums. VIN validation checks format,
checksum digit, and throws on I/O/Q characters. OfferMath computes trade-in
allowance, recon difference, target gross, and the full price derivation
(anchor → target gross → price → pack → net). The appraisal endpoints accept
POST (create draft, 201 + Location) and GET (list newest-first, optional
status filter). Test count is now 56.

What is still NOT built: the walk / recon / comp endpoints, the offer endpoint
that serves the derivation, the desk page. Lifecycle, audit, recon actuals and
the reports remain untouched.

## Phase C

Phase C is closed. The worksheet's child collections — walk, recon-line, and
comparable-sale — are all on the wire (GET + POST each). The offer endpoint
(`GET .../offer`) serves the recommended trade value together with its full
derivation array, and the store numbers upsert cleanly. Test count is now 76.

What is still NOT built: lifecycle and the audit trail, recon actuals and
variance, the three reports (look-to-book, recon variance, gross by appraiser),
the desk page, the ops surface beyond `/healthz`, seeded demo data, and deploy
packaging.
