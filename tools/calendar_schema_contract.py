"""Static extension classification for historical validators, never SQL execution."""
import json

TABLES={'institution_calendar_event','institution_calendar_settings','institution_calendar_period','institution_calendar_import','institution_lesson','institution_calendar_occupancy'}

def phase4_contract(root,current):
    if not(TABLES & set(current['tables'])):
        return current
    previous=json.loads((root/'migrations/institution-calendar/predecessor-contract.json').read_text())
    assert previous['inventory']==dict(tables=60,triggers=153,fks=171,indexes=257)
    assert current['inventory']==dict(tables=66,triggers=174,fks=193,indexes=293)
    assert set(current['tables'])-set(previous['tables'])==TABLES
    assert len(current['tables'])==66 and len(current['triggers'])==len(current['trigger_metadata'])==174
    for key in ('tables','triggers','trigger_metadata'):
        assert all(current[key][name]==value for name,value in previous[key].items()),'Calendar changed predecessor '+key
    return previous
