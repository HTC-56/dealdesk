-- 004_recon_actual.sql — recon actuals, SPEC.md feature 4. After the store
-- acquires the vehicle, real invoices post against the estimate line by line;
-- the gap between the two is variance, and variance is data here rather than
-- something a report recomputes.
-- Migrations are APPEND-ONLY: never edit this file, add 005_*.sql instead.
--
-- One posting hangs off ONE recon_line, not off the appraisal: SPEC.md says
-- actuals post "line-by-line", and a cost with no estimate line to answer to
-- could not be varianced against anything. Work nobody estimated is entered as
-- a recon_line with an estimate of 0 (002_worksheet.sql allows that) and then
-- posted against normally, so the trail still shows what was expected and what
-- was spent.
--
-- Many postings per line, on purpose: one repair order is rarely one invoice,
-- and summing them is the variance math's job, not the schema's.
--
-- `amount` is INTEGER whole cents with no `_cents` suffix, like every other
-- money column, so Dapper's underscore matching lands it on `Money Amount`
-- with no AS alias in any query.
--
-- Unlike recon_line.estimate, amount is NOT constrained to be positive. A
-- returned part or a supplier credit is a negative posting — OfferMath.cs
-- already says a recon credit belongs in the actuals rather than in an
-- estimate line, and this is the column that keeps that promise. What IS
-- refused is a zero: a posting that moves no money is noise, the same reason
-- audit_entry refuses a row whose value did not change.
--
-- The line cascades from its appraisal, and postings cascade from the line, so
-- deleting a worksheet still takes its whole recon history with it.

CREATE TABLE recon_actual (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    recon_line_id  INTEGER NOT NULL REFERENCES recon_line (id) ON DELETE CASCADE,
    amount         INTEGER NOT NULL,
    description    TEXT    NOT NULL,
    posted_by      TEXT    NOT NULL,
    posted_at      TEXT    NOT NULL,

    CONSTRAINT recon_actual_amount_moved CHECK (amount <> 0),
    CONSTRAINT recon_actual_described    CHECK (length(description) > 0),
    CONSTRAINT recon_actual_who_named    CHECK (length(posted_by) > 0)
);

-- Every read is "the postings for this line", oldest first; the id in the
-- index keeps that ordering off the table itself.
CREATE INDEX recon_actual_line_idx ON recon_actual (recon_line_id, id);
