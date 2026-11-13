"""Append reviewed calendar SQL and promote an attested scalar schema contract; no SQL execution."""
import argparse,json
from pathlib import Path
from calendar_schema_contract import phase4_contract
ROOT=Path(__file__).resolve().parents[1]
def main():
    p=argparse.ArgumentParser();p.add_argument('contract');a=p.parse_args()
    before=json.loads((ROOT/'migrations/institution-calendar/predecessor-contract.json').read_text())
    current=json.loads(Path(a.contract).read_text())
    for key in ('tables','triggers','trigger_metadata'):
        for name,value in before[key].items():
            # Only disposable fixture port/datadir guards differ in SHOW CREATE.
            if key=='tables':assert current[key][name]==value
            current[key][name]=value
    phase4_contract(ROOT,current)
    marker='-- BEGIN INSTITUTION CALENDAR PHASE5\n'
    bootstrap=ROOT/'migrations/bootstrap/001_schema.sql'
    prefix=bootstrap.read_text(encoding='utf-8-sig').split(marker)[0]
    assert 'CREATE TABLE institution_lesson (' not in prefix
    bootstrap.write_text(prefix+marker+(ROOT/'migrations/institution-calendar/001_expand.sql').read_text(),encoding='utf-8')
    (ROOT/'migrations/bootstrap/schema-contract.json').write_text(json.dumps(current,indent=2)+'\n')
    print('Calendar bootstrap and contract promoted:',current['inventory'])
if __name__=='__main__':main()
