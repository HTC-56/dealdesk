-- 003_audit.sql — the append-only audit trail behind SPEC.md feature 3.
-- Migrations are APPEND-ONLY: never edit this file, add 004_*.sql instead.
--
-- One row per changed FIELD, not per request: revising miles and appraiser in
-- the same call writes two rows, so the recon-variance and look-to-book work
-- later can ask "when did this number move" of one column.
--
-- Values are stored as TEXT whatever the column's own type is. The trail spans
-- a status string, an odometer integer and an appraiser name; one text column
-- keeps it a single readable table instead of one typed column per field, and
-- nothing downstream does arithmetic on an audit value.
--
-- APPEND-ONLY IS ENFORCED HERE, not just in the handlers: the two triggers
-- below abort any UPDATE or DELETE against this table. That also means the FK
-- carries NO cascade — deleting an appraisal that has a trail fails rather
-- than silently taking the evidence with it. dealdesk exposes no delete route,
-- so no endpoint is affected.

CREATE TABLE audit_entry (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    appraisal_id  INTEGER NOT NULL REFERENCES appraisal (id),
    field         TEXT    NOT NULL,
    old_value     TEXT    NOT NULL,
    new_value     TEXT    NOT NULL,
    changed_by    TEXT    NOT NULL,
    reason        TEXT    NOT NULL,
    changed_at    TEXT    NOT NULL,

    CONSTRAINT audit_entry_field_named   CHECK (length(field) > 0),
    CONSTRAINT audit_entry_who_named     CHECK (length(changed_by) > 0),
    CONSTRAINT audit_entry_reason_given  CHECK (length(reason) > 0),

    -- An entry that records no movement is noise in the trail.
    CONSTRAINT audit_entry_value_moved   CHECK (old_value <> new_value)
);

CREATE TRIGGER audit_entry_no_update
BEFORE UPDATE ON audit_entry
BEGIN
    SELECT RAISE(ABORT, 'audit_entry is append-only: UPDATE is refused');
END;

CREATE TRIGGER audit_entry_no_delete
BEFORE DELETE ON audit_entry
BEGIN
    SELECT RAISE(ABORT, 'audit_entry is append-only: DELETE is refused');
END;

-- The trail is always read for one appraisal, newest first; the id tiebreak
-- keeps two changes inside the same timestamp in insertion order.
CREATE INDEX audit_entry_appraisal_idx ON audit_entry (appraisal_id, id);
