// Generate the additive readiness routine from the reviewed bootstrap closure.
// The historical bootstrap and its raw-byte manifest are never rewritten.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..');
const contract=JSON.parse(fs.readFileSync(path.join(root,'migrations/bootstrap/schema-contract.json'),'utf8').replace(/^\uFEFF/,''));
const tables=[...Object.keys(contract.tables),'identity_account_state','identity_account_event','institution_profile_acquisition','identity_password_attempt','er_schema_release'].sort();
const triggers=[...Object.keys(contract.triggers),'identity_account_event_no_update','identity_account_event_no_delete','identity_account_state_guard','profile_acquisition_insert','profile_acquisition_no_update','profile_acquisition_no_delete'].sort();
const quoted=a=>a.map(x=>"'"+x+"'").join(',');
const sql=`-- Release er-quality-1. Apply once AFTER bootstrap and identity-account 001..003.
-- Read-only probe executes with the migration owner's metadata visibility.
-- Grant API users EXECUTE on er_schema_ready, not CREATE/DROP/TRIGGER privileges.
CREATE TABLE er_schema_release (
 version varchar(64) CHARACTER SET ascii NOT NULL PRIMARY KEY,
 applied_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)
) ENGINE=InnoDB;
DELIMITER $$
CREATE PROCEDURE er_schema_ready()
SQL SECURITY DEFINER
READS SQL DATA
BEGIN
 SELECT (
 EXISTS(SELECT 1 FROM er_schema_release WHERE version='er-quality-1')
 AND (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME IN (${quoted(tables)}))=${tables.length}
 AND (SELECT COUNT(*) FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA=DATABASE() AND TRIGGER_NAME IN (${quoted(triggers)}))=${triggers.length}
 AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='identity_account_state' AND COLUMN_NAME IN ('user_id','generation','display_name'))=3
 AND (SELECT COUNT(*) FROM information_schema.REFERENTIAL_CONSTRAINTS WHERE CONSTRAINT_SCHEMA=DATABASE() AND TABLE_NAME IN ('identity_account_state','identity_account_event','institution_profile_acquisition','identity_password_attempt') AND DELETE_RULE='RESTRICT')=7
 ) AS ready;
END$$
DELIMITER ;
INSERT INTO er_schema_release(version) VALUES('er-quality-1');
`;
const destination=path.join(root,'migrations/identity-account/004_release_schema.sql');
if(process.argv.includes('--write'))fs.writeFileSync(destination,sql);
else if(fs.readFileSync(destination,'utf8').replaceAll('\r\n','\n')!==sql)throw Error('Readiness contract drift');
console.log(`Readiness schema closure: ${tables.length} tables, ${triggers.length} trigger prerequisites`);
require('child_process').execFileSync(process.execPath,[path.join(root,'tools/release-additive-contract.cjs')],{stdio:'inherit'});
