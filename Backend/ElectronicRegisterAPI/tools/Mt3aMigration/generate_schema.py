"""Deterministic MT-3A SQL source generator. No DB connection or secrets.

Keep the generated SQL under migrations/mt3; never overwrite 001/002.
"""
from pathlib import Path
import sys

OUT = Path(sys.argv[1]).resolve() if len(sys.argv)>1 else Path(__file__).resolve().parents[4] / 'migrations' / 'mt3'
TABLES = ['biennium','study_area','study_path','subject','teacher','student',
          'biennium_study_area','biennium_study_path','class','class_subject',
          'exam','exam_student','grade','exam_submission','question','question_option',
          'submission_answer','submission_answer_option','exam_violation']
# child, foreign-key column, parent, original constraint, historical delete action
EDGES = [
 ('biennium_study_area','biennium_id','biennium','fk_bsa_biennium','CASCADE'),
 ('biennium_study_area','study_area_id','study_area','fk_bsa_area','RESTRICT'),
 ('biennium_study_path','biennium_study_area_id','biennium_study_area','fk_bsp_bsa','CASCADE'),
 ('biennium_study_path','study_path_id','study_path','fk_bsp_path','RESTRICT'),
 ('class','biennium_study_path_id','biennium_study_path','fk_class_bsp','RESTRICT'),
 ('class_subject','class_id','class','fk_cs_class','CASCADE'),
 ('class_subject','subject_id','subject','fk_cs_subject','RESTRICT'),
 ('class_subject','teacher_id','teacher','fk_cs_teacher','RESTRICT'),
 ('student','class_id','class','fk_student_class','SET NULL'),
 ('exam','class_subject_id','class_subject','fk_exam_class_subject','RESTRICT'),
 ('exam','retake_of_exam_id','exam','fk_exam_retake_of','SET NULL'),
 ('exam_student','exam_id','exam','fk_exam_student_exam','CASCADE'),
 ('exam_student','student_id','student','fk_exam_student_student','RESTRICT'),
 ('grade','student_id','student','fk_grade_student','CASCADE'),
 ('grade','class_subject_id','class_subject','fk_grade_class_subject','RESTRICT'),
 ('exam_submission','exam_id','exam','fk_exam_submission_exam','RESTRICT'),
 ('exam_submission','student_id','student','fk_exam_submission_student','RESTRICT'),
 ('exam_submission','grade_id','grade','fk_exam_submission_grade','SET NULL'),
 ('question','exam_id','exam','fk_question_exam','CASCADE'),
 ('question_option','question_id','question','fk_question_option_question','CASCADE'),
 ('submission_answer','submission_id','exam_submission','fk_submission_answer_submission','CASCADE'),
 ('submission_answer','question_id','question','fk_submission_answer_question','RESTRICT'),
 ('submission_answer_option','submission_answer_id','submission_answer','fk_submission_answer_option_answer','CASCADE'),
 ('submission_answer_option','option_id','question_option','fk_submission_answer_option_option','RESTRICT'),
 ('exam_violation','submission_id','exam_submission','fk_exam_violation_submission','CASCADE'),
]
SCOPE = {('student','class_id'):'class_institution_id',
         ('exam','retake_of_exam_id'):'retake_institution_id',
         ('exam_submission','grade_id'):'grade_institution_id'}
GUID = 'CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci'
BATCH = '\n-- MT3A-BATCH\n'
def write(name, statements):
    text='-- MT-3A / MySQL 5.7. Run only through guarded test tooling.\n'+BATCH.join(statements)+'\n'
    (OUT/name).write_text('\n'.join(line.rstrip() for line in text.splitlines())+'\n', encoding='utf-8')
def refname(old): return old.replace('fk_', 'fk_mt3a_', 1)

expand = [f'ALTER TABLE `{t}` ADD COLUMN institution_id {GUID} NULL;' for t in TABLES+['audit_event']]
for (t,col),scope in SCOPE.items(): expand.append(f'ALTER TABLE `{t}` ADD COLUMN {scope} {GUID} NULL;')
expand += [f'ALTER TABLE submission_answer ADD COLUMN exam_id {GUID} NULL;',
           f'ALTER TABLE submission_answer_option ADD COLUMN question_id {GUID} NULL;']
