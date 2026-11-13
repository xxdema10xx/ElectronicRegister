// Reviewable addition of staging operational artifacts; historical manifests untouched.
const fs=require('fs'),file='tools/release-artifacts.cjs';let s=fs.readFileSync(file,'utf8');
s=s.replace("const explicit=[","const explicit=['tools/StagingSupport/StagingSupport.csproj','tools/StagingSupport/Program.cs','tools/StagingSupport/packages.lock.json','tools/test_staging.py','.github/workflows/staging-package.yml',");
s=s.replace('/\\.(cs|csproj|sql|json|config)$/','/\\.(cs|csproj|sql|json|config|yaml|yml|py|sh|conf)$/');
// Dockerfiles have no extension and must be covered too.
s=s.replace("'tools/StagingSupport/StagingSupport.csproj'","'infra/staging/Dockerfile.ops','tools/StagingSupport/StagingSupport.csproj'");
fs.writeFileSync(file,s);
