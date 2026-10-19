# Verify counters, individual outcomes and exact identities; never infer success from exit code.
import hashlib,json,sys,xml.etree.ElementTree as ET
from pathlib import Path
root=Path('tests/Integration/TestResults/mt4a')
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def read(folder):
    result={}
    for p in Path(folder).glob('*.trx'):
        doc=ET.parse(p);c=doc.find('t:ResultSummary/t:Counters',ns).attrib
        rows=doc.findall('t:Results/t:UnitTestResult',ns)
        assert int(c['failed'])==0 and int(c['notExecuted'])==0 and int(c['total'])==int(c['passed'])==int(c['executed'])==len(rows),(str(p),c)
        assert all(r.attrib['outcome']=='Passed' for r in rows),str(p)
        names={r.attrib['testName'] for r in rows};assert len(names)==len(rows)
        key='unit' if len(rows)==51 else 'integration'
        assert key not in result,'Duplicate project TRX'
        result[key]={'names':names,'file':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'passed':len(rows)}
    assert set(result)=={'unit','integration'},folder
    return result
old=read('tests/Integration/TestResults/mt3e/final-2')
baseline=read(root/'baseline-corrected');one=read(root/'final-1');two=read(root/'final-2')
for project in old:
    assert old[project]['names']==baseline[project]['names'],'Baseline identities differ'
    assert old[project]['names']<=one[project]['names'],'Previous test removed'
    assert one[project]['names']==two[project]['names'],'Final identities differ'
summary={'previousTests':577,'previousIdentitiesPreserved':True,'identicalFinalSets':True,'failed':0,'skipped':0,'runs':{}}
for label,run in [('baseline',baseline),('final-1',one),('final-2',two)]:
    summary['runs'][label]={k:{n:v for n,v in r.items() if n!='names'} for k,r in run.items()}
(root/'verification.json').write_text(json.dumps(summary,indent=2))
print(json.dumps(summary,indent=2))

