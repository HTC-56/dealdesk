-- 002_worksheet.sql — the worksheet itself: the condition walk, the itemized
-- recon estimate, the hand-entered comps, and the two store numbers the offer
-- math subtracts.
-- Migrations are APPEND-ONLY: never edit this file, add 003_*.sql instead.
--
-- Every money column here is INTEGER whole cents, mapped onto Domain/Money.cs
-- by MoneyTypeHandler. The columns carry no `_cents` suffix on purpose: Dapper
-- matches snake_case to PascalCase, so `estimate` lands straight on a
-- `Money Estimate` property and no query needs an AS alias.
--
-- Comps are hand-entered by design — SPEC.md's non-goals forbid market feeds,
-- book values and scraping — so no column here records a source system.
--
-- Every child row hangs off one appraisal and dies with it. Db.Open turns
-- PRAGMA foreign_keys ON, so ON DELETE CASCADE is actually enforced.

CREATE TABLE walk_item (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    appraisal_id  INTEGER NOT NULL REFERENCES appraisal (id) ON DELETE CASCADE,
    area          TEXT    NOT NULL,
    severity      TEXT    NOT NULL DEFAULT 'minor',
    note          TEXT    NOT NULL DEFAULT '',
    created_at    TEXT    NOT NULL,

    CONSTRAINT walk_item_area_valid CHECK (
        area IN ('exterior', 'interior', 'mechanical', 'tires', 'glass',
                 'electronics', 'other')
    ),
    CONSTRAINT walk_item_severity_valid CHECK (
        severity IN ('minor', 'moderate', 'severe')
    )
);

CREATE TABLE recon_line (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    appraisal_id  INTEGER NOT NULL REFERENCES appraisal (id) ON DELETE CASCADE,
    category      TEXT    NOT NULL,
    description   TEXT    NOT NULL,
    estimate      INTEGER NOT NULL,
    created_at    TEXT    NOT NULL,

    -- The category vocabulary is fixed here because the recon-variance report
    -- groups by it; a free-text category would make that report meaningless.
    CONSTRAINT recon_line_category_valid CHECK (
        category IN ('mechanical', 'body', 'paint', 'tires', 'glass',
                     'interior', 'detail', 'other')
    ),
    CONSTRAINT recon_line_estimate_sane CHECK (estimate >= 0),
    CONSTRAINT recon_line_described     CHECK (length(description) > 0)
);

CREATE TABLE comp (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    appraisal_id  INTEGER NOT NULL REFERENCES appraisal (id) ON DELETE CASCADE,
    label         TEXT    NOT NULL,
    model_year    INTEGER NOT NULL,
    miles         INTEGER NOT NULL,
    price         INTEGER NOT NULL,
    note          TEXT    NOT NULL DEFAULT '',
    created_at    TEXT    NOT NULL,

    CONSTRAINT comp_price_positive CHECK (price > 0),
    CONSTRAINT comp_miles_sane     CHECK (miles >= 0),
    CONSTRAINT comp_year_sane      CHECK (model_year BETWEEN 1900 AND 2200)
);

-- At most one row per appraisal: the two store numbers, plus an optional
-- anchor the desk typed by hand instead of taking the comp average.
CREATE TABLE offer_input (
    appraisal_id     INTEGER PRIMARY KEY REFERENCES appraisal (id) ON DELETE CASCADE,
    pack             INTEGER NOT NULL DEFAULT 0,
    target_gross     INTEGER NOT NULL DEFAULT 0,
    anchor_override  INTEGER NULL,
    updated_at       TEXT    NOT NULL,

    CONSTRAINT offer_input_pack_sane   CHECK (pack >= 0),
    CONSTRAINT offer_input_target_sane CHECK (target_gross >= 0),
    CONSTRAINT offer_input_anchor_sane CHECK (
        anchor_override IS NULL OR anchor_override > 0
    )
);

CREATE INDEX walk_item_appraisal_idx  ON walk_item  (appraisal_id);
CREATE INDEX recon_line_appraisal_idx ON recon_line (appraisal_id);
CREATE INDEX comp_appraisal_idx       ON comp       (appraisal_id);
CREATE INDEX recon_line_category_idx  ON recon_line (category);
