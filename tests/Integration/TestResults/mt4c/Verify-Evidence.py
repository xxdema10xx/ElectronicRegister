import hashlib,json,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parent
ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
def run(folder):
    files=list(folder.glob('*.trx'));assert len(files)==2,(folder,files)
    identities=set();projects=[]
    for file in files:
        tree=ET.parse(file)
        counts=tree.find('.//t:Counters',ns).attrib
        assert counts['failed']=='0' and counts['notExecuted']=='0', (file,counts)
        assert counts['total']==counts['passed']==counts['executed'],(file,counts)
        methods={u.attrib['id']:u.find('t:TestMethod',ns).attrib['className'] for u in tree.findall('.//t:UnitTest',ns)}
        results=tree.findall('.//t:UnitTestResult',ns)
        assert len(results)==int(counts['total'])
        names={(methods[x.attrib['testId']],x.attrib['testName']) for x in results}
        assert len(names)==len(results)
        assert all(x.attrib['outcome']=='Passed' for x in results)
        assert not identities.intersection(names)
        identities.update(names)
        projects.append({'file':str(file.relative_to(root.parent)),'passed':len(names),'sha256':hashlib.sha256(file.read_bytes()).hexdigest()})
    return identities,projects
old,_=run(root.parent/'mt4b'/'final-2');assert len(old)==644
baseline,b=run(root/'baseline');assert baseline==old
one,a=run(root/'final-1');two,c=run(root/'final-2')
assert old<=one and old<=two and one==two
assert len(one-old)==31,(len(one),len(one-old))
result={'previousTests':len(old),'previousIdentitiesPreserved':True,'totalPassedEachFinalRun':len(one),'newTests':len(one-old),'failed':0,'skipped':0,'identicalFinalTestSets':True,'runs':{'baseline':b,'final-1':a,'final-2':c}}
(root/'verification.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in result.items() if k!='runs'},indent=2))
