import re,sys
from pathlib import Path
candidate=Path(sys.argv[1]);target=Path('Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence')
context=(candidate/'Mt4CandidateContext.cs').read_text(encoding='utf-8-sig')
source=candidate/'Entities/CourseAdministrationEvent.cs';dest=target/'Entities/CourseAdministrationEvent.cs'
if dest.exists() and dest.read_bytes()!=source.read_bytes():raise RuntimeError('Existing entity differs; review instead of overwriting')
dest.write_bytes(source.read_bytes())
a=context.index('        modelBuilder.Entity<CourseAdministrationEvent>(entity =>');b=context.index('\n        });',a)+len('\n        });');block=context[a:b]
block=re.sub(r'(entity.HasOne\(d => d\.[A-Za-z]+\)\.)WithMany\([^\n]*?\)',r'\1WithMany()',block)
# Preserve legacy Class model without adding a new alternate-key value generator path. SQL still enforces this composite FK.
block=re.sub(r'            entity.HasOne\(d => d.Class\).*?HasConstraintName\("fk_mt4c_class"\);','            entity.Ignore(e => e.Class);',block,flags=re.S)
assert 'entity.Ignore(e => e.Class);' in block, 'Review scaffold Class navigation before reconciliation'
block=block.replace('DeleteBehavior.ClientSetNull','DeleteBehavior.Restrict')
block=re.sub(r'entity.HasOne\(d => d\.[A-Za-z]+\).*?HasConstraintName\("[^"\n]+"\);',lambda m:m.group(0) if '.OnDelete(' in m.group(0) else m.group(0).replace('.HasConstraintName(','.OnDelete(DeleteBehavior.Restrict)\n                .HasConstraintName('),block,flags=re.S)
header="""// Reproduced by migrations/mt4/Reconcile-Mt4c.py from a fresh database scaffold.
using Microsoft.EntityFrameworkCore;
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
namespace ElectronicRegisterAPI.Infrastructure.Persistence;
public partial class ElectronicRegisterContext
{
    public virtual DbSet<CourseAdministrationEvent> CourseAdministrationEvents { get; set; }
    private static void ConfigureMt4c(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstitutionMembership>().HasAlternateKey(e=>new {e.InstitutionId,e.Id,e.UserId}).HasName("uq_mt4c_membership_actor");
        modelBuilder.Entity<StudentEnrollment>().Property<long>("Revision").HasColumnName("revision").HasColumnType("bigint(20)").HasDefaultValue(0L).IsConcurrencyToken();
"""
(target/'ElectronicRegisterContext.Mt4c.cs').write_text(header+block+'\n    }\n}\n',encoding='utf-8',newline='\n')
hook=target/'ElectronicRegisterContext.Multitenancy.cs';text=hook.read_text(encoding='utf-8-sig')
if 'ConfigureMt4c(modelBuilder);' not in text:text=text.replace('ConfigureMt4(modelBuilder);','ConfigureMt4(modelBuilder);\n        ConfigureMt4c(modelBuilder);')
hook.write_text(text,encoding='utf-8',newline='\n')
