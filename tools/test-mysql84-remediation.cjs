'use strict';
const assert=require('assert'),fs=require('fs'),path=require('path');
const {derive,candidate,forbidden}=require('./mysql84-remediation.cjs');
const result=candidate();
assert.equal(result.triggers.length,19);
assert.equal(result.status,'IMPLEMENTED_PENDING_RUNTIME');
assert.equal(derive('BINARY NEW.status = OLD.status'),'BINARY NEW.status = OLD.status');
for(const sql of ["CAST(NEW.hash AS BINARY) REGEXP 'x'", "CONVERT(NEW.hash USING binary) RLIKE 'x'", "REGEXP_LIKE(BINARY NEW.hash, 'x')", "NEW.hash REGEXP _binary'x'"])
 assert(forbidden.test(sql),sql);
assert(!forbidden.test("BINARY NEW.status=OLD.status OR NEW.reason REGEXP '[[:cntrl:]]'"));
assert.equal(derive("NEW.nonce NOT REGEXP BINARY '^[A-F0-9]{64}$'"),"NOT REGEXP_LIKE(CONVERT(NEW.nonce USING utf8mb4) COLLATE utf8mb4_bin, '^[A-F0-9]{64}\\\\z', 'c')");
const base=require('../migrations/bootstrap/schema-contract.json');
for(const t of result.triggers){
 assert(!forbidden.test(t.statement));
 assert.equal(derive(t.statement),t.statement);
 assert.equal(t.mode,base.triggers[t.name].mode);
 // Every byte outside the two explicitly supported expression shapes is retained.
 const erase=s=>s.replaceAll('\\\\z', '$').replace(/REGEXP_LIKE\(CONVERT\(([\w.]+) USING utf8mb4\) COLLATE utf8mb4_bin, ('[^']*'), 'c'\)/g,'PREDICATE($1,$2)')
  .replace(/BINARY\s+([\w.]+)\s+REGEXP\s+('[^']*')/g,'PREDICATE($1,$2)')
  .replace(/([\w.]+)\s+NOT REGEXP BINARY\s+('[^']*')/g,'NOT PREDICATE($1,$2)');
 assert.equal(erase(t.statement),erase(base.triggers[t.name].statement));
}
// Optional stdout SQL for the isolated CI server. These are predicate probes,
// deliberately not certification of any conditional trigger execution path.
if(process.argv.includes('--sql')){
 const patterns=[...new Set(result.triggers.flatMap(t=>[...t.statement.matchAll(/, ('[^']*'), 'c'\)/g)].map(m=>m[1])))];
 let queries=['SELECT VERSION();'];
 for(const pattern of patterns){
  const code=pattern.includes('MT'),valid=code?'MT5BP_'+'A'.repeat(32):'A'.repeat(64);
  const values=[valid,valid.slice(1),valid+'A',valid.replace('A','G'),valid.toLowerCase(),'é'+valid.slice(1),valid+'\n',null];
  for(const v of values){
   const value=v===null?'CONVERT(NULL USING utf8mb4)':`CONVERT(0x${Buffer.from(v).toString('hex')} USING utf8mb4)`;
   const expected=v===null?'NULL':v===valid?'1':'0';
   queries.push(`SELECT REGEXP_LIKE(${value} COLLATE utf8mb4_bin, ${pattern}, 'c') <=> ${expected};`);
  }
 }
 process.stdout.write(queries.join('\n')+'\n');
}else console.log('19 candidate bodies verified statically; runtime NOT tested');
