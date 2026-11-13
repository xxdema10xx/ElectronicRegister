"""Generate additive MySQL 5.7 schema and Database First sidecar from explicit columns.
Does not connect to a database. Review/apply SQL before using mappings.
"""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / 'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence'
GUID = 'char(36) CHARACTER SET ascii'
tables = {
 'formative_plan': [('id',GUID),('institution_id',GUID),('study_path_id',GUID),('code','varchar(64)'),('name','varchar(200)'),('description','text',True),('revision','bigint'),('created_at_utc','datetime(6)')],
 'formative_plan_version': [('id',GUID),('institution_id',GUID),('study_path_id',GUID),('plan_id',GUID),('version_number','int'),('name','varchar(200)'),('status',"enum('Draft','Published')"),('revision','bigint'),('theory_planned_minutes','int'),('internship_planned_minutes','int'),('created_at_utc','datetime(6)'),('published_at_utc','datetime(6)',True)],
 'formative_unit': [('id',GUID),('institution_id',GUID),('version_id',GUID),('code','varchar(64)',True),('name','varchar(200)'),('description','text',True),('display_order','int')],
 'formative_module': [('id',GUID),('institution_id',GUID),('version_id',GUID),('unit_id',GUID),('subject_id',GUID,True),('code','varchar(64)',True),('name','varchar(200)'),('description','text',True),('display_order','int'),('planned_minutes','int')],
 'formative_module_competency': [('id',GUID),('institution_id',GUID),('version_id',GUID),('module_id',GUID),('description','varchar(4000)'),('display_order','int')],
 'formative_module_content': [('id',GUID),('institution_id',GUID),('version_id',GUID),('module_id',GUID),('description','varchar(4000)'),('display_order','int')],
 'course_edition_formative_plan': [('id',GUID),('institution_id',GUID),('study_path_id',GUID),('course_edition_id',GUID),('version_id',GUID),('created_at_utc','datetime(6)')],
 'formative_plan_event': [('id',GUID),('institution_id',GUID),('plan_id',GUID),('version_id',GUID,True),('course_edition_id',GUID,True),('actor_user_id',GUID),('actor_membership_id',GUID),('operation','varchar(40)'),('revision_before','bigint',True),('revision_after','bigint'),('request_hash','char(64) CHARACTER SET ascii COLLATE ascii_bin'),('before_json','json',True),('after_json','json'),('reason','varchar(500)'),('occurred_at_utc','datetime(6)')]
}
constraints = {
 'formative_plan': ['UNIQUE KEY uq_fp_code(institution_id,code)', 'UNIQUE KEY uq_fp_path(institution_id,study_path_id,id)', 'CONSTRAINT fk_fp_path FOREIGN KEY(institution_id,study_path_id) REFERENCES study_path(institution_id,id)'],
 'formative_plan_version': ['UNIQUE KEY uq_fpv_number(institution_id,plan_id,version_number)', 'UNIQUE KEY uq_fpv_path(institution_id,study_path_id,id)', 'CONSTRAINT fk_fpv_plan FOREIGN KEY(institution_id,study_path_id,plan_id) REFERENCES formative_plan(institution_id,study_path_id,id)'],
 'formative_unit': ['UNIQUE KEY uq_fu_version(institution_id,version_id,id)', 'UNIQUE KEY uq_fu_order(institution_id,version_id,display_order)', 'UNIQUE KEY uq_fu_code(institution_id,version_id,code)', 'CONSTRAINT fk_fu_version FOREIGN KEY(institution_id,version_id) REFERENCES formative_plan_version(institution_id,id)'],
 'formative_module': ['UNIQUE KEY uq_fm_version(institution_id,version_id,id)', 'UNIQUE KEY uq_fm_order(institution_id,unit_id,display_order)', 'UNIQUE KEY uq_fm_code(institution_id,unit_id,code)', 'CONSTRAINT fk_fm_unit FOREIGN KEY(institution_id,version_id,unit_id) REFERENCES formative_unit(institution_id,version_id,id)', 'CONSTRAINT fk_fm_subject FOREIGN KEY(institution_id,subject_id) REFERENCES subject(institution_id,id)'],
 'formative_module_competency': ['UNIQUE KEY uq_fmc_order(institution_id,module_id,display_order)', 'CONSTRAINT fk_fmc_module FOREIGN KEY(institution_id,version_id,module_id) REFERENCES formative_module(institution_id,version_id,id)'],
 'formative_module_content': ['UNIQUE KEY uq_fmt_order(institution_id,module_id,display_order)', 'CONSTRAINT fk_fmt_module FOREIGN KEY(institution_id,version_id,module_id) REFERENCES formative_module(institution_id,version_id,id)'],
 'course_edition_formative_plan': ['UNIQUE KEY uq_cefp_edition(institution_id,course_edition_id)', 'CONSTRAINT fk_cefp_edition FOREIGN KEY(institution_id,course_edition_id) REFERENCES course_edition(institution_id,id)', 'CONSTRAINT fk_cefp_version FOREIGN KEY(institution_id,study_path_id,version_id) REFERENCES formative_plan_version(institution_id,study_path_id,id)'],
 'formative_plan_event': ['KEY idx_fpe_history(institution_id,plan_id,occurred_at_utc,id)', 'CONSTRAINT fk_fpe_plan FOREIGN KEY(institution_id,plan_id) REFERENCES formative_plan(institution_id,id)', 'CONSTRAINT fk_fpe_version FOREIGN KEY(institution_id,version_id) REFERENCES formative_plan_version(institution_id,id)', 'CONSTRAINT fk_fpe_edition FOREIGN KEY(institution_id,course_edition_id) REFERENCES course_edition(institution_id,id)', 'CONSTRAINT fk_fpe_actor FOREIGN KEY(institution_id,actor_membership_id,actor_user_id) REFERENCES institution_membership(institution_id,id,user_id)']
}