write('003_ownership_expand.sql',expand)

# Validation runs BEFORE DML and again after it. Null means proposed legacy ownership,
# never the first tenant. Existing non-null owners and profile links must agree.
validate = []
for table in TABLES+['audit_event']:
    validate.append(f"SELECT '{table}_institution_exists' AS rule,COUNT(*) AS violations FROM `{table}` r LEFT JOIN institution i ON i.id=r.institution_id WHERE r.institution_id IS NOT NULL AND i.id IS NULL;")
for child,col,parent,old,delete in EDGES:
    validate.append(f"SELECT '{old}' AS rule, COUNT(*) AS violations FROM `{child}` c LEFT JOIN `{parent}` p ON p.id=c.{col} WHERE c.{col} IS NOT NULL AND (p.id IS NULL OR COALESCE(c.institution_id,@legacy_id) <> COALESCE(p.institution_id,@legacy_id));")
for kind in ['student','teacher']:
    validate.append(f"SELECT '{kind}_profile_owner' AS rule,COUNT(*) AS violations FROM institution_{kind}_profile l JOIN `{kind}` p ON p.id=l.{kind}_id WHERE l.institution_id <> COALESCE(p.institution_id,@legacy_id);")
validate += [
 "SELECT 'answer_exam' AS rule,COUNT(*) AS violations FROM submission_answer a JOIN exam_submission s ON s.id=a.submission_id JOIN question q ON q.id=a.question_id WHERE s.exam_id<>q.exam_id;",
 "SELECT 'selected_option_question' AS rule,COUNT(*) AS violations FROM submission_answer_option b JOIN submission_answer a ON a.id=b.submission_answer_id JOIN question_option o ON o.id=b.option_id WHERE a.question_id<>o.question_id;",
 "SELECT 'audit_unresolved' AS rule,COUNT(*) AS violations FROM audit_event a LEFT JOIN exam e ON a.entity_type='exam' AND e.id=a.entity_id LEFT JOIN exam_submission s ON a.entity_type='exam_submission' AND s.id=a.entity_id WHERE NOT (a.entity_type='platform' AND a.action LIKE 'platform.%' AND a.entity_id IS NULL AND a.institution_id IS NULL AND a.before_json IS NULL AND a.after_json IS NULL) AND ((a.entity_type NOT IN ('exam','exam_submission')) OR (a.institution_id IS NULL AND e.id IS NULL AND s.id IS NULL) OR (a.institution_id IS NOT NULL AND COALESCE(e.institution_id,s.institution_id) IS NOT NULL AND a.institution_id<>COALESCE(e.institution_id,s.institution_id)));"
]
write('004_ownership_preflight.sql',validate)
backfill = [f'UPDATE `{t}` SET institution_id=@legacy_id WHERE institution_id IS NULL;' for t in TABLES]
for (t,col),scope in SCOPE.items():
    # Explicit updated_at assignment prevents MySQL ON UPDATE from changing legacy cells.
    stamp=', updated_at=updated_at' if t in ['exam','exam_submission'] else ''
    backfill.append(f'UPDATE `{t}` SET {scope}=IF({col} IS NULL,NULL,institution_id){stamp};')
# The broad owner updates must also preserve the two legacy timestamp columns.
backfill = [s.replace('SET institution_id=@legacy_id WHERE', 'SET institution_id=@legacy_id, updated_at=updated_at WHERE') if s.startswith(('UPDATE `exam`','UPDATE `exam_submission`')) else s for s in backfill]
backfill += [
 'UPDATE submission_answer a JOIN exam_submission s ON s.id=a.submission_id SET a.exam_id=s.exam_id;',
 'UPDATE submission_answer_option b JOIN submission_answer a ON a.id=b.submission_answer_id SET b.question_id=a.question_id;',
 "UPDATE audit_event a LEFT JOIN exam e ON a.entity_type='exam' AND e.id=a.entity_id LEFT JOIN exam_submission s ON a.entity_type='exam_submission' AND s.id=a.entity_id SET a.institution_id=COALESCE(e.institution_id,s.institution_id) WHERE a.institution_id IS NULL;"
]
write('005_ownership_backfill.sql',backfill)

