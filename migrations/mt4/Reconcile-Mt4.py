# Selective database-first reconciliation. Never copies legacy entities or context.
import re,sys,hashlib,json
from pathlib import Path
candidate=Path(sys.argv[1]);target=Path('Backend/ElectronicRegisterAPI/src/Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence')
context=(candidate/'Mt4CandidateContext.cs').read_text(encoding='utf-8-sig')
names=['CourseEdition','CourseEditionCampus','StudentEnrollment']
blocks=[]
for name in names:
    entity=(candidate/'Entities'/f'{name}.cs').read_bytes()
    destination=target/'Entities'/f'{name}.cs'
    if destination.exists() and destination.read_bytes()!=entity: raise RuntimeError('Existing entity differs: '+name)
    destination.write_bytes(entity)
    start=context.index('        modelBuilder.Entity<'+name+'>(entity =>')
    end=context.index('\n        });',start)+len('\n        });')
    block=context[start:end]
    # Legacy inverse navigations do not exist in preserved generated classes.
    for nav in ['Institution','StudyPath','Student','Campus']:
        block=re.sub(r'(entity.HasOne\(d => d\.'+nav+r'\)\.)WithMany\([^\n]*?\)',r'\1WithMany()',block)
    if name in ['CourseEdition','StudentEnrollment']:
        nav='StudyPath' if name=='CourseEdition' else 'Student'
        pattern=r'entity.HasOne\(d => d\.'+nav+r'\).*?HasConstraintName\("[^"\n]+"\);'
        block=re.sub(pattern,lambda m:m.group(0).replace('.HasPrincipalKey(p => new { p.InstitutionId, p.Id })','.HasPrincipalKey("InstitutionId", "Id")'),block,flags=re.S)
    block=block.replace('DeleteBehavior.ClientSetNull','DeleteBehavior.Restrict')
    blocks.append(block)
header='''// MT4 database-first sidecar. Reproduced by migrations/mt4/Reconcile-Mt4.py.
using ElectronicRegisterAPI.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
namespace ElectronicRegisterAPI.Infrastructure.Persistence;
public partial class ElectronicRegisterContext
{
    public virtual DbSet<CourseEdition> CourseEditions { get; set; }
    public virtual DbSet<CourseEditionCampus> CourseEditionCampuses { get; set; }
    public virtual DbSet<StudentEnrollment> StudentEnrollments { get; set; }
    private static void ConfigureMt4(ModelBuilder modelBuilder)
    {
'''
footer='''
        modelBuilder.Entity<Class>().Property<Guid?>("CourseEditionId").HasColumnName("course_edition_id")
            .HasColumnType("char(36)").UseCollation("ascii_general_ci").HasCharSet("ascii");
        modelBuilder.Entity<Class>().HasIndex("InstitutionId","CourseEditionId").HasDatabaseName("idx_mt4_class_edition");
        modelBuilder.Entity<Class>().HasOne<CourseEdition>().WithMany(e=>e.Classes)
            .HasForeignKey("InstitutionId","CourseEditionId").HasPrincipalKey(e=>new { e.InstitutionId,e.Id })
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_mt4_class_edition");
        modelBuilder.Entity<Campus>().HasAlternateKey(e=>new {e.InstitutionId,e.Id}).HasName("uq_mt4_campus_owner");
        modelBuilder.Entity<StudyPath>().HasAlternateKey("InstitutionId","Id").HasName("uq_mt3a_study_path_owner");
        modelBuilder.Entity<Student>().HasAlternateKey("InstitutionId","Id").HasName("uq_mt3a_student_owner");
        // Alternate-key shadow owners must not receive a permanent client Guid.
        // Temporary values are omitted from INSERT; MT3A SQL derives the real owner.
        modelBuilder.Entity<StudyPath>().Property<Guid>("InstitutionId").HasValueGenerator<Microsoft.EntityFrameworkCore.ValueGeneration.TemporaryGuidValueGenerator>();
        modelBuilder.Entity<Student>().Property<Guid>("InstitutionId").HasValueGenerator<Microsoft.EntityFrameworkCore.ValueGeneration.TemporaryGuidValueGenerator>();
        modelBuilder.Entity<CourseEdition>().Property(e=>e.InstitutionId).IsConcurrencyToken();
        modelBuilder.Entity<StudentEnrollment>().Property(e=>e.InstitutionId).IsConcurrencyToken();
        modelBuilder.Entity<Class>().Property<Guid?>("CourseEditionId").IsConcurrencyToken();
    }
}
'''
(target/'ElectronicRegisterContext.Mt4.cs').write_text(header+'\n'.join(blocks)+footer)
hook=target/'ElectronicRegisterContext.Multitenancy.cs';text=hook.read_text(encoding='utf-8-sig')
if 'ConfigureMt4(modelBuilder);' not in text:
    marker='''        });

    }
}'''
    assert marker in text
    text=text.replace(marker,'''        });
        ConfigureMt4(modelBuilder);

    }
}''')
    hook.write_text(text)
print('Reconciled three new entities, MT4 mapping and one hook; legacy generated files preserved.')