def pascal(name): return ''.join(x.capitalize() for x in name.split('_'))
sql = ["-- Fase 4 additive MySQL 5.7.24. DDL commits implicitly; preflight and backup required.","SET NAMES utf8mb4 COLLATE utf8mb4_unicode_ci;", "SET SESSION sql_mode='STRICT_TRANS_TABLES,NO_ZERO_DATE,NO_ZERO_IN_DATE,NO_ENGINE_SUBSTITUTION';"]
mapping = ['// Database First additive sidecar; legacy owner shadow mappings remain untouched.', 'using Microsoft.EntityFrameworkCore;', 'using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;', 'namespace ElectronicRegisterAPI.Infrastructure.Persistence;', 'public partial class ElectronicRegisterContext {', 'private static void ConfigureFormativePlans(ModelBuilder b) {']
for table, columns in tables.items():
    declarations = [f" {c[0]} {c[1]} {'DEFAULT NULL' if len(c)>2 and c[2] else 'NOT NULL'}" for c in columns]
    declarations += [' PRIMARY KEY(id)', f' UNIQUE KEY uq_{table}_owner(institution_id,id)']
    declarations += [' '+x for x in constraints[table]]
    sql.append('CREATE TABLE '+table+' (\n'+',\n'.join(declarations)+'\n) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;')
    entity = ['// Database First entity for '+table, '#nullable enable', 'namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;', 'public partial class '+pascal(table)+' {']
    mapping.append('{ var e=b.Entity<'+pascal(table)+'>(); e.ToTable("'+table+'"); e.HasKey(x=>x.Id);')
    for col in columns:
        name, typ = col[:2]; null = len(col)>2 and col[2]
        cstype = 'Guid' if typ.startswith('char(36)') else 'long' if typ=='bigint' else 'int' if typ=='int' else 'DateTime' if typ.startswith('datetime') else 'string'
        entity.append(f' public {cstype}{"?" if null else ""} {pascal(name)} {{get;set;}}'+(' = null!;' if cstype=='string' and not null else ''))
        storetype = typ.split(' CHARACTER')[0]
        mapping.append(f' e.Property(x=>x.{pascal(name)}).HasColumnName("{name}").HasColumnType("{storetype}")'+('.HasCharSet("ascii").UseCollation("ascii_general_ci")' if typ.startswith('char(36)') else '')+('.IsRequired();' if not null else ';'))
    entity.append('}')
    (BASE/'Entities'/f'{pascal(table)}.cs').write_text('\n'.join(entity)+'\n',encoding='utf-8',newline='\n')
    # SQL-only legacy composite FKs intentionally avoid altering owner shadow generators.
    for clause in constraints[table]:
        match=re.match(r'(UNIQUE )?KEY (\w+)\(([^)]+)\)',clause)
        if match:
            unique,index,cols=match.groups()
            mapping.append(' e.HasIndex(x=>new {'+','.join('x.'+pascal(c) for c in cols.split(','))+'})'+('.IsUnique()' if unique else '')+f'.HasDatabaseName("{index}");')
    mapping.append('}')