enforce=[]
for t in TABLES:
    enforce.append(f"ALTER TABLE `{t}` MODIFY institution_id {GUID} NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';")
    if t not in ['exam_student','submission_answer_option']:
        enforce.append(f'ALTER TABLE `{t}` ADD UNIQUE KEY uq_mt3a_{t}_owner (institution_id,id);')
    enforce.append(f'ALTER TABLE `{t}` ADD CONSTRAINT fk_mt3a_{t}_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;')
enforce += [
 'ALTER TABLE audit_event ADD KEY idx_mt3a_audit_owner_created (institution_id,created_at), ADD CONSTRAINT fk_mt3a_audit_owner FOREIGN KEY (institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT;',
 f"ALTER TABLE submission_answer MODIFY exam_id {GUID} NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';",
 f"ALTER TABLE submission_answer_option MODIFY question_id {GUID} NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';",
 'ALTER TABLE exam_submission ADD UNIQUE KEY uq_mt3a_submission_exam (institution_id,exam_id,id);',
 'ALTER TABLE question ADD UNIQUE KEY uq_mt3a_question_exam (institution_id,exam_id,id);',
 'ALTER TABLE submission_answer ADD UNIQUE KEY uq_mt3a_answer_question (institution_id,question_id,id);',
 'ALTER TABLE question_option ADD UNIQUE KEY uq_mt3a_option_question (institution_id,question_id,id);',
]
for child,col,parent,old,delete in EDGES:
    scope=SCOPE.get((child,col),'institution_id')
    childcols=f'{scope},{col}'
    parentcols='institution_id,id'
    if child=='submission_answer':
        childcols=f'institution_id,exam_id,{col}'
        parentcols='institution_id,exam_id,id'
    if child=='submission_answer_option':
        childcols=f'institution_id,question_id,{col}'
        parentcols='institution_id,question_id,id'
    # Historical FK stays: EF navigation/ordering stays stable; no replacement or
    # delete-action change. Both FKs have the same historical delete semantics.
    enforce.append(f'ALTER TABLE `{child}` ADD KEY idx_{refname(old)[3:]} ({childcols}), ADD CONSTRAINT {refname(old)} FOREIGN KEY ({childcols}) REFERENCES `{parent}` ({parentcols}) ON DELETE {delete} ON UPDATE RESTRICT;')
for kind in ['student','teacher']:
    enforce.append(f'ALTER TABLE institution_{kind}_profile ADD KEY idx_mt3a_{kind}_profile (institution_id,{kind}_id), ADD CONSTRAINT fk_mt3a_{kind}_profile_owner FOREIGN KEY (institution_id,{kind}_id) REFERENCES `{kind}` (institution_id,id) ON DELETE RESTRICT ON UPDATE RESTRICT;')
for t,index,cols in [('biennium','uq_biennium','start_year,end_year'),('study_area','uq_study_area_name','name'),('study_path','uq_study_path_name','name'),('subject','uq_subject_name','name')]:
    enforce.append(f'ALTER TABLE `{t}` DROP INDEX {index}, ADD UNIQUE KEY {index} (institution_id,{cols});')
write('006_ownership_enforce.sql',enforce)

