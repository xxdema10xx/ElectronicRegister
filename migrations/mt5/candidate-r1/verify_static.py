"""Local-file-only review checks. Never connects to SQL or starts processes/servers."""
import hashlib
import json
from pathlib import Path
import re

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
FILES = [
    '000_preflight.sql', '001_security_state_expand.sql',
    '002_security_state_backfill.sql', '003_security_state_validate.sql',
    '004_membership_delete_error_contract.sql', '005_delete_contract_validate.sql',
]


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def lexical_statements(text):
    """Check quoting, parentheses and client delimiters, not server SQL grammar."""
    statements, buffer, delimiter = [], [], ';'
    quote, depth, i = None, 0, 0
    while i < len(text):
        if quote:
            char = text[i]
            buffer.append(char)
            i += 1
            if char == '\\' and i < len(text):
                buffer.append(text[i]); i += 1
            elif char == quote:
                if i < len(text) and text[i] == quote:
                    buffer.append(text[i]); i += 1
                else:
                    quote = None
            continue
        if text.startswith('--', i):
            end = text.find('\n', i)
            i = len(text) if end < 0 else end
            continue
        if text.startswith('/*', i):
            end = text.find('*/', i + 2)
            assert end >= 0, 'Unclosed comment'
            i = end + 2
            continue
        if (i == 0 or text[i-1] == '\n') and text.startswith('DELIMITER ', i):
            assert not ''.join(buffer).strip(), 'Pending statement before delimiter'
            end = text.find('\n', i)
            assert end >= 0
            delimiter = text[i+10:end].strip()
            assert delimiter in (';', '$$')
            i = end + 1
            continue
        if text.startswith(delimiter, i):
            assert depth == 0, 'Unbalanced parentheses'
            value = ''.join(buffer).strip()
            assert value
            statements.append(value)
            buffer = []
            i += len(delimiter)
            continue
        char = text[i]
        if char in ("'", '"', '`'):
            quote = char
        elif char == '(':
            depth += 1
        elif char == ')':
            depth -= 1
            assert depth >= 0, 'Unbalanced parentheses'
        buffer.append(char)
        i += 1
    assert not quote and depth == 0 and not ''.join(buffer).strip()
    assert delimiter == ';'
    return statements