mapping += ['}', '}']
(BASE/'ElectronicRegisterContext.Formative.cs').write_text('\n'.join(mapping)+'\n',encoding='utf-8',newline='\n')
sql.append('DELIMITER $$')
def trigger(name, action, table, body):
    sql.append(f'CREATE TRIGGER {name} BEFORE {action} ON {table} FOR EACH ROW BEGIN\n {body}\nEND$$')
def deny(condition, message): return f"IF {condition} THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='{message}'; END IF;"
zero="'00000000-0000-0000-0000-000000000000'"
trigger('fp_insert','INSERT','formative_plan',deny(f"NEW.id={zero} OR CHAR_LENGTH(TRIM(NEW.code))=0 OR CHAR_LENGTH(TRIM(NEW.name))=0 OR NEW.revision<>0",'Invalid formative plan'))
trigger('fp_update','UPDATE','formative_plan',deny('NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.study_path_id<>OLD.study_path_id OR BINARY NEW.code<>BINARY OLD.code OR NEW.created_at_utc<>OLD.created_at_utc OR NEW.revision<>OLD.revision+1 OR CHAR_LENGTH(TRIM(NEW.name))=0','Plan identity or revision immutable'))
trigger('fp_delete','DELETE','formative_plan',"SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Formative plan retained';")
trigger('fpv_insert','INSERT','formative_plan_version',deny(f"NEW.id={zero} OR NEW.status<>'Draft' OR NEW.version_number<1 OR NEW.revision<>0 OR NEW.published_at_utc IS NOT NULL OR NEW.theory_planned_minutes<1 OR NEW.theory_planned_minutes>10000000 OR NEW.internship_planned_minutes<0 OR NEW.internship_planned_minutes>10000000 OR CHAR_LENGTH(TRIM(NEW.name))=0",'Invalid draft'))
publish = """IF NEW.status='Published' THEN
 IF NEW.published_at_utc IS NULL OR
 (SELECT COUNT(*) FROM formative_unit WHERE institution_id=OLD.institution_id AND version_id=OLD.id) NOT BETWEEN 1 AND 500 OR
 EXISTS(SELECT 1 FROM formative_unit u WHERE u.institution_id=OLD.institution_id AND u.version_id=OLD.id AND (SELECT COUNT(*) FROM formative_module m WHERE m.institution_id=u.institution_id AND m.unit_id=u.id) NOT BETWEEN 1 AND 500) OR
 (SELECT COALESCE(SUM(planned_minutes),0) FROM formative_module WHERE institution_id=OLD.institution_id AND version_id=OLD.id)<>NEW.theory_planned_minutes OR
 EXISTS(SELECT 1 FROM formative_module m WHERE m.institution_id=OLD.institution_id AND m.version_id=OLD.id AND (m.subject_id IS NULL OR (SELECT COUNT(*) FROM formative_module_competency c WHERE c.institution_id=m.institution_id AND c.module_id=m.id) NOT BETWEEN 1 AND 200 OR (SELECT COUNT(*) FROM formative_module_content c WHERE c.institution_id=m.institution_id AND c.module_id=m.id) NOT BETWEEN 1 AND 200))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Incomplete formative plan publication'; END IF;
 ELSE IF NEW.published_at_utc IS NOT NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Draft publication date invalid'; END IF; END IF;"""
