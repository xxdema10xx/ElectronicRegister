from pathlib import Path
import re
root=Path.cwd(); src=root/'src'
out=['# MULTITENANCY ISOLATION MATRIX — MT-3 parziale','',
'Ricognizione prima del DDL. Tutte le API scolastiche restano bloccate ai token scoped dal gate MT-2. Nessuna query legacy viene dichiarata isolata dal solo Guid.', '',
'## Ownership e piano FK', '',
'| Risorsa / tabella | Ownership attuale / destinazione | Endpoint | Repository | Operazioni / stato |',
'| --- | --- | --- | --- | --- |']
rows={
'user':('Globale intenzionale; account management scolastico da confinare a membership','Auth, Users','UserRepository','Login globale legittimo; CRUD globale bloccato ai tenant nuovi'),
'student':('Legacy non attribuito; profilo univoco via institution_student_profile preparatorio','Student, Grade, Exam','StudentRepository','CRUD/count/search: da migrare'),
'teacher':('Legacy non attribuito; profilo univoco via institution_teacher_profile preparatorio','Teacher, Subject, Exam','TeacherRepository','CRUD/directory: da migrare'),
'subject':('Da aggiungere institution_id e UNIQUE(institution_id,name)','Subject, Grade, Exam','SubjectRepository','CRUD/lookup: da migrare'),
'study_area':('Da aggiungere institution_id e UNIQUE(institution_id,name)','non HTTP','StudyAreaRepository','CRUD interno non isolato'),
'study_path':('Da aggiungere institution_id e UNIQUE(institution_id,name)','non HTTP','StudyPathRepository','CRUD interno non isolato'),
'biennium':('Da aggiungere institution_id e UNIQUE(institution_id,start_year,end_year)','Biennium','BienniumRepository','CRUD/alias Subject da migrare'),
'biennium_study_area':('Da derivare/confermare da Biennium; composite FK ad Area stesso tenant','indiretto','StudyArea/StudyPath/Biennium','join da migrare'),
'biennium_study_path':('Da derivare/confermare da ponte e Path; composite FK','indiretto','StudyPath/Biennium','join da migrare; non CourseEdition'),
'class':('Da Institution via percorso; composite FK a catena','indiretto Student/Exam','Student/ClassSubject','join da migrare'),
'class_subject':('Da institution_id diretto; FK composite Class/Teacher/Subject','Subject/Grade/Exam','ClassSubjectRepository','tutte le ownership da migrare'),
'grade':('Da institution_id diretto; FK composite Student/ClassSubject','Grade/Exam','GradeRepository','CRUD/paged/statistics/finalizzazione da migrare'),
'exam':('Da institution_id diretto; FK composite ClassSubject e Retake','Exam','ExamRepository','CRUD/lifecycle/assigned da migrare'),
'exam_student':('Da Exam; composite FK a Student nello stesso tenant','Exam assegnazioni','ExamRepository','da migrare'),
'exam_submission':('Da institution_id diretto; composite FK Exam/Student/Grade','Exam','ExamSubmissionRepository','Start/save/submit/review/finalize/result/worker da migrare'),
'question':('Derivata da Exam; chiave (institution_id,exam_id,id) se necessaria per FK risposte','Exam','ExamRepository','nessuna API globale; indiretta da migrare'),
'question_option':('Derivata da Question; verificare domanda esatta oltre tenant','Exam','ExamRepository/ExamSubmissionRepository','FK opzioni-risposte da progettare'),
'submission_answer':('Derivata da Submission; FK composita con domanda dello stesso Exam','Exam','ExamSubmissionRepository','tenant non basta a verificare stesso Exam'),
'submission_answer_option':('Derivata da Answer; opzione deve appartenere alla domanda della risposta','Exam','ExamSubmissionRepository','ponte many-to-many, nessun DbSet esplicito; da migrare'),
'exam_violation':('Derivata da Submission','Exam','ExamSubmissionRepository','da migrare'),
'audit_event':('Da institution_id diretto; EntityId polimorfico richiede preflight per eventi non riconducibili','non HTTP diretto','AuditRepository','append atomico con Exam/submission; da migrare'),
'institution':('Tenant root; non regione','Auth istituzioni selezionabili','InstitutionRepository/MembershipAuthorizationRepository','MT1/MT2; nessun onboarding pubblico'),
'campus':('FK institution_id diretta','non HTTP','CampusRepository','lookup scoped, non sostituisce autorizzazione'),
'institution_membership':('User globale + Institution, coppia unica','Auth','InstitutionMembershipRepository/MembershipAuthorizationRepository','MT2 verificato; gestione pubblica non implementata'),
'institution_membership_role':('Derivata da membership','Auth','InstitutionMembershipRepository','MT2 verificato, nessun ruolo globale'),
'institution_student_profile':('NUOVA: ownership Student unica e FK (institution_id,membership_id)','nessuno','InstitutionProfileRepository','preparatorio; risolutore senza fallback'),
'institution_teacher_profile':('NUOVA: ownership Teacher unica e FK (institution_id,membership_id)','nessuno','InstitutionProfileRepository','preparatorio; risolutore senza fallback')}
for t,(owner,endpoint,repo,status) in rows.items(): out.append(f'| {t} | {owner} | {endpoint} | {repo} | {status} |')
def section(name,lines):
    out.extend(['','## '+name,'','```text',*lines,'```'])
