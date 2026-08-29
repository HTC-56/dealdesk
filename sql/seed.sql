-- seed.sql — the demo month, SPEC.md feature 8. NOT a migration.
--
-- Migrator only picks up `NNN_name.sql`, so this script is embedded beside the
-- migrations but never applied by them. Data/Seeder.cs runs it on demand, and
-- only into a database whose `appraisal` table is empty: a demo month written
-- twice is two demo months, and no report would read right afterwards.
--
-- Because it only ever runs into an empty table, every id here is EXPLICIT.
-- Worksheets are 1..12 and recon lines are 1..15, so the README quickstart can
-- name a worksheet, the desk page can link one, and a test can assert against
-- one without first querying for its id.
--
-- Every name in this file is invented — three appraisers, three makes that do
-- not exist, and VINs on a fake world manufacturer code that nonetheless carry
-- correct check digits, so `Domain/Vin.cs` accepts every one of them. SPEC.md's
-- non-goals forbid real vendors and forbid decoding a VIN against anything.
--
-- Money is INTEGER whole cents, as everywhere else in the schema.
--
-- THE MONTH, in one paragraph: twelve worksheets across three appraisers,
-- covering all five lifecycle states — 4 won, 2 lost, 2 presented, 2 appraised,
-- 2 draft. One of the two lost cars was appraised before the customer walked
-- and one was lost straight from draft, which is the case report_look_to_book
-- can only tell apart by reading the audit trail. Fifteen recon lines touch all
-- eight categories; nine of them have posted invoices (one of those a supplier
-- credit) and six are still unposted, so the recon reports show both overage
-- and unfinished work rather than one tidy answer.

-- Timestamps: the month is relative to the moment the seed runs, so a demo run
-- on any date reads as "this month" rather than as a fixed month in the past.
-- `n` is days ago; `at` is the round-trip UTC format `created_at` already uses.
CREATE TEMPORARY TABLE seed_day AS
WITH RECURSIVE day(n) AS (
  SELECT 0
  UNION ALL
  SELECT n + 1 FROM day WHERE n < 30
)
SELECT n AS n,
       strftime('%Y-%m-%dT%H:%M:%S.0000000+00:00', 'now', '-' || n || ' days') AS at
FROM day;

-- ---------------------------------------------------------------------------
-- The twelve worksheets.
-- ---------------------------------------------------------------------------

INSERT INTO appraisal
  (id, vin, model_year, make, model, trim_level, miles, appraiser, status,
   created_at, updated_at)
VALUES
  (1, 'ZZ9ZZ99Z3Z9000101', 2019, 'Meridian', 'Trailhead', 'LT', 74311,
   'A. Whitfield', 'won',
   (SELECT at FROM seed_day WHERE n = 27), (SELECT at FROM seed_day WHERE n = 20)),
  (2, 'ZZ9ZZ99Z5Z9000102', 2020, 'Halcyon', 'Vantage', 'SE', 51806,
   'A. Whitfield', 'won',
   (SELECT at FROM seed_day WHERE n = 24), (SELECT at FROM seed_day WHERE n = 17)),
  (3, 'ZZ9ZZ99Z7Z9000103', 2017, 'Ridgeway', 'Sable', 'Base', 98240,
   'A. Whitfield', 'lost',
   (SELECT at FROM seed_day WHERE n = 22), (SELECT at FROM seed_day WHERE n = 19)),
  (4, 'ZZ9ZZ99Z9Z9000104', 2021, 'Meridian', 'Crestline', 'Touring', 33175,
   'A. Whitfield', 'presented',
   (SELECT at FROM seed_day WHERE n = 12), (SELECT at FROM seed_day WHERE n = 8)),
  (5, 'ZZ9ZZ99Z0Z9000105', 2018, 'Halcyon', 'Larkspur', 'Sport', 66902,
   'A. Whitfield', 'draft',
   (SELECT at FROM seed_day WHERE n = 3), (SELECT at FROM seed_day WHERE n = 3)),
  (6, 'ZZ9ZZ99Z2Z9000106', 2016, 'Ridgeway', 'Foxglove', 'Base', 112430,
   'B. Ferreira', 'won',
   (SELECT at FROM seed_day WHERE n = 26), (SELECT at FROM seed_day WHERE n = 18)),
  (7, 'ZZ9ZZ99Z4Z9000107', 2022, 'Meridian', 'Trailhead', 'LT', 21588,
   'B. Ferreira', 'appraised',
   (SELECT at FROM seed_day WHERE n = 10), (SELECT at FROM seed_day WHERE n = 9)),
  (8, 'ZZ9ZZ99Z6Z9000108', 2015, 'Halcyon', 'Vantage', 'Base', 134077,
   'B. Ferreira', 'lost',
   (SELECT at FROM seed_day WHERE n = 21), (SELECT at FROM seed_day WHERE n = 21)),
  (9, 'ZZ9ZZ99Z8Z9000109', 2020, 'Ridgeway', 'Sable', 'Sport', 47913,
   'B. Ferreira', 'presented',
   (SELECT at FROM seed_day WHERE n = 7), (SELECT at FROM seed_day WHERE n = 5)),
  (10, 'ZZ9ZZ99Z4Z9000110', 2019, 'Meridian', 'Crestline', 'SE', 68455,
   'C. Delacroix', 'won',
   (SELECT at FROM seed_day WHERE n = 25), (SELECT at FROM seed_day WHERE n = 16)),
  (11, 'ZZ9ZZ99Z6Z9000111', 2021, 'Halcyon', 'Larkspur', 'Touring', 29640,
   'C. Delacroix', 'appraised',
   (SELECT at FROM seed_day WHERE n = 6), (SELECT at FROM seed_day WHERE n = 6)),
  (12, 'ZZ9ZZ99Z8Z9000112', 2018, 'Ridgeway', 'Foxglove', 'LT', 81229,
   'C. Delacroix', 'draft',
   (SELECT at FROM seed_day WHERE n = 1), (SELECT at FROM seed_day WHERE n = 1));