trigger('fpv_update','UPDATE','formative_plan_version',deny("OLD.status<>'Draft' OR NEW.status NOT IN ('Draft','Published') OR NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.study_path_id<>OLD.study_path_id OR NEW.plan_id<>OLD.plan_id OR NEW.version_number<>OLD.version_number OR NEW.created_at_utc<>OLD.created_at_utc OR NEW.revision<>OLD.revision+1 OR NEW.theory_planned_minutes NOT BETWEEN 1 AND 10000000 OR NEW.internship_planned_minutes NOT BETWEEN 0 AND 10000000 OR CHAR_LENGTH(TRIM(NEW.name))=0",'Published snapshot or identity immutable')+'\n '+publish)
trigger('fpv_delete','DELETE','formative_plan_version',"SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Version retained';")
for table in ['formative_unit','formative_module','formative_module_competency','formative_module_content']:
    prefix={'formative_unit':'fu','formative_module':'fm','formative_module_competency':'fmc','formative_module_content':'fmt'}[table]
    parent = 'unit_id' if table=='formative_module' else 'module_id' if table.startswith('formative_module_') else None
    for action in ['INSERT','UPDATE','DELETE']:
        row='OLD' if action=='DELETE' else 'NEW'
        body = f"DECLARE version_state varchar(10); SELECT status INTO version_state FROM formative_plan_version WHERE institution_id={row}.institution_id AND id={row}.version_id FOR UPDATE;\n "+deny("version_state IS NULL OR version_state<>'Draft'",'Published children immutable')
        if action=='UPDATE':
            body+='\n '+deny('NEW.id<>OLD.id OR NEW.institution_id<>OLD.institution_id OR NEW.version_id<>OLD.version_id'+(f' OR NEW.{parent}<>OLD.{parent}' if parent else ''),'Child parent immutable')
        if action!='DELETE':
            invalid=f'NEW.id={zero} OR NEW.display_order<0'
            if table in ['formative_unit','formative_module']: invalid+=' OR CHAR_LENGTH(TRIM(NEW.name))=0 OR (NEW.code IS NOT NULL AND CHAR_LENGTH(TRIM(NEW.code))=0)'
            else: invalid+=' OR CHAR_LENGTH(TRIM(NEW.description))=0'
            if table=='formative_module': invalid+=' OR NEW.planned_minutes NOT BETWEEN 1 AND 10000000'
            body+='\n '+deny(invalid,'Invalid formative element')
        trigger(prefix+'_'+action.lower(),action,table,body)
trigger('cefp_insert','INSERT','course_edition_formative_plan',"DECLARE version_state varchar(10); DECLARE edition_path char(36) CHARACTER SET ascii; SELECT status INTO version_state FROM formative_plan_version WHERE institution_id=NEW.institution_id AND id=NEW.version_id FOR UPDATE; SELECT study_path_id INTO edition_path FROM course_edition WHERE institution_id=NEW.institution_id AND id=NEW.course_edition_id FOR UPDATE; "+deny("version_state IS NULL OR version_state<>'Published' OR edition_path IS NULL OR edition_path<>NEW.study_path_id",'Edition plan binding invalid'))
for table,prefix in [('course_edition_formative_plan','cefp'),('formative_plan_event','fpe')]:
    for action in ['UPDATE','DELETE']: trigger(prefix+'_'+action.lower(),action,table,"SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Formative history immutable';")
trigger('fpe_insert','INSERT','formative_plan_event',deny("NEW.revision_after<0 OR NEW.revision_before<0 OR CHAR_LENGTH(TRIM(NEW.reason))=0 OR NOT(BINARY NEW.request_hash REGEXP '^[0-9A-F]{64}$') OR NOT EXISTS(SELECT 1 FROM institution_membership m JOIN institution_membership_role r ON r.membership_id=m.id JOIN institution i ON i.id=m.institution_id WHERE m.institution_id=NEW.institution_id AND m.id=NEW.actor_membership_id AND m.user_id=NEW.actor_user_id AND m.status='active' AND i.status='active' AND r.role='InstitutionAdmin') OR (NEW.version_id IS NOT NULL AND NOT EXISTS(SELECT 1 FROM formative_plan_version v WHERE v.institution_id=NEW.institution_id AND v.id=NEW.version_id AND v.plan_id=NEW.plan_id))",'Invalid formative audit actor or resource'))
sql.append('DELIMITER ;')
out=ROOT/'migrations/institution-formative-plan'
out.mkdir(exist_ok=True)
(out/'001_expand.sql').write_text('\n'.join(sql)+'\n',encoding='utf-8',newline='\n')

if __name__ == '__main__': print('Generated additive schema and eight Database First entity mappings; no SQL executed.')
