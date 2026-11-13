const assert=require('assert'),fs=require('fs');
const release=require('./mysql84-release.cjs'),h=require('./mysql84-hashes.cjs'),fixture=require('./mysql84-fixture.cjs');
const old=release.contract(false),current=release.contract();
assert.equal(Object.keys(current.hashColumns).length,13);
assert.equal(Object.values(current.hashColumns).flat().length,16);
for(const [table,fields]of Object.entries(current.hashColumns))for(const field of fields){
 assert(old.tables[table].includes('`'+field.name+'` char(64)'));
 assert(current.tables[table].includes('`'+field.name+'` varchar(65)'));
 assert(current.tables[table].includes(h.checkName(table,field.name)));
 assert(!current.tableChanges[table].includes(',,'));
}
const sql=fs.readFileSync('migrations/bootstrap/001_schema.sql','utf8');
assert.equal(fixture.bind(sql,{}),sql);
const c={database:'electronic_register_test',host:'127.0.0.1',port:55555,ownedFixture:{uuid:'12345678-1234-1234-1234-123456789abc',host:'123456789abc',capability:'A'.repeat(64)}};
const bound=fixture.bind(sql,c);
assert(bound.includes('BINARY @@server_uuid='));assert(bound.includes('BINARY @er_mt5b_fixture_capability='));
assert(bound.includes("SUBSTRING_INDEX(USER(),'@',1)='root'"));assert(bound.includes('FIND_IN_SET(OLD.institution_id'));
for(const bad of [{...c,database:'electronic_register'},{...c,host:'ec2.invalid'},{...c,port:3307}])assert.throws(()=>fixture.bind(sql,bad));
console.log('PASS: 16 widened hash fields/13 tables, exact CHECKs, immutable predecessor and bounded fixture capability');
