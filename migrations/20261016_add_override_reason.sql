-- Migration: add override_reason column to exam_submission
-- Compatible with MySQL 5.7.24
ALTER TABLE exam_submission
  ADD COLUMN override_reason VARCHAR(1000) NULL AFTER suggested_grade;

-- End of migration
