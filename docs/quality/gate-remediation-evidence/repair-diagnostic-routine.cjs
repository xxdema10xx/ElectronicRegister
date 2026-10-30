// Diagnostic instance only: repair the procedure after the recorded generator
// error (empty SQL hex literal). Never replay marker or earlier migrations.
const {execute,root,run}=require('./sql.cjs'),fs=require('fs');
fs.writeFileSync(run+'/diagnostic-routine-repair','empty-literal correction',{flag:'wx'});
const {sql}=require(root+'/tools/release-additive-contract.cjs');
execute(sql.slice(0,sql.lastIndexOf('INSERT INTO er_schema_release')));
console.log('Corrected diagnostic routine only; ready='+execute('CALL er_schema_ready();'));