-- ---------------------------------------------------------------------------
-- The condition walk — two lines on every worksheet, so no worksheet on the
-- desk page opens empty.
-- ---------------------------------------------------------------------------

INSERT INTO walk_item (appraisal_id, area, severity, note, created_at)
VALUES
  (1, 'exterior', 'moderate', 'Scuff along the driver door',
   (SELECT at FROM seed_day WHERE n = 27)),
  (1, 'tires', 'severe', 'All four down to the wear bars',
   (SELECT at FROM seed_day WHERE n = 27)),
  (2, 'exterior', 'moderate', 'Quarter panel scraped at the wheel arch',
   (SELECT at FROM seed_day WHERE n = 24)),
  (2, 'interior', 'minor', 'Cargo liner missing',
   (SELECT at FROM seed_day WHERE n = 24)),
  (3, 'mechanical', 'moderate', 'Idles rough from cold',
   (SELECT at FROM seed_day WHERE n = 22)),
  (3, 'glass', 'minor', 'Chip low on the passenger side',
   (SELECT at FROM seed_day WHERE n = 22)),
  (4, 'mechanical', 'severe', 'Timing service overdue by the book',
   (SELECT at FROM seed_day WHERE n = 12)),
  (4, 'exterior', 'moderate', 'Front bumper repainted before we saw it',
   (SELECT at FROM seed_day WHERE n = 12)),
  (5, 'interior', 'minor', 'Seat bolster worn on the driver side',
   (SELECT at FROM seed_day WHERE n = 3)),
  (5, 'electronics', 'minor', 'One key fob only',
   (SELECT at FROM seed_day WHERE n = 3)),
  (6, 'mechanical', 'moderate', 'Coolant weeping at the upper hose',
   (SELECT at FROM seed_day WHERE n = 26)),
  (6, 'glass', 'severe', 'Windshield cracked across the sweep',
   (SELECT at FROM seed_day WHERE n = 26)),
  (7, 'other', 'minor', 'Second key never handed over',
   (SELECT at FROM seed_day WHERE n = 10)),
  (7, 'exterior', 'minor', 'Clean panels, no repaint',
   (SELECT at FROM seed_day WHERE n = 10)),
  (8, 'mechanical', 'severe', 'Transmission slips under load',
   (SELECT at FROM seed_day WHERE n = 21)),
  (8, 'interior', 'moderate', 'Headliner sagging at the rear',
   (SELECT at FROM seed_day WHERE n = 21)),
  (9, 'tires', 'moderate', 'One tire down to the cords',
   (SELECT at FROM seed_day WHERE n = 7)),
  (9, 'exterior', 'minor', 'Light rock chipping on the hood',
   (SELECT at FROM seed_day WHERE n = 7)),
  (10, 'tires', 'moderate', 'Fronts mismatched to the rears',
   (SELECT at FROM seed_day WHERE n = 25)),
  (10, 'exterior', 'minor', 'Swirl marks through the clear coat',
   (SELECT at FROM seed_day WHERE n = 25)),
  (11, 'interior', 'moderate', 'Carpet stained in the rear footwell',
   (SELECT at FROM seed_day WHERE n = 6)),
  (11, 'electronics', 'minor', 'Rear camera slow to wake',
   (SELECT at FROM seed_day WHERE n = 6)),
  (12, 'exterior', 'moderate', 'Tailgate dented at the handle',
   (SELECT at FROM seed_day WHERE n = 1)),
  (12, 'mechanical', 'minor', 'Brakes have life left',
   (SELECT at FROM seed_day WHERE n = 1));

