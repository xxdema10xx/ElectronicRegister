const fs=require('fs');
const names=['InstitutionBulkImportAtomicityTests.cs','InstitutionCalendarConcurrencyTests.cs','InstitutionEmailAtomicityTests.cs','InstitutionFormativePlanLifecycleTests.cs','InstitutionStaffAtomicityTests.cs','QualityRevocationTests.cs'];
for(const name of names){const f='tests/Integration/'+name;let s=fs.readFileSync(f,'utf8');
 s=s.replace(/var (write|revoke|rotation|mutation)\s*=\s*((?:Request|Execute|ConfirmImport|Send|Sql)\([\s\S]*?);/g,(_,v,call)=>`var ${v} = SqlLockEvidence.Track(() => ${call});`);
 s=s.replace(/SqlLockEvidence\.WaitAsync\(fixture\.ConnectionString,\s*(revoke|rotation),\s*(?:Id\([\s\S]*?\)|_a|_teacherUser|people\["(?:Staff|InstitutionAdmin)"\]\.Member)\)/g,(_,v)=>`SqlLockEvidence.WaitAsync(fixture.ConnectionString, ${v}, ${name==='InstitutionFormativePlanLifecycleTests.cs'?'mutation':'write'})`);
 fs.writeFileSync(f,s);
}
const f='tests/Integration/Phase3_TestSupport.cs';let s=fs.readFileSync(f,'utf8').replace('var firstTask = first(firstScope.ServiceProvider);','var firstTask = SqlLockEvidence.Track(() => first(firstScope.ServiceProvider));').replace('secondTask = second(secondScope.ServiceProvider);','secondTask = SqlLockEvidence.Track(() => second(secondScope.ServiceProvider));').replace('secondTask, blocker.ServerThread','secondTask, firstTask');fs.writeFileSync(f,s);
const native='tests/Integration/Mt5bSecurityStateTests.cs';s=fs.readFileSync(native,'utf8').replace('var contender = Contender();','var contender = SqlLockEvidence.Track(second, Contender);');fs.writeFileSync(native,s);
const fixture='tests/Integration/InstitutionClassCrudFixture.cs';s=fs.readFileSync(fixture,'utf8').replace('await using var cmd=new MySqlCommand(sql,c);','SqlLockEvidence.Capture(c);await using var cmd=new MySqlCommand(sql,c);');fs.writeFileSync(fixture,s);