context=src/'Infrastructure/ElectronicRegisterAPI.Infrastructure/Persistence/ElectronicRegisterContext.cs'
section('DbSet legacy e join',[s.strip() for s in context.read_text(encoding='utf-8-sig').splitlines() if 'DbSet<' in s or 'UsingEntity<' in s])
section('Repository e metodi (qualsiasi metodo non indicato tenant-aware rimane da migrare)',[f'{p.relative_to(src)}:{n}: {line.strip()}' for p in src.rglob('*Repository.cs') for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1) if re.search(r'public .*\(',line)])
section('Manager e interfacce di chiamata',[f'{p.relative_to(src)}:{n}: {line.strip()}' for p in src.rglob('*Manager.cs') for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1) if re.search(r'public .*\(',line)])
section('Controller: route, metodo, autorizzazione e firma action',[f'{p.relative_to(src)}:{n}: {line.strip()}' for p in src.rglob('*Controller.cs') for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1) if re.search(r'\[(Route|Http|Authorize)|public .*\(',line)])
section('DTO: identificativi risorse da validare nel tenant',[f'{p.relative_to(src)}:{n}: {line.strip()}' for p in (src/'Domain/ElectronicRegisterAPI.Domain/DTOs').glob('*.cs') for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1) if re.search(r'Guid|\b\w+Id\b',line)])
section('Raw SQL, filtri ignorati, background, scritture Grade/Audit e caller legacy',[f'{p.relative_to(src)}:{n}: {line.strip()}' for p in src.rglob('*.cs') if not any(x in p.parts for x in ('bin','obj')) for n,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1) if re.search(r'FromSql|ExecuteSql|IgnoreQueryFilters|BackgroundService|WriteLifecycleAudit|new AuditEvent|new Grade|ClaimsContext',line)])
out.extend(['','## Percorsi interni e gate', '',
'TransactionManager non assegna ownership: confini SQL/lock restano invariati. ExamSubmissionProcessor e worker sono processi interni privilegiati ancora single-tenant: batch globale limitato 50, scope per elemento, nessun principal SuperAdmin. Richiedono redesign tenant prima del cutover.', '',
'AuditRepository non ha API di lettura pubblica; BeforeJson/AfterJson restano contenuti didattici, non tecnici globali. Grade ufficiale viene scritto dal processor/review/finalize e CRUD ordinario dal GradeManager; bypass Grade esame rimane rifiutato.', '',
'Stato conclusivo di questa consegna: A parziale (profili e piano ownership, non ownership delle 21 tabelle); B preparatorio; C/D/E NON MIGRATI. Nessuna route didattica migrata o aperta. Il gate MT2 e i test HTTP negativi A/B restano invariati.'])
(root/'mt3-staging/architecture/MULTITENANCY_ISOLATION_MATRIX.md').write_text('\n'.join(out)+'\n',encoding='utf-8')
