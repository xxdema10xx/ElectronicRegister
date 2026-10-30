const fs=require('fs'),{execute,root,run}=require('./sql.cjs');
fs.writeFileSync(run+'/005-started','apply-once',{flag:'wx'});
execute(fs.readFileSync(root+'/migrations/identity-account/005_release_gate.sql','utf8'));
console.log('New additive gate applied once on the identity-attested disposable database; ready='+execute('CALL er_schema_ready();'));
