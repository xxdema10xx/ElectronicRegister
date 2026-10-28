"""Generate scalar Database First sidecars from an attested SHOW CREATE contract."""
import argparse,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
TABLES=['institution_calendar_event','institution_calendar_settings','institution_calendar_period','institution_calendar_import','institution_lesson','institution_calendar_occupancy']
def pascal(s):return ''.join(x.title() for x in s.split('_'))
def main():
    parser=argparse.ArgumentParser();parser.add_argument('contract');args=parser.parse_args()
    contract=json.loads(Path(args.contract).read_text())
    base=ROOT/'Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence'
    mapping=['// Database First scalar sidecar. SQL owns composite foreign keys; legacy EF owners are unchanged.','using Microsoft.EntityFrameworkCore;','using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;','namespace ElectronicRegisterAPI.Infrastructure.Persistence;','public partial class ElectronicRegisterContext {','private static void ConfigureInstitutionCalendar(ModelBuilder b) {']
    for table in TABLES:
        ddl=contract['tables'][table];name=pascal(table)+'Record';entity=['#nullable enable','namespace ElectronicRegisterAPI.Infrastructure.Persistence.Entities;',f'public sealed class {name} {{']
        mapping.append(f'{{ var e=b.Entity<{name}>();e.ToTable("{table}");')
        for column,definition in re.findall(r'^  `([^`]+)` (.+?)(?:,)?$',ddl,re.M):
            sqltype=re.match(r'(?:enum\([^)]*\)|[a-z]+(?:\(\d+\))?)',definition).group(0)
            nullable='NOT NULL' not in definition
            if sqltype.startswith('char(36)'):typ='Guid'
            elif sqltype.startswith(('varchar','char','json','text')) or sqltype.startswith('enum'):typ='string'
            elif sqltype.startswith('bigint'):typ='long'
            elif sqltype.startswith('int'):typ='int'
            elif sqltype.startswith('datetime'):typ='DateTime'
            elif sqltype=='date':typ='DateOnly'
            elif sqltype=='time':typ='TimeOnly'
            else:raise ValueError(sqltype)
            prop=pascal(column);suffix='?' if nullable else '';init=' = null!;' if typ=='string' and not nullable else ''
            entity.append(f' public {typ}{suffix} {prop} {{get;set;}}{init}')
            line=f' e.Property(x=>x.{prop}).HasColumnName("{column}").HasColumnType("{sqltype}")'
            if sqltype.startswith('char(36)'):line+='.HasCharSet("ascii").UseCollation("ascii_general_ci")'
            if not nullable:line+='.IsRequired()'
            mapping.append(line+';')
        pk=re.search(r'PRIMARY KEY \(([^)]+)\)',ddl).group(1);props=[pascal(x) for x in re.findall(r'`([^`]+)`',pk)]
        mapping.append(' e.HasKey(x=>new {'+','.join('x.'+p for p in props)+'});')
        for p in props:mapping.append(f' e.Property(x=>x.{p}).ValueGeneratedNever();')
        for unique,key,cols in re.findall(r'\n  (UNIQUE )?KEY `([^`]+)` \(([^)]+)\)',ddl):
            properties=[pascal(x) for x in re.findall(r'`([^`]+)`',cols)]
            mapping.append(' e.HasIndex(x=>new {'+','.join('x.'+p for p in properties)+'})'+('.IsUnique()' if unique else '')+f'.HasDatabaseName("{key}");')
        mapping.append('}')
        entity.append('}')
        (base/'Entities'/f'{name}.cs').write_text('\n'.join(entity)+'\n')
    mapping+=['}','}']
    (base/'ElectronicRegisterContext.Calendar.cs').write_text('\n'.join(mapping)+'\n')
if __name__=='__main__':main()