-- ---------------------------------------------------------------------------
-- Hand-entered comps. Each set averages to a round anchor on purpose, so the
-- derivation `GET .../offer` prints reads as arithmetic anyone can follow.
--
-- Worksheet 12 gets NO comps: it is the car that just landed, and it is the
-- worksheet that demonstrates the 409 an unpriceable offer answers with.
-- ---------------------------------------------------------------------------

INSERT INTO comp (appraisal_id, label, model_year, miles, price, note, created_at)
VALUES
  (1, 'Auction, same week', 2019, 71400, 1845000, 'Similar trim',
   (SELECT at FROM seed_day WHERE n = 27)),
  (1, 'Retail listing, 30 miles out', 2019, 68900, 1860000, 'Cleaner history',
   (SELECT at FROM seed_day WHERE n = 27)),
  (1, 'Wholesale offer', 2019, 79200, 1845000, 'Higher miles',
   (SELECT at FROM seed_day WHERE n = 27)),
  (2, 'Auction, same week', 2020, 49800, 2140000, 'Same trim',
   (SELECT at FROM seed_day WHERE n = 24)),
  (2, 'Retail listing', 2020, 44100, 2155000, 'Lower miles',
   (SELECT at FROM seed_day WHERE n = 24)),
  (2, 'Wholesale offer', 2020, 58600, 2125000, 'Higher miles',
   (SELECT at FROM seed_day WHERE n = 24)),
  (3, 'Auction, prior week', 2017, 95100, 1225000, 'Comparable miles',
   (SELECT at FROM seed_day WHERE n = 22)),
  (3, 'Retail listing', 2017, 88700, 1240000, 'Better condition',
   (SELECT at FROM seed_day WHERE n = 22)),
  (3, 'Wholesale offer', 2017, 104300, 1210000, 'Rougher car',
   (SELECT at FROM seed_day WHERE n = 22)),
  (4, 'Auction, same week', 2021, 31200, 2880000, 'Same trim',
   (SELECT at FROM seed_day WHERE n = 12)),
  (4, 'Retail listing', 2021, 27500, 2910000, 'Lower miles',
   (SELECT at FROM seed_day WHERE n = 12)),
  (4, 'Wholesale offer', 2021, 38800, 2850000, 'Higher miles',
   (SELECT at FROM seed_day WHERE n = 12)),
  (5, 'Auction, same week', 2018, 64300, 1590000, 'Comparable',
   (SELECT at FROM seed_day WHERE n = 3)),
  (5, 'Retail listing', 2018, 61100, 1610000, 'Cleaner car',
   (SELECT at FROM seed_day WHERE n = 3)),
  (6, 'Auction, prior week', 2016, 118200, 985000, 'Comparable miles',
   (SELECT at FROM seed_day WHERE n = 26)),
  (6, 'Retail listing', 2016, 109400, 1000000, 'Better tires',
   (SELECT at FROM seed_day WHERE n = 26)),
  (6, 'Wholesale offer', 2016, 126700, 970000, 'Higher miles',
   (SELECT at FROM seed_day WHERE n = 26)),
  (7, 'Auction, same week', 2022, 19800, 2460000, 'Same trim',
   (SELECT at FROM seed_day WHERE n = 10)),
  (7, 'Retail listing', 2022, 17300, 2485000, 'Lower miles',
   (SELECT at FROM seed_day WHERE n = 10)),
  (7, 'Wholesale offer', 2022, 26400, 2435000, 'Higher miles',
   (SELECT at FROM seed_day WHERE n = 10)),
  (9, 'Auction, same week', 2020, 45600, 2050000, 'Same trim',
   (SELECT at FROM seed_day WHERE n = 7)),
  (9, 'Retail listing', 2020, 41200, 2075000, 'Lower miles',
   (SELECT at FROM seed_day WHERE n = 7)),
  (9, 'Wholesale offer', 2020, 52900, 2025000, 'Higher miles',
   (SELECT at FROM seed_day WHERE n = 7)),
  (10, 'Auction, prior week', 2019, 65800, 1720000, 'Comparable',
   (SELECT at FROM seed_day WHERE n = 25)),
  (10, 'Retail listing', 2019, 60400, 1745000, 'Lower miles',
   (SELECT at FROM seed_day WHERE n = 25)),
  (10, 'Wholesale offer', 2019, 73100, 1695000, 'Higher miles',
   (SELECT at FROM seed_day WHERE n = 25)),
  (11, 'Auction, same week', 2021, 28100, 2610000, 'Same trim',
   (SELECT at FROM seed_day WHERE n = 6)),
  (11, 'Retail listing', 2021, 24700, 2635000, 'Lower miles',
   (SELECT at FROM seed_day WHERE n = 6)),
  (11, 'Wholesale offer', 2021, 34500, 2585000, 'Higher miles',
   (SELECT at FROM seed_day WHERE n = 6));

