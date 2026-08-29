-- 001_init.sql — the appraisal identity + lifecycle row.
-- Migrations are APPEND-ONLY: never edit this file, add 002_*.sql instead.
--
-- Columns here are exactly the vehicle basics and lifecycle states SPEC.md
-- names. Money-bearing tables (walk items, recon, comps, offer) arrive in
-- later migrations; nothing in this file carries currency.

CREATE TABLE appraisal (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    vin           TEXT    NOT NULL,
    model_year    INTEGER NOT NULL,
    make          TEXT    NOT NULL,
    model         TEXT    NOT NULL,
    trim_level    TEXT    NOT NULL DEFAULT '',
    miles         INTEGER NOT NULL,
    appraiser     TEXT    NOT NULL,
    status        TEXT    NOT NULL DEFAULT 'draft',
    created_at    TEXT    NOT NULL,
    updated_at    TEXT    NOT NULL,

    CONSTRAINT appraisal_status_valid CHECK (
        status IN ('draft', 'appraised', 'presented', 'won', 'lost')
    ),
    CONSTRAINT appraisal_miles_sane     CHECK (miles >= 0),
    CONSTRAINT appraisal_year_sane      CHECK (model_year BETWEEN 1900 AND 2200),
    CONSTRAINT appraisal_vin_len        CHECK (length(vin) = 17)
);

CREATE INDEX appraisal_status_idx    ON appraisal (status);
CREATE INDEX appraisal_appraiser_idx ON appraisal (appraiser);
