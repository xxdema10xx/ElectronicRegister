-- Revision defaults to zero for every preexisting relation. No historical events fabricated.
-- Rerun is read-only and safe after EXPAND; preserve all dates/statuses and legacy columns.
SELECT COUNT(*) AS invalid_revision FROM student_enrollment WHERE revision IS NULL OR revision<0;
