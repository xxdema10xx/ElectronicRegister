-- Non-destructive index cleanup after 006. No FK or data removed.
-- Replacement: uq_mt3a_option_question(institution_id,question_id,id).
ALTER TABLE question_option DROP INDEX uq_mt3a_question_option_owner;
-- MT3A-BATCH
-- Replacement: uq_mt3a_answer_question(institution_id,question_id,id).
ALTER TABLE submission_answer DROP INDEX uq_mt3a_submission_answer_owner;
-- MT3A-BATCH
-- Institution FK uses idx_mt3a_exam_violation_submission(institution_id,submission_id).
-- id lookups use the unchanged PRIMARY(id). No composite reference targets violation.
ALTER TABLE exam_violation DROP INDEX uq_mt3a_exam_violation_owner;