def main():
    texts = {name: (HERE/name).read_text(encoding='utf-8') for name in FILES}
    manifest = json.loads((HERE/'manifest-r1.json').read_text())
    assert {x['File'] for x in manifest['Candidates']} == set(FILES)
    for item in manifest['Candidates']:
        assert digest(HERE/item['File']) == item['SHA256'], 'Candidate hash mismatch'
    historical = ROOT/'tests/Integration/TestResults/mt5b-persistence/final-migration-manifest.json'
    assert digest(historical) == manifest['HistoricalManifestSHA256']
    old = {x['File']: x['SHA256'] for x in json.loads(historical.read_text(encoding='utf-8-sig'))}
    assert all(digest(HERE/name) != old[name] for name in FILES)

    statements = {name: lexical_statements(text) for name, text in texts.items()}
    for name in (FILES[0], FILES[3], FILES[5]):
        assert len(statements[name]) == 1 and statements[name][0].startswith('SELECT ')
    for name, count in ((FILES[0], 7), (FILES[3], 6), (FILES[5], 2)):
        assert len(re.findall(r"^SELECT '[^']+'", texts[name], re.M)) == count

    all_sql = '\n'.join('\n'.join(items) for items in statements.values())
    assert not re.search(r'\b(DROP|TRUNCATE|GRANT|REVOKE|ALTER|USE|CREATE\s+DATABASE)\b', all_sql, re.I)
    assert not re.search(r'\b(?:SET\s+(?:@@)?GLOBAL|mysql\.)', all_sql, re.I)
    assert not re.search(r'UPDATE\s+institution_membership\b', all_sql, re.I)
    assert 'INSERT IGNORE' not in all_sql and 'ON DUPLICATE KEY' not in all_sql
    assert all_sql.count('SET security_generation=security_generation+1') == 3
    assert all_sql.count('security_generation<9223372036854775807') == 3
    assert all_sql.count('IF ROW_COUNT()<>1 THEN') == 3
    assert all_sql.count('CREATE TABLE institution_membership_security_state') == 1
    assert 'CREATE TABLE IF NOT EXISTS' not in all_sql

    pattern = r'CREATE TRIGGER (\w+) (BEFORE|AFTER) (INSERT|UPDATE|DELETE) ON (\w+)\s+FOR EACH ROW(?: PRECEDES (\w+))?\s+BEGIN(.*?)END\$\$'
    triggers = re.findall(pattern, texts[FILES[1]]+'\n'+texts[FILES[4]], re.S)
    assert len(triggers) == 11 and len({t[0] for t in triggers}) == 11
    inventory_source = ROOT/'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/Migrations/Mt5bSecurityStateIntegrity.cs'
    known = set(re.findall(r"'(mt5b_\w+)'", inventory_source.read_text(encoding='utf-8-sig')))
    assert {t[0] for t in triggers} == known
    historical_shapes = json.loads((ROOT/'tests/Integration/TestResults/mt5b-persistence/final-new-triggers.json').read_text(encoding='utf-8-sig'))
    expected = {t['Name']: (t['Timing'], t['Event'], t['Table']) for t in historical_shapes}
    assert all(tuple(t[1:4]) == expected[t[0]] for t in triggers)
    assert next(t[4] for t in triggers if t[0]=='mt5b_membership_delete_contract') == 'mt5b_membership_security_delete'

    canonical = {}
    for directory in ('mt1', 'mt3', 'mt4'):
        for path in sorted((ROOT/'migrations'/directory).glob('*.sql')):
            canonical[path.relative_to(ROOT).as_posix()] = digest(path)
    assert canonical == manifest['CanonicalPredecessorSQL'], 'Predecessor SQL changed'
    retained = set()
    for rel in canonical:
        retained.update(re.findall(r'CREATE TRIGGER\s+(\w+)', (ROOT/rel).read_text(encoding='utf-8-sig'), re.I))
    assert len(retained) == 53
    assert not retained.intersection(known)
    predecessor_targets = []
    for rel in canonical:
        predecessor_targets += re.findall(r'CREATE TRIGGER\s+\w+\s+(?:BEFORE|AFTER)\s+\w+\s+ON\s+`?(\w+)', (ROOT/rel).read_text(encoding='utf-8-sig'), re.I)
    assert not set(predecessor_targets).intersection({'institution_membership', 'institution_membership_role', 'institution_membership_security_state'})

    # Predicates must stay identical in security DELETE and both membership DELETE guards.
    predicates = re.findall(r'IF NOT\((COALESCE\(.*?\),0\))\) THEN', texts[FILES[1]]+'\n'+texts[FILES[4]], re.S)
    assert len(predicates) == 3
    assert len({re.sub(r'\s+', '', p) for p in predicates}) == 1
    for name in (FILES[1], FILES[2], FILES[4]):
        assert 'PREPARE mt5b_r1_gate' in texts[name]
        assert "@@port=13306" in texts[name]
        assert "--no-defaults" not in texts[name]  # SQL, not shell commands.

    result = {
        'Status': 'STATIC_REVIEW_CHECKS_PASSED',
        'SQLExecuted': False, 'MySQLGrammarParsed': False, 'DotNetSuiteExecuted': False,
        'CandidateFiles': 6, 'TriggerNamesAndShapesMatched': 11,
        'RetainedCanonicalTriggerNames': len(retained), 'TriggerNameCollisions': 0,
        'ValidatorRuleCounts': [7, 6, 2],
        'StatementCounts': {name: len(items) for name, items in statements.items()},
        'FixturePredicatesIdentical': True,
        'CandidateHashesVerified': True, 'HistoricalManifestUnchanged': True,
        'PredecessorSQLUnchanged': True,
        'Limits': ['Lexical/dependency review only; not MySQL syntax certification',
                   'Requires approved isolated execution for errors, ordering, 1442, rollback, concurrency and teardown']
    }
    (HERE/'static-review.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
