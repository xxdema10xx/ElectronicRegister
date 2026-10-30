// One-shot scoped edit for F06; retained as review evidence, not a build step.
const fs=require('fs');
const base='Backend/ElectronicRegisterAPI/src/';
function edit(p,f){const s=fs.readFileSync(base+p,'utf8');fs.writeFileSync(base+p,f(s));}
edit('Domain/ElectronicRegisterAPI.Domain/Interfaces/Managers/IInstitutionExamManager.cs',s=>s.replace(/(\w+\()([^()]*)(\);)/g,(m,a,b,c)=>a+b.trimEnd()+(b.trim()?', ':'')+'CancellationToken ct = default'+c));
edit('Application/ElectronicRegisterAPI.Application/Managers/InstitutionExamManager.cs',s=>{
 s=s.replace('TenantContext actor) : ITransactionManager','TenantContext actor, CancellationToken requestCt) : ITransactionManager');
 s=s.replace('await authorization.LockExamAsync(actor, ct);','await authorization.LockExamAsync(actor, requestCt);').replace('}, ct);','}, requestCt);');
 s=s.replace('Open(Operation operation)','Open(Operation operation, CancellationToken ct)').replace('access.RequireAsync(CancellationToken.None)','access.RequireAsync(ct)').replace('profiles.RequireStudentAsync(t)','profiles.RequireStudentAsync(t, ct)').replace('profiles.RequireTeacherAsync(t)','profiles.RequireTeacherAsync(t, ct)').replace('factory.Create(actor)','factory.Create(actor, ct)').replace('writeAuthorization, t)','writeAuthorization, t, ct)');
 s=s.replace('Func<Work, Task<T>> action) => await action(await Open(operation))','Func<Work, Task<T>> action, CancellationToken ct) => await action(await Open(operation, ct))').replace('Func<Work, Task> action) => await action(await Open(operation))','Func<Work, Task> action, CancellationToken ct) => await action(await Open(operation, ct))');
 s=s.replace('Guid? id = null) => Run','Guid? id = null, CancellationToken ct = default) => Run');
 // Each public method is bounded by the next declaration, so lambda bodies stay intact.
 s=s.replace(/(    public (?:async )?Task[^\n]*?\w+\()([^)]*)(\))([\s\S]*?)(?=\n    (?:public |private )|\n}\s*$)/g,(m,a,params,c,body)=>{
  params=params.trimEnd()+(params.trim()?', ':'')+'CancellationToken ct = default';
  body=body.replace(/Open\(Operation\.(Read|Student)\)/g,'Open(Operation.$1, ct)');
  if(body.includes('=> Run('))body=body.replace(/\);(\s*)$/,', ct);$1');
  if(body.includes('=> GradeWrite('))body=body.replace(/\);(\s*)$/,', ct: ct);$1');
  return a+params+c+body;
 });
 // Multiline GradePage signature and GradeWrite tail are private boundaries.
 s=s.replace(/(private Task<T> GradeWrite[\s\S]*?)\n    }\);/, '$1\n    }, ct);');
 return s;
});
// Transport forwards RequestAborted without changing any HTTP payload or route.
for(const controller of ['InstitutionExamController','InstitutionGradeController','InstitutionSubmissionController'])
 edit(`Api/ElectronicRegisterAPI/Controllers/${controller}.cs`,s=>s.replace(/manager\.(\w+)\(([^()]*)\)/g,(m,n,a)=>`manager.${n}(${a}${a.trim()?', ':''}HttpContext.RequestAborted)`));
for(const name of ['ExamRepository','ExamSubmissionRepository','GradeRepository','ClassSubjectRepository','StudentRepository','SubjectRepository','AuditRepository'])
 edit(`Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/${name}.cs`,s=>{
  s=s.replace('private readonly InstitutionExamDataScope? _scope;','private readonly InstitutionExamDataScope? _scope;\n    private CancellationToken Ct => _scope?.CancellationToken ?? CancellationToken.None;');
  // Only EF async invocations; no domain or transaction callback signatures.
  const re=/\.(ToListAsync|ToDictionaryAsync|FirstOrDefaultAsync|SingleOrDefaultAsync|SingleAsync|AnyAsync|CountAsync|AverageAsync|SaveChangesAsync|BeginTransactionAsync|ExecuteSqlInterpolatedAsync)\(/g;
  const edits=[];let m;
  while((m=re.exec(s))){let depth=1,i=re.lastIndex,quote=false;const start=i;
   for(;i<s.length&&depth;i++){const c=s[i];if(c==='"'&&s[i-1]!=='\\')quote=!quote;if(!quote){if(c==='(')depth++;if(c===')')depth--;}}
   const args=s.slice(start,i-1);if(!/\b(ct|Ct|cancellationToken)\b/.test(args))edits.push([i-1,(args.trim()?', ':'')+'Ct']);re.lastIndex=i;
  }
  for(const [i,t]of edits.reverse())s=s.slice(0,i)+t+s.slice(i);
  return s;
 });
