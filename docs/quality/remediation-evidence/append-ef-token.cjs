const fs=require('fs');
const directory='Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Repositories/';
for(const [file,token]of [['InstitutionExamDataScope.cs','ct'],['InstitutionExamRepositoryFactory.cs','_scope.CancellationToken']]){
 let s=fs.readFileSync(directory+file,'utf8'),m;
 const re=/\.(ToListAsync|ToDictionaryAsync|FirstOrDefaultAsync|SingleOrDefaultAsync|SingleAsync|AnyAsync|CountAsync|ExecuteSqlInterpolatedAsync)\(/g,edits=[];
 while((m=re.exec(s))){let depth=1,i=re.lastIndex,quote=false,start=i;
  for(;i<s.length&&depth;i++){const c=s[i];if(c==='"'&&s[i-1]!=='\\')quote=!quote;if(!quote){if(c==='(')depth++;if(c===')')depth--;}}
  const args=s.slice(start,i-1);if(!/\b(ct|Ct|CancellationToken)\b/.test(args))edits.push([i-1,(args.trim()?', ':'')+token]);re.lastIndex=i;
 }
 for(const [i,t]of edits.reverse())s=s.slice(0,i)+t+s.slice(i);
 fs.writeFileSync(directory+file,s);
}
