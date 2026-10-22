-- Version 20261022.1. Additive MySQL 5.7.24 DDL; run exactly once after preflight and backup.
-- This file deliberately performs no historical name rewrite.
ALTER TABLE exam_submission ADD COLUMN display_order_json LONGTEXT NULL FIRST;