-- ---------------------------------------------------------------------------
-- The itemized recon estimate — fifteen lines, all eight categories.
-- ---------------------------------------------------------------------------

INSERT INTO recon_line (id, appraisal_id, category, description, estimate, created_at)
VALUES
  (1, 1, 'mechanical', 'Front brake service', 45000,
   (SELECT at FROM seed_day WHERE n = 26)),
  (2, 1, 'tires', 'Four tires mounted and balanced', 80000,
   (SELECT at FROM seed_day WHERE n = 26)),
  (3, 1, 'detail', 'Full detail before the line', 15000,
   (SELECT at FROM seed_day WHERE n = 26)),
  (4, 2, 'body', 'Quarter panel dent pull', 60000,
   (SELECT at FROM seed_day WHERE n = 23)),
  (5, 2, 'paint', 'Quarter panel blend', 120000,
   (SELECT at FROM seed_day WHERE n = 23)),
  (6, 6, 'mechanical', 'Upper radiator hose and coolant', 30000,
   (SELECT at FROM seed_day WHERE n = 25)),
  (7, 6, 'glass', 'Windshield replacement', 42000,
   (SELECT at FROM seed_day WHERE n = 25)),
  (8, 6, 'interior', 'Headliner re-glue', 25000,
   (SELECT at FROM seed_day WHERE n = 25)),
  (9, 10, 'tires', 'Two front tires', 40000,
   (SELECT at FROM seed_day WHERE n = 24)),
  (10, 10, 'detail', 'Paint correction and seal', 22000,
   (SELECT at FROM seed_day WHERE n = 24)),
  (11, 4, 'mechanical', 'Timing service at the book interval', 95000,
   (SELECT at FROM seed_day WHERE n = 11)),
  (12, 4, 'paint', 'Front bumper respray', 55000,
   (SELECT at FROM seed_day WHERE n = 11)),
  (13, 7, 'other', 'Cut and program a second key', 18000,
   (SELECT at FROM seed_day WHERE n = 9)),
  (14, 9, 'tires', 'One tire replaced', 12000,
   (SELECT at FROM seed_day WHERE n = 6)),
  (15, 11, 'interior', 'Carpet extraction', 20000,
   (SELECT at FROM seed_day WHERE n = 6));

-- ---------------------------------------------------------------------------
-- Recon actuals. Eleven postings against nine of the fifteen lines, so six
-- lines are still unposted and every recon report carries an unpostedLines
-- count that means something. Line 4 shows the shape the schema exists for:
-- two postings, the second a negative supplier credit.
-- ---------------------------------------------------------------------------

INSERT INTO recon_actual (recon_line_id, amount, description, posted_by, posted_at)
VALUES
  (1, 52500, 'Brake job, pads and rotors', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 23)),
  (2, 78000, 'Four tires, fleet pricing', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 23)),
  (3, 15000, 'Detail department', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 21)),
  (4, 90000, 'Body shop invoice', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 20)),
  (4, -15000, 'Credit, panel returned undamaged', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 19)),
  (5, 65000, 'Paint booth, first pass', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 20)),
  (5, 45000, 'Paint booth, blend into the door', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 18)),
  (6, 41000, 'Hose, clamp and coolant flush', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 22)),
  (7, 42000, 'Glass vendor, mobile fit', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 21)),
  (9, 36500, 'Two tires, fleet pricing', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 20)),
  (10, 24000, 'Paint correction, outside detailer', 'R. Vasquez',
   (SELECT at FROM seed_day WHERE n = 18));

-- ---------------------------------------------------------------------------
-- The two store numbers, on every worksheet anyone actually priced. Worksheets
-- 5 and 12 are drafts and worksheet 8 was lost before it was priced, so those
-- three have no row and read back as pack 0 / target 0.
-- ---------------------------------------------------------------------------

