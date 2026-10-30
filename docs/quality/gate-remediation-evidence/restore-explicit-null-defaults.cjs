// Preserve exact SHOW CREATE representation after reversible default probes.
const {execute,root}=require('./sql.cjs'),{contract}=require(root+'/tools/release-additive-contract.cjs');
for(const c of contract.columns.filter(c=>c.IS_NULLABLE==='YES'&&c.COLUMN_DEFAULT===null))execute(`ALTER TABLE \`${c.TABLE_NAME}\` ALTER COLUMN \`${c.COLUMN_NAME}\` SET DEFAULT NULL`);
console.log('Explicit NULL defaults restored to the reviewed table definitions.');
