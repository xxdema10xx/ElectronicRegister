// MySQL 5.7 rejects ALTER COLUMN SET DEFAULT CURRENT_TIMESTAMP(6).
// Restore the one known interrupted negative control with full MODIFY syntax.
const {execute}=require('./sql.cjs');
execute('ALTER TABLE er_schema_release MODIFY applied_at datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6)');
console.log('Interrupted diagnostic default restored; ready='+execute('CALL er_schema_ready();'));
