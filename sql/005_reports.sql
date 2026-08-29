-- 005_reports.sql — the three reports a used-car director actually watches,
-- as SQL views. SPEC.md feature 5.
-- Migrations are APPEND-ONLY: never edit this file, add 006_*.sql instead.
--
-- VIEWS, not tables. A report is a reading of rows that already exist, so
-- nothing here stores a number that could drift from the worksheet it came
-- from — the same reason GET .../offer and GET .../recon-variance compute on
-- read rather than caching a column. Api/ReportEndpoints.cs SELECTs from these
-- and adds only the store-wide totals.
--
-- Money columns stay INTEGER whole cents with no `_cents` suffix, so Dapper's
-- underscore matching lands `target_gross` straight on `TargetGross` and no
-- query in the repo carries an AS alias.
--
-- The variance sign is the one 004_recon_actual.sql and Domain/ReconVariance.cs
-- already fixed: variance = actual − estimate, POSITIVE means over estimate.
-- It is not redefined here. A report that disagreed with the worksheet it
-- summarises would be worse than no report.

-- An internal building block, not a report of its own: one row per appraisal
-- that has recon lines, carrying that worksheet's estimate, actual and how many
-- of its lines nobody has posted against yet. report_front_gross joins it so
-- the gross arithmetic stays readable instead of repeating a correlated
-- subquery three times.
--
-- The postings are summed in a correlated subquery rather than a JOIN on
-- purpose: joining recon_line to recon_actual repeats each estimate once per
-- posting, which would silently inflate the estimate column on any line with
-- more than one invoice.
CREATE VIEW appraisal_recon_rollup AS
SELECT
  l.appraisal_id AS appraisal_id,
  SUM(l.estimate) AS estimate,
  SUM(COALESCE((SELECT SUM(p.amount) FROM recon_actual p
                WHERE p.recon_line_id = l.id), 0)) AS actual,
  SUM(CASE WHEN EXISTS (SELECT 1 FROM recon_actual p
                        WHERE p.recon_line_id = l.id)
           THEN 0 ELSE 1 END) AS unposted_lines
FROM recon_line l
GROUP BY l.appraisal_id;

-- Report 1 — look-to-book, one row per appraiser.
--
-- "Looked" is every worksheet the appraiser opened; "booked" is the ones the
-- store actually bought. The ratio SPEC.md names is appraised vs won, so the
-- denominator is APPRAISED rather than looked: a worksheet abandoned in draft
-- was never a real look at the car.
--
-- Whether a worksheet ever reached `appraised` cannot always be read off its
-- current status. The chain is forward-only, so `presented` and `won` were
-- certainly appraised first — but Domain/Lifecycle.cs lets `lost` be reached
-- straight from `draft`, because a customer can walk before anyone prices the
-- car. For those, the audit trail is asked whether the status ever moved INTO
-- `appraised`. That is exactly what 003_audit.sql's one-row-per-field trail
-- was built to answer.
CREATE VIEW report_look_to_book AS
SELECT
  a.appraiser AS appraiser,
  COUNT(*) AS looked,
  SUM(CASE
        WHEN a.status IN ('appraised', 'presented', 'won') THEN 1
        WHEN EXISTS (SELECT 1 FROM audit_entry e
                     WHERE e.appraisal_id = a.id
                       AND e.field = 'status'
                       AND e.new_value = 'appraised') THEN 1
        ELSE 0
      END) AS appraised,
  SUM(CASE WHEN a.status = 'won' THEN 1 ELSE 0 END) AS booked,
  SUM(CASE WHEN a.status = 'lost' THEN 1 ELSE 0 END) AS lost,
  SUM(CASE WHEN a.status IN ('draft', 'appraised', 'presented') THEN 1 ELSE 0 END)
    AS open_worksheets
FROM appraisal a
GROUP BY a.appraiser;

-- Report 2 — recon variance by line category, across every worksheet.
--
-- This is the store-wide reading of what GET .../recon-variance serves for one
-- car: which categories the estimates are honest about and which ones the
-- shop keeps running over. It is grouped by the category vocabulary that
-- 002_worksheet.sql fixed as a CHECK constraint precisely so this report would
-- mean something.
--
-- `unposted_lines` is carried beside the money, not folded into it: a category
-- whose lines are still unposted reads as a large negative variance, which is
-- unfinished recon rather than money saved.
CREATE VIEW report_recon_variance AS
SELECT
  l.category AS category,
  COUNT(*) AS line_count,
  SUM(l.estimate) AS estimate,
  SUM(COALESCE((SELECT SUM(p.amount) FROM recon_actual p
                WHERE p.recon_line_id = l.id), 0)) AS actual,
  SUM(COALESCE((SELECT SUM(p.amount) FROM recon_actual p
                WHERE p.recon_line_id = l.id), 0)) - SUM(l.estimate) AS variance,
  SUM(CASE WHEN EXISTS (SELECT 1 FROM recon_actual p
                        WHERE p.recon_line_id = l.id)
           THEN 0 ELSE 1 END) AS unposted_lines
FROM recon_line l
GROUP BY l.category;

-- Report 3 — front gross by appraiser, over WON worksheets only.
--
-- dealdesk carries no retail selling price: SPEC.md's non-goals refuse desking
-- beyond the trade worksheet, so there is no deal jacket here to subtract a
-- cost from. The front gross this report can honestly show is the gross the
-- worksheet PLANNED — offer_input.target_gross, the very number the offer math
-- subtracted to reach the recommended trade value — less what recon has run
-- over since. Recon overage comes out of front gross in every store, so:
--
--     projected_gross = target_gross − recon variance
--
-- A won worksheet nobody ever priced has no offer_input row; it counts as a
-- book with a planned gross of zero rather than dropping out of the report.
--
-- `unposted_lines` travels with the money because a projected gross built on
-- recon that has not finished posting is provisional. While it is above zero,
-- recon_variance still contains lines whose estimate nothing has answered yet.
CREATE VIEW report_front_gross AS
SELECT
  a.appraiser AS appraiser,
  COUNT(*) AS won_count,
  SUM(COALESCE(o.target_gross, 0)) AS target_gross,
  SUM(COALESCE(r.actual, 0) - COALESCE(r.estimate, 0)) AS recon_variance,
  SUM(COALESCE(o.target_gross, 0)
      - (COALESCE(r.actual, 0) - COALESCE(r.estimate, 0))) AS projected_gross,
  SUM(COALESCE(r.unposted_lines, 0)) AS unposted_lines
FROM appraisal a
LEFT JOIN offer_input o ON o.appraisal_id = a.id
LEFT JOIN appraisal_recon_rollup r ON r.appraisal_id = a.id
WHERE a.status = 'won'
GROUP BY a.appraiser;
