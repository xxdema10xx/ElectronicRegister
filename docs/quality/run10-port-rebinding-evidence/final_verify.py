import hashlib,json,shutil,subprocess,sys
from pathlib import Path

base=Path('.diagnostics/run10-local')
out=Path('docs/quality/run10-port-rebinding-evidence')
out.mkdir(exist_ok=True)
def read(path):
    data=Path(path).read_bytes()
    return json.loads(data.decode('utf-16' if data.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig'))
def run(*args): return subprocess.check_output(args,text=True)
phases=read(base/'recovery-verified/runtime-phases.json')['problems']
assert len(phases)==4 and all(p['status']=='PASS' for p in phases), phases
subprocess.run([sys.executable,str(base/'final_identity.py')],check=True)
before=read(base/'protected-before.json')
after=json.loads(run('docker','inspect',before['Id']))[0]
fields=lambda o:dict(Id=o['Id'],Name=o['Name'],Image=o['Image'],ConfigImage=o['Config']['Image'],
    StartedAt=o['State']['StartedAt'],FinishedAt=o['State']['FinishedAt'],Running=o['State']['Running'],
    Status=o['State']['Status'],RestartCount=o['RestartCount'],Mounts=o['Mounts'],
    Ports=o['NetworkSettings']['Ports'],PortBindings=o['HostConfig']['PortBindings'])
proof=dict(status='PASS' if fields(before)==fields(after) else 'FAIL',before=fields(before),after=fields(after))
(base/'protected-metadata-check.json').write_text(json.dumps(proof,indent=2))
assert proof['status']=='PASS',proof
manifest_path=Path('migrations/mysql84-remediation/manifest.json')
manifest=read(manifest_path)
sources=['tools/mysql84_recovery.py','tools/mysql84_local.py','tools/mysql84_release.py',
         'tools/mysql84_runtime.py','tools/test_mysql84_restart.py','tools/test_mysql84_recovery.py']
coverage={name:hashlib.sha256(Path(name).read_text(encoding='utf-8').replace('\r\n','\n').encode()).hexdigest().upper() for name in sources}
assert all(manifest['files'][name]==digest for name,digest in coverage.items())
historical=run('git','show','HEAD:migrations/release/manifest.json')
assert historical.replace('\r\n','\n')==Path('migrations/release/manifest.json').read_text().replace('\r\n','\n')
(base/'candidate-source-coverage.json').write_text(json.dumps(dict(status='PASS',
    closureFiles=len(manifest['files']),manifestSha256=hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
    sources=coverage,historicalManifestUnchanged=True),indent=2))
target='3ccbb050937e59810333fdd89fa946d1573d1b7d0b384efd0411cdde4661d4f2'
observed=json.loads(run('docker','inspect',target))[0]
assert observed['Id']==target and observed['Name']=='/er84-run10-20261103'
assert observed['Mounts'][0]['Name']=='er84-run10-20261103-data'
stop=run('docker','stop',target).strip()
(base/'fixture-final-state.json').write_text(json.dumps(dict(container=target,stopped=stop==target,volumeRetained=True),indent=2))
files={
 'restart-regression.json':base/'restart-regression.json',
 'targeted-restart-result.json':base/'targeted-restart-result.json',
 'refused-tcp-restart-result.json':base/'refused-tcp-restart-result.json',
 'rejected-uuid-result.json':base/'rejected-uuid-result.json',
 'runtime-monolithic-phases.json':base/'upgrade/runtime-phases.json',
 'runtime-monolithic.log':base/'upgrade.log',
 'upgrade-journal.json':base/'upgrade/upgrade-journal.json',
 'recovery-phases.json':base/'recovery-verified/runtime-phases.json',
 'upgrade-result.json':base/'recovery-verified/runtime-result.json',
 'fresh-result.json':base/'recovery-verified/fresh-result.json',
 'fresh-journal.json':base/'recovery-verified/fresh-journal.json',
 'api-barrier-result.json':base/'recovery-verified/api-barrier-result.json',
 'api-restored-result.json':base/'recovery-verified/api-restored-result.json',
 'upgrade-restart-result.json':base/'recovery-verified/restart-result.json',
 'final-identity.json':base/'final-identity.json',
 'protected-metadata-check.json':base/'protected-metadata-check.json',
 'fixture-final-state.json':base/'fixture-final-state.json',
 'candidate-source-coverage.json':base/'candidate-source-coverage.json',
 'python-all.log':base/'python-all.log',
 'manifest.log':base/'manifest.log',
 'node-release.log':base/'node-release.log',
 'resume_verified.py':base/'resume_verified.py',
 'final_identity.py':base/'final_identity.py',
 'final_verify.py':base/'final_verify.py',
 'source-run10-restart.json':Path('.diagnostics/run10-source/mysql84-corrective-release-evidence-upgrade/_temp/restart-regression.json'),
 'source-run10-targeted.json':Path('.diagnostics/run10-source/mysql84-corrective-release-evidence-upgrade/_temp/targeted-restart-result.json'),
}
for name,path in files.items():shutil.copyfile(path,out/name)
index={name:dict(bytes=(out/name).stat().st_size,sha256=hashlib.sha256((out/name).read_bytes()).hexdigest()) for name in files}
(out/'index.json').write_text(json.dumps(index,indent=2))
print('PASS: final SQL/API identities, protected metadata, stopped dedicated fixture, evidence indexed')