# No fallback to an HTTP tenant. Missing root ownership can only use an explicitly
# installed, provisioning-only legacy test manifest. New owned writes supply owner.
triggers=[f"CREATE TABLE mt3a_legacy_scope (scope ENUM('legacy') NOT NULL PRIMARY KEY, institution_id {GUID} NOT NULL UNIQUE, CONSTRAINT fk_mt3a_legacy_scope FOREIGN KEY(institution_id) REFERENCES institution(id) ON DELETE RESTRICT ON UPDATE RESTRICT) ENGINE=InnoDB;"]
for t in TABLES:
    edges=[e for e in EDGES if e[0]==t]
    root=t in ['biennium','study_area','study_path','subject','teacher','student']
    for operation in ['INSERT','UPDATE']:
        body=[]
        if operation=='UPDATE':
            body.append("IF NOT(NEW.institution_id <=> OLD.institution_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A owner is immutable'; END IF;")
        if operation=='INSERT':
            if root:
                source="(SELECT institution_id FROM mt3a_legacy_scope WHERE scope='legacy')"
                # A Student with a class inherits that class; an unclassified root
                # still requires the explicit legacy manifest.
                if t=='student': source=f"COALESCE((SELECT institution_id FROM class WHERE id=NEW.class_id),{source})"
            else:
                _,col,parent,*_=edges[0]
                source=f'(SELECT institution_id FROM `{parent}` WHERE id=NEW.{col})'
            body.append(f"IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SET NEW.institution_id={source}; END IF;")
        body.append("IF NEW.institution_id IS NULL OR NEW.institution_id='00000000-0000-0000-0000-000000000000' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A explicit owner required'; END IF;")
        for (child,col),scope in SCOPE.items():
            if child==t: body.append(f'SET NEW.{scope}=IF(NEW.{col} IS NULL,NULL,NEW.institution_id);')
        if t=='submission_answer': body.append('SET NEW.exam_id=(SELECT exam_id FROM exam_submission WHERE id=NEW.submission_id);')
        if t=='submission_answer_option': body.append('SET NEW.question_id=(SELECT question_id FROM submission_answer WHERE id=NEW.submission_answer_id);')
        triggers.append(f'CREATE TRIGGER mt3a_{t}_{operation.lower()} BEFORE {operation} ON `{t}` FOR EACH ROW BEGIN\n  '+'\n  '.join(body)+'\nEND;')
# Polymorphic audit cannot have an entity FK. Known school event attribution is
# validated by trigger; deletion of referenced resource preserves audit ownership.
for op in ['INSERT','UPDATE']:
    immutable="IF NOT(NEW.institution_id <=> OLD.institution_id) OR NOT(NEW.entity_type <=> OLD.entity_type) OR NOT(NEW.entity_id <=> OLD.entity_id) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit attribution immutable'; END IF;" if op=='UPDATE' else ''
    triggers.append(f"""CREATE TRIGGER mt3a_audit_{op.lower()} BEFORE {op} ON audit_event FOR EACH ROW BEGIN
 DECLARE resolved CHAR(36) CHARACTER SET ascii COLLATE ascii_general_ci;
 {immutable}
 IF NEW.entity_type='exam' THEN SET resolved=(SELECT institution_id FROM exam WHERE id=NEW.entity_id);
 ELSEIF NEW.entity_type='exam_submission' THEN SET resolved=(SELECT institution_id FROM exam_submission WHERE id=NEW.entity_id);
 ELSE SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit classification required'; END IF;
 IF resolved IS NULL THEN
   IF '{op}'='INSERT' OR NEW.institution_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit entity unresolved'; END IF;
 ELSE
   IF NEW.institution_id IS NULL THEN SET NEW.institution_id=resolved; END IF;
   IF NEW.institution_id<>resolved THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A audit owner mismatch'; END IF;
 END IF;
 END;""")
write('007_ownership_triggers.sql',triggers)

# Forward-only extension after 007: explicit platform metadata, never reclassify
# unresolved legacy rows as global. DROP here replaces trigger definitions only.
platform=[]
for op in ['INSERT','UPDATE']:
    old=next(s for s in triggers if s.startswith(f'CREATE TRIGGER mt3a_audit_{op.lower()}'))
    body=old.replace(" IF NEW.entity_type='exam' THEN", " IF NEW.entity_type='platform' THEN\n IF NEW.institution_id IS NOT NULL OR NEW.entity_id IS NOT NULL OR NEW.before_json IS NOT NULL OR NEW.after_json IS NOT NULL OR NEW.action NOT LIKE 'platform.%' THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='MT3A platform shape required'; END IF;\n ELSE\n IF NEW.entity_type='exam' THEN")
    body=body.replace(' END;',' END IF;\n END;')
    platform.extend([f'DROP TRIGGER mt3a_audit_{op.lower()};',body])
write('009_explicit_platform_audit.sql',platform)

if __name__=='__main__':
    print('Generated 003-007 and 009 SQL; no database modified.')