INSERT INTO offer_input (appraisal_id, pack, target_gross, anchor_override, updated_at)
VALUES
  (1, 90000, 150000, NULL, (SELECT at FROM seed_day WHERE n = 26)),
  (2, 90000, 180000, NULL, (SELECT at FROM seed_day WHERE n = 23)),
  (3, 90000, 140000, NULL, (SELECT at FROM seed_day WHERE n = 21)),
  (4, 90000, 200000, NULL, (SELECT at FROM seed_day WHERE n = 11)),
  (6, 90000, 150000, NULL, (SELECT at FROM seed_day WHERE n = 25)),
  (7, 90000, 175000, NULL, (SELECT at FROM seed_day WHERE n = 9)),
  (9, 90000, 160000, NULL, (SELECT at FROM seed_day WHERE n = 6)),
  (10, 90000, 165000, NULL, (SELECT at FROM seed_day WHERE n = 24)),
  (11, 90000, 190000, NULL, (SELECT at FROM seed_day WHERE n = 6));

-- ---------------------------------------------------------------------------
-- The audit trail — twenty-four entries: twenty-one lifecycle moves and three
-- field revisions. Worksheet 3 has a move INTO `appraised` before it was lost;
-- worksheet 8 does not, because that customer walked before anyone priced the
-- car. That difference is the whole reason report_look_to_book reads this
-- table instead of trusting the current status.
-- ---------------------------------------------------------------------------

INSERT INTO audit_entry
  (appraisal_id, field, old_value, new_value, changed_by, reason, changed_at)
VALUES
  (1, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 26)),
  (1, 'miles', '74113', '74311', 'A. Whitfield', 'Odometer corrected from the title',
   (SELECT at FROM seed_day WHERE n = 25)),
  (1, 'status', 'appraised', 'presented', 'D. Okonjo', 'Offer presented to the customer',
   (SELECT at FROM seed_day WHERE n = 23)),
  (1, 'status', 'presented', 'won', 'D. Okonjo', 'Customer accepted the trade',
   (SELECT at FROM seed_day WHERE n = 20)),
  (2, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 23)),
  (2, 'status', 'appraised', 'presented', 'D. Okonjo', 'Offer presented to the customer',
   (SELECT at FROM seed_day WHERE n = 20)),
  (2, 'status', 'presented', 'won', 'D. Okonjo', 'Customer accepted the trade',
   (SELECT at FROM seed_day WHERE n = 17)),
  (3, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 21)),
  (3, 'status', 'appraised', 'lost', 'D. Okonjo', 'Customer kept the vehicle',
   (SELECT at FROM seed_day WHERE n = 19)),
  (4, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 11)),
  (4, 'modelYear', '2020', '2021', 'A. Whitfield', 'Model year corrected from the door sticker',
   (SELECT at FROM seed_day WHERE n = 10)),
  (4, 'status', 'appraised', 'presented', 'D. Okonjo', 'Offer presented to the customer',
   (SELECT at FROM seed_day WHERE n = 8)),
  (6, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 25)),
  (6, 'status', 'appraised', 'presented', 'D. Okonjo', 'Offer presented to the customer',
   (SELECT at FROM seed_day WHERE n = 21)),
  (6, 'status', 'presented', 'won', 'D. Okonjo', 'Customer accepted the trade',
   (SELECT at FROM seed_day WHERE n = 18)),
  (7, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 9)),
  (8, 'status', 'draft', 'lost', 'D. Okonjo', 'Customer walked before we priced it',
   (SELECT at FROM seed_day WHERE n = 21)),
  (9, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 6)),
  (9, 'status', 'appraised', 'presented', 'D. Okonjo', 'Offer presented to the customer',
   (SELECT at FROM seed_day WHERE n = 5)),
  (10, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 24)),
  (10, 'trimLevel', 'LT', 'SE', 'C. Delacroix', 'Trim corrected from the window sticker',
   (SELECT at FROM seed_day WHERE n = 22)),
  (10, 'status', 'appraised', 'presented', 'D. Okonjo', 'Offer presented to the customer',
   (SELECT at FROM seed_day WHERE n = 19)),
  (10, 'status', 'presented', 'won', 'D. Okonjo', 'Customer accepted the trade',
   (SELECT at FROM seed_day WHERE n = 16)),
  (11, 'status', 'draft', 'appraised', 'D. Okonjo', 'Desk review complete',
   (SELECT at FROM seed_day WHERE n = 6));

DROP TABLE seed_day;
