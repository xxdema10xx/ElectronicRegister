-- Explicit child FK indexes added in 006 are now covered by the UNIQUE triples.
-- No FK, row or legacy index is removed. Replacements retain the same left prefix.
ALTER TABLE exam_submission DROP INDEX idx_mt3a_exam_submission_exam;
-- MT3A-BATCH
-- Replacement: uq_mt3a_question_exam(institution_id,exam_id,id).
ALTER TABLE question DROP INDEX idx_mt3a_question_exam;
-- MT3A-BATCH
-- Replacement: uq_mt3a_option_question(institution_id,question_id,id).
ALTER TABLE question_option DROP INDEX idx_mt3a_question_option_question;
