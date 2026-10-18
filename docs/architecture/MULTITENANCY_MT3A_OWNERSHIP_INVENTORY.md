# MT-3A: inventario effettivo MySQL e indici

Rilevato su `electronic_register_test`, MySQL 5.7.24, dopo 003-011. **28 tabelle: 27 preesistenti e un manifest interno.**
Le categorie sono A GLOBAL_IDENTITY, B TENANT_OWNED_DIRECT, C TENANT_OWNED_INDIRECT, D PLATFORM_INTERNAL, E REQUIRES_DECISION.
Audit e intenzionalmente polimorfico: E indica classificazione obbligatoria per riga; i casi accettati sono B (didattico) oppure D (metadati platform). Non restano eventi storici non classificati nel dataset migrato.
Tutte le 19 tabelle scolastiche, incluse le dipendenze/ponte, materializzano ownership B NOT NULL. Per i ponti multi-parent serve a chiudere le FK; per dipendenze con un solo parent e una scelta uniforme e verificabile, con costo di spazio/scrittura esplicito. Il solo ponte ruoli MT1 resta C, rigorosamente derivato dal singolo parent.
La sezione per tabella riporta **tutte** le PK/UNIQUE/FK effettive, comprese le FK legacy conservate. Le nuove FK sono identificate da `fk_mt3a_`. I controlli applicativi comuni sono autorizzazione caller+tenant, stato/ruoli e filtri SQL in ogni query, ancora necessari prima di C/D/E.

## audit_event

- **PK:** `(id)`.
- **UNIQUE:** nessuna oltre PK.
- **Ownership / attribuzione:** E REQUIRES_DECISION (polimorfica: B oppure D per riga); Exam/ExamSubmission -> Institution; platform esplicito -> NULL.
- **Rischio, modifica e limite applicativo:** Entita nota: B, owner storico immutabile anche dopo cancellazione. Solo platform.* senza EntityId/JSON: D. Sconosciuti non diventano globali: preflight/trigger rifiutano.
- **FK SQL effettive:**
  - `fk_audit_actor`: `(actor_user_id) -> user(id)`; DELETE SET NULL, UPDATE RESTRICT.
  - `fk_mt3a_audit_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## biennium

- **PK:** `(id)`.
- **UNIQUE:** `uq_biennium(institution_id,start_year,end_year)`; `uq_mt3a_biennium_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId diretto, manifest legacy esplicito.
- **Rischio, modifica e limite applicativo:** Coppia date unica per fondazione; significato del biennio invariato. Nessuna CourseEdition.
- **FK SQL effettive:**
  - `fk_mt3a_biennium_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## biennium_study_area

- **PK:** `(id)`.
- **UNIQUE:** `uq_biennium_area(biennium_id,study_area_id)`; `uq_mt3a_biennium_study_area_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da Biennium, confrontato con StudyArea.
- **Rischio, modifica e limite applicativo:** Entrambi i parent devono essere dello stesso tenant; CASCADE Biennium e RESTRICT Area conservati.
- **FK SQL effettive:**
  - `fk_bsa_area`: `(study_area_id) -> study_area(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_bsa_biennium`: `(biennium_id) -> biennium(id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_mt3a_biennium_study_area_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_bsa_area`: `(institution_id,study_area_id) -> study_area(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_bsa_biennium`: `(institution_id,biennium_id) -> biennium(institution_id,id)`; DELETE CASCADE, UPDATE RESTRICT.

## biennium_study_path

- **PK:** `(id)`.
- **UNIQUE:** `uq_bsa_path(biennium_study_area_id,study_path_id)`; `uq_mt3a_biennium_study_path_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da BienniumStudyArea, confrontato con StudyPath.
- **Rischio, modifica e limite applicativo:** Entrambi i parent vincolati. Non reinterpretato come CourseEdition.
- **FK SQL effettive:**
  - `fk_bsp_bsa`: `(biennium_study_area_id) -> biennium_study_area(id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_bsp_path`: `(study_path_id) -> study_path(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_biennium_study_path_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_bsp_bsa`: `(institution_id,biennium_study_area_id) -> biennium_study_area(institution_id,id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_mt3a_bsp_path`: `(institution_id,study_path_id) -> study_path(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.

## campus

- **PK:** `(id)`.
- **UNIQUE:** nessuna oltre PK.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; institution_id -> Institution.
- **Rischio, modifica e limite applicativo:** FK RESTRICT MT1; autorizzazione caller nel repository/service.
- **FK SQL effettive:**
  - `fk_campus_institution`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## class

- **PK:** `(id)`.
- **UNIQUE:** `uq_class(biennium_study_path_id,name)`; `uq_mt3a_class_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da BienniumStudyPath.
- **Rischio, modifica e limite applicativo:** Class A -> percorso B impedito da FK composita.
- **FK SQL effettive:**
  - `fk_class_bsp`: `(biennium_study_path_id) -> biennium_study_path(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_class_bsp`: `(institution_id,biennium_study_path_id) -> biennium_study_path(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_class_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## class_subject

- **PK:** `(id)`.
- **UNIQUE:** `uq_class_subject(class_id,subject_id)`; `uq_mt3a_class_subject_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da Class; Subject e Teacher concordi.
- **Rischio, modifica e limite applicativo:** Tre FK composite; ruoli correnti docente e diritti sul gruppo restano applicativi.
- **FK SQL effettive:**
  - `fk_cs_class`: `(class_id) -> class(id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_cs_subject`: `(subject_id) -> subject(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_cs_teacher`: `(teacher_id) -> teacher(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_class_subject_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_cs_class`: `(institution_id,class_id) -> class(institution_id,id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_mt3a_cs_subject`: `(institution_id,subject_id) -> subject(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_cs_teacher`: `(institution_id,teacher_id) -> teacher(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.

## exam

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_exam_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da ClassSubject; retake stesso tenant.
- **Rischio, modifica e limite applicativo:** retake_institution_id preserva SET NULL. Cicli/self-retake, stato, disponibilita e calcolo restano regole applicative.
- **FK SQL effettive:**
  - `fk_exam_class_subject`: `(class_subject_id) -> class_subject(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_exam_retake_of`: `(retake_of_exam_id) -> exam(id)`; DELETE SET NULL, UPDATE RESTRICT.
  - `fk_mt3a_exam_class_subject`: `(institution_id,class_subject_id) -> class_subject(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_exam_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_exam_retake_of`: `(retake_institution_id,retake_of_exam_id) -> exam(institution_id,id)`; DELETE SET NULL, UPDATE RESTRICT.

## exam_student

- **PK:** `(exam_id,student_id)`.
- **UNIQUE:** nessuna oltre PK.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da Exam; Student concorde.
- **Rischio, modifica e limite applicativo:** Ponte con PK originale (ExamId,StudentId); due FK composite. Iscrizione/classe ammessa resta applicativa.
- **FK SQL effettive:**
  - `fk_exam_student_exam`: `(exam_id) -> exam(id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_exam_student_student`: `(student_id) -> student(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_exam_student_exam`: `(institution_id,exam_id) -> exam(institution_id,id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_mt3a_exam_student_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_exam_student_student`: `(institution_id,student_id) -> student(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.

## exam_submission

- **PK:** `(id)`.
- **UNIQUE:** `uq_exam_submission_grade(grade_id)`; `uq_exam_submission_student(exam_id,student_id)`; `uq_mt3a_exam_submission_owner(institution_id,id)`; `uq_mt3a_submission_exam(institution_id,exam_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da Exam; Student e Grade concordi.
- **Rischio, modifica e limite applicativo:** grade_institution_id preserva SET NULL. Reviewer e User globale: ruolo/membership al momento della review da verificare in D.
- **FK SQL effettive:**
  - `fk_exam_submission_exam`: `(exam_id) -> exam(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_exam_submission_grade`: `(grade_id) -> grade(id)`; DELETE SET NULL, UPDATE RESTRICT.
  - `fk_exam_submission_reviewer`: `(reviewed_by_user_id) -> user(id)`; DELETE SET NULL, UPDATE RESTRICT.
  - `fk_exam_submission_student`: `(student_id) -> student(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_exam_submission_exam`: `(institution_id,exam_id) -> exam(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_exam_submission_grade`: `(grade_institution_id,grade_id) -> grade(institution_id,id)`; DELETE SET NULL, UPDATE RESTRICT.
  - `fk_mt3a_exam_submission_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_exam_submission_student`: `(institution_id,student_id) -> student(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.

## exam_violation

- **PK:** `(id)`.
- **UNIQUE:** nessuna oltre PK.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato dalla Submission.
- **Rischio, modifica e limite applicativo:** Un parent sarebbe sufficiente per ownership indiretta; scelta materializzazione uniforme e FK esplicita, senza UNIQUE tenant/id superflua.
- **FK SQL effettive:**
  - `fk_exam_violation_submission`: `(submission_id) -> exam_submission(id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_mt3a_exam_violation_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_exam_violation_submission`: `(institution_id,submission_id) -> exam_submission(institution_id,id)`; DELETE CASCADE, UPDATE RESTRICT.

## grade

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_grade_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da Student; ClassSubject concorde.
- **Rischio, modifica e limite applicativo:** Due FK composite. Coerenza stesso studente/materia del Grade ufficiale entro tenant resta applicativa; formule immutate.
- **FK SQL effettive:**
  - `fk_grade_class_subject`: `(class_subject_id) -> class_subject(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_grade_student`: `(student_id) -> student(id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_mt3a_grade_class_subject`: `(institution_id,class_subject_id) -> class_subject(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_grade_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_grade_student`: `(institution_id,student_id) -> student(institution_id,id)`; DELETE CASCADE, UPDATE RESTRICT.

## institution

- **PK:** `(id)`.
- **UNIQUE:** `uq_institution_code(code)`.
- **Ownership / attribuzione:** D PLATFORM_INTERNAL; Registro tenant; id identifica la Fondazione.
- **Rischio, modifica e limite applicativo:** Non e identita globale autorizzante. Stato/ruoli verificati da MT2; attivazione/onboarding fuori scope.
- **FK SQL effettive:**
  nessuna; ownership/catalogo o identita come indicato sopra.

## institution_membership

- **PK:** `(id)`.
- **UNIQUE:** `uq_membership_institution_id(institution_id,id)`; `uq_membership_user_institution(user_id,institution_id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; institution_id -> Institution; user_id -> User globale.
- **Rischio, modifica e limite applicativo:** SQL non autorizza ruolo/stato; MT2 rilegge Institution/Membership Active. Non spostare identita/istituto con CRUD generico.
- **FK SQL effettive:**
  - `fk_membership_institution`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_membership_user`: `(user_id) -> user(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## institution_membership_role

- **PK:** `(membership_id,role)`.
- **UNIQUE:** nessuna oltre PK.
- **Ownership / attribuzione:** C TENANT_OWNED_INDIRECT; membership_id -> Membership -> Institution.
- **Rischio, modifica e limite applicativo:** Singolo parent rigoroso: InstitutionId duplicato non necessario. Nessun ruolo globale.
- **FK SQL effettive:**
  - `fk_membership_role_membership`: `(membership_id) -> institution_membership(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## institution_student_profile

- **PK:** `(institution_id,membership_id)`.
- **UNIQUE:** `uq_institution_student_profile_student(student_id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; Institution + Membership + Student dello stesso istituto.
- **Rischio, modifica e limite applicativo:** Composito aggiuntivo allo Student, UNIQUE(student_id) conservata. Ruolo Student e identita caller verificati applicativamente.
- **FK SQL effettive:**
  - `fk_mt3a_student_profile_owner`: `(institution_id,student_id) -> student(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_student_profile_membership`: `(institution_id,membership_id) -> institution_membership(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_student_profile_student`: `(student_id) -> student(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## institution_teacher_profile

- **PK:** `(institution_id,membership_id)`.
- **UNIQUE:** `uq_institution_teacher_profile_teacher(teacher_id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; Institution + Membership + Teacher dello stesso istituto.
- **Rischio, modifica e limite applicativo:** Composito aggiuntivo al Teacher, UNIQUE(teacher_id) conservata. Ruolo Teacher e identita caller verificati applicativamente.
- **FK SQL effettive:**
  - `fk_mt3a_teacher_profile_owner`: `(institution_id,teacher_id) -> teacher(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_teacher_profile_membership`: `(institution_id,membership_id) -> institution_membership(institution_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_teacher_profile_teacher`: `(teacher_id) -> teacher(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## mt3a_legacy_scope

- **PK:** `(scope)`.
- **UNIQUE:** `institution_id(institution_id)`.
- **Ownership / attribuzione:** D PLATFORM_INTERNAL; Manifest SQL esplicito -> Institution provisioning di test.
- **Rischio, modifica e limite applicativo:** ENUM/PK singleton legacy, FK RESTRICT. Solo compatibilita root legacy: nessun TenantContext HTTP, diritto globale o provisioning User.
- **FK SQL effettive:**
  - `fk_mt3a_legacy_scope`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## question

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_question_exam(institution_id,exam_id,id)`; `uq_mt3a_question_owner(institution_id,id)`; `uq_question_position(exam_id,position)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da Exam.
- **Rischio, modifica e limite applicativo:** Chiave (istituto,esame,id) per vincolare le risposte allo stesso Exam.
- **FK SQL effettive:**
  - `fk_mt3a_question_exam`: `(institution_id,exam_id) -> exam(institution_id,id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_mt3a_question_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_question_exam`: `(exam_id) -> exam(id)`; DELETE CASCADE, UPDATE RESTRICT.

## question_option

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_option_question(institution_id,question_id,id)`; `uq_question_option_position(question_id,position)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId materializzato da Question.
- **Rischio, modifica e limite applicativo:** Chiave (istituto,domanda,id) per opzioni esatte; correttezza/grading e cardinalita selezioni restano applicativi.
- **FK SQL effettive:**
  - `fk_mt3a_question_option_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_question_option_question`: `(institution_id,question_id) -> question(institution_id,id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_question_option_question`: `(question_id) -> question(id)`; DELETE CASCADE, UPDATE RESTRICT.

## student

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_student_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId diretto; Class coerente se presente; membership opzionale.
- **Rischio, modifica e limite applicativo:** class_institution_id nullable preserva SET NULL, trigger lo uguaglia al proprietario quando ClassId presente. Nessun enrollment nuovo.
- **FK SQL effettive:**
  - `fk_mt3a_student_class`: `(class_institution_id,class_id) -> class(institution_id,id)`; DELETE SET NULL, UPDATE RESTRICT.
  - `fk_mt3a_student_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_student_class`: `(class_id) -> class(id)`; DELETE SET NULL, UPDATE RESTRICT.

## study_area

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_study_area_owner(institution_id,id)`; `uq_study_area_name(institution_id,name)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId diretto, manifest legacy esplicito.
- **Rischio, modifica e limite applicativo:** Catalogo modificabile della fondazione; nome unico nel tenant. Nessun catalogo globale implicito.
- **FK SQL effettive:**
  - `fk_mt3a_study_area_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## study_path

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_study_path_owner(institution_id,id)`; `uq_study_path_name(institution_id,name)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId diretto, manifest legacy esplicito.
- **Rischio, modifica e limite applicativo:** Catalogo modificabile della fondazione; nome unico nel tenant.
- **FK SQL effettive:**
  - `fk_mt3a_study_path_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## subject

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_subject_owner(institution_id,id)`; `uq_subject_name(institution_id,name)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId diretto, manifest legacy esplicito.
- **Rischio, modifica e limite applicativo:** Nome unico nel tenant; stesso nome A/B, record e Guid distinti.
- **FK SQL effettive:**
  - `fk_mt3a_subject_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## submission_answer

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_answer_question(institution_id,question_id,id)`; `uq_submission_answer(submission_id,question_id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId + exam_id materializzati dalla Submission.
- **Rischio, modifica e limite applicativo:** FK triple a Submission e Question: stessa fondazione E stesso Exam, anche entro il medesimo tenant.
- **FK SQL effettive:**
  - `fk_mt3a_submission_answer_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_submission_answer_question`: `(institution_id,exam_id,question_id) -> question(institution_id,exam_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_submission_answer_submission`: `(institution_id,exam_id,submission_id) -> exam_submission(institution_id,exam_id,id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_submission_answer_question`: `(question_id) -> question(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_submission_answer_submission`: `(submission_id) -> exam_submission(id)`; DELETE CASCADE, UPDATE RESTRICT.

## submission_answer_option

- **PK:** `(submission_answer_id,option_id)`.
- **UNIQUE:** nessuna oltre PK.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId + question_id materializzati da Answer.
- **Rischio, modifica e limite applicativo:** Ponte senza DbSet esplicito: FK triple ad Answer e Option garantiscono la domanda esatta. Mapping dictionary legacy conservato.
- **FK SQL effettive:**
  - `fk_mt3a_submission_answer_option_answer`: `(institution_id,question_id,submission_answer_id) -> submission_answer(institution_id,question_id,id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_mt3a_submission_answer_option_option`: `(institution_id,question_id,option_id) -> question_option(institution_id,question_id,id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_mt3a_submission_answer_option_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.
  - `fk_submission_answer_option_answer`: `(submission_answer_id) -> submission_answer(id)`; DELETE CASCADE, UPDATE RESTRICT.
  - `fk_submission_answer_option_option`: `(option_id) -> question_option(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## teacher

- **PK:** `(id)`.
- **UNIQUE:** `uq_mt3a_teacher_owner(institution_id,id)`.
- **Ownership / attribuzione:** B TENANT_OWNED_DIRECT; InstitutionId diretto; membership opzionale tramite link.
- **Rischio, modifica e limite applicativo:** Profilo storico senza User attribuito al manifest, senza inventare membership. Ownership immutabile.
- **FK SQL effettive:**
  - `fk_mt3a_teacher_owner`: `(institution_id) -> institution(id)`; DELETE RESTRICT, UPDATE RESTRICT.

## user

- **PK:** `(id)`.
- **UNIQUE:** `idx_user_student(student_id)`; `idx_user_teacher(teacher_id)`; `uq_user_email(email)`.
- **Ownership / attribuzione:** A GLOBAL_IDENTITY; Credenziali globali; User.Id.
- **Rischio, modifica e limite applicativo:** User.Role e link Student/Teacher sono solo bridge legacy; CRUD account richiede membership autorizzata in C.
- **FK SQL effettive:**
  - `fk_user_student`: `(student_id) -> student(id)`; DELETE SET NULL, UPDATE RESTRICT.
  - `fk_user_teacher`: `(teacher_id) -> teacher(id)`; DELETE SET NULL, UPDATE RESTRICT.

## Indici nuovi o con scope modificato: analisi per indice

Ogni riga descrive un B-tree mantenuto a ogni INSERT/DELETE e UPDATE delle colonne della chiave. I Guid sono CHAR(36) ASCII: circa 36 byte per componente Guid, oltre overhead InnoDB e PK; nomi UTF8 fino a 4 byte/carattere. Non sono misure di latenza o benchmark.
Ordine institution_id prima delle risorse: uguaglianza tenant e poi parent/id; il tenant isolato usa il prefisso sinistro. Le scope nullable dei tre SET NULL hanno lo stesso ordine rispetto al riferimento. Gli indici legacy restano necessari a FK/navigazioni/query legacy, anche se alcune future query potranno usare indici scoped.

| Tabella / indice | Ordine colonne | Predicato supportato e vincolo | Costo / ridondanza |
| --- | --- | --- | --- |
| `audit_event.idx_mt3a_audit_owner_created` | `institution_id,created_at` | institution_id = ? AND created_at tra estremi; ordinamento audit per tenant; fk_mt3a_audit_owner | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `biennium.uq_biennium` | `institution_id,start_year,end_year` | institution_id = ? AND start_year = ? AND end_year = ?; UNIQUE; fk_mt3a_biennium_owner | 3 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `biennium.uq_mt3a_biennium_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; fk_mt3a_biennium_owner, parent fk_mt3a_bsa_biennium | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `biennium_study_area.idx_mt3a_bsa_area` | `institution_id,study_area_id` | institution_id = ? AND study_area_id = ?; fk_mt3a_biennium_study_area_owner, fk_mt3a_bsa_area | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `biennium_study_area.idx_mt3a_bsa_biennium` | `institution_id,biennium_id` | institution_id = ? AND biennium_id = ?; fk_mt3a_biennium_study_area_owner, fk_mt3a_bsa_biennium | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `biennium_study_area.uq_mt3a_biennium_study_area_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; fk_mt3a_biennium_study_area_owner, parent fk_mt3a_bsp_bsa | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `biennium_study_path.idx_mt3a_bsp_bsa` | `institution_id,biennium_study_area_id` | institution_id = ? AND biennium_study_area_id = ?; fk_mt3a_biennium_study_path_owner, fk_mt3a_bsp_bsa | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `biennium_study_path.idx_mt3a_bsp_path` | `institution_id,study_path_id` | institution_id = ? AND study_path_id = ?; fk_mt3a_biennium_study_path_owner, fk_mt3a_bsp_path | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `biennium_study_path.uq_mt3a_biennium_study_path_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; fk_mt3a_biennium_study_path_owner, parent fk_mt3a_class_bsp | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `class.idx_mt3a_class_bsp` | `institution_id,biennium_study_path_id` | institution_id = ? AND biennium_study_path_id = ?; fk_mt3a_class_bsp, fk_mt3a_class_owner | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `class.uq_mt3a_class_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; fk_mt3a_class_owner, parent fk_mt3a_cs_class, parent fk_mt3a_student_class | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `class_subject.idx_mt3a_cs_class` | `institution_id,class_id` | institution_id = ? AND class_id = ?; fk_mt3a_class_subject_owner, fk_mt3a_cs_class | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `class_subject.idx_mt3a_cs_subject` | `institution_id,subject_id` | institution_id = ? AND subject_id = ?; fk_mt3a_class_subject_owner, fk_mt3a_cs_subject | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `class_subject.idx_mt3a_cs_teacher` | `institution_id,teacher_id` | institution_id = ? AND teacher_id = ?; fk_mt3a_class_subject_owner, fk_mt3a_cs_teacher | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `class_subject.uq_mt3a_class_subject_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; fk_mt3a_class_subject_owner, parent fk_mt3a_exam_class_subject, parent fk_mt3a_grade_class_subject | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `exam.idx_mt3a_exam_class_subject` | `institution_id,class_subject_id` | institution_id = ? AND class_subject_id = ?; fk_mt3a_exam_class_subject, fk_mt3a_exam_owner | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `exam.idx_mt3a_exam_retake_of` | `retake_institution_id,retake_of_exam_id` | retake_institution_id = ? AND retake_of_exam_id = ?; fk_mt3a_exam_retake_of | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `exam.uq_mt3a_exam_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; fk_mt3a_exam_owner, parent fk_mt3a_exam_retake_of, parent fk_mt3a_exam_student_exam, parent fk_mt3a_exam_submission_exam, parent fk_mt3a_question_exam | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `exam_student.idx_mt3a_exam_student_exam` | `institution_id,exam_id` | institution_id = ? AND exam_id = ?; fk_mt3a_exam_student_exam, fk_mt3a_exam_student_owner | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `exam_student.idx_mt3a_exam_student_student` | `institution_id,student_id` | institution_id = ? AND student_id = ?; fk_mt3a_exam_student_owner, fk_mt3a_exam_student_student | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `exam_submission.idx_mt3a_exam_submission_grade` | `grade_institution_id,grade_id` | grade_institution_id = ? AND grade_id = ?; fk_mt3a_exam_submission_grade | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `exam_submission.idx_mt3a_exam_submission_student` | `institution_id,student_id` | institution_id = ? AND student_id = ?; fk_mt3a_exam_submission_owner, fk_mt3a_exam_submission_student | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `exam_submission.uq_mt3a_exam_submission_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; fk_mt3a_exam_submission_owner, parent fk_mt3a_exam_violation_submission | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `exam_submission.uq_mt3a_submission_exam` | `institution_id,exam_id,id` | institution_id = ? AND exam_id = ? AND id = ?; UNIQUE; fk_mt3a_exam_submission_exam, fk_mt3a_exam_submission_owner, parent fk_mt3a_submission_answer_submission | 3 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `exam_violation.idx_mt3a_exam_violation_submission` | `institution_id,submission_id` | institution_id = ? AND submission_id = ?; fk_mt3a_exam_violation_owner, fk_mt3a_exam_violation_submission | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `grade.idx_mt3a_grade_class_subject` | `institution_id,class_subject_id` | institution_id = ? AND class_subject_id = ?; fk_mt3a_grade_class_subject, fk_mt3a_grade_owner | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `grade.idx_mt3a_grade_student` | `institution_id,student_id` | institution_id = ? AND student_id = ?; fk_mt3a_grade_owner, fk_mt3a_grade_student | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `grade.uq_mt3a_grade_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; parent fk_mt3a_exam_submission_grade, fk_mt3a_grade_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `institution_student_profile.idx_mt3a_student_profile` | `institution_id,student_id` | institution_id = ? AND student_id = ?; fk_mt3a_student_profile_owner | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `institution_teacher_profile.idx_mt3a_teacher_profile` | `institution_id,teacher_id` | institution_id = ? AND teacher_id = ?; fk_mt3a_teacher_profile_owner | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `mt3a_legacy_scope.PRIMARY` | `scope` | scope = ?; UNIQUE;  | 1 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `mt3a_legacy_scope.institution_id` | `institution_id` | institution_id = ?; UNIQUE; fk_mt3a_legacy_scope | 1 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `question.uq_mt3a_question_exam` | `institution_id,exam_id,id` | institution_id = ? AND exam_id = ? AND id = ?; UNIQUE; fk_mt3a_question_exam, fk_mt3a_question_owner, parent fk_mt3a_submission_answer_question | 3 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `question.uq_mt3a_question_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; fk_mt3a_question_owner, parent fk_mt3a_question_option_question | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `question_option.uq_mt3a_option_question` | `institution_id,question_id,id` | institution_id = ? AND question_id = ? AND id = ?; UNIQUE; fk_mt3a_question_option_owner, fk_mt3a_question_option_question, parent fk_mt3a_submission_answer_option_option | 3 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `student.idx_mt3a_student_class` | `class_institution_id,class_id` | class_institution_id = ? AND class_id = ?; fk_mt3a_student_class | 2 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `student.uq_mt3a_student_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; parent fk_mt3a_exam_student_student, parent fk_mt3a_exam_submission_student, parent fk_mt3a_grade_student, parent fk_mt3a_student_profile_owner, fk_mt3a_student_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `study_area.uq_mt3a_study_area_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; parent fk_mt3a_bsa_area, fk_mt3a_study_area_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `study_area.uq_study_area_name` | `institution_id,name` | institution_id = ? AND name = ?; UNIQUE; fk_mt3a_study_area_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `study_path.uq_mt3a_study_path_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; parent fk_mt3a_bsp_path, fk_mt3a_study_path_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `study_path.uq_study_path_name` | `institution_id,name` | institution_id = ? AND name = ?; UNIQUE; fk_mt3a_study_path_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `subject.uq_mt3a_subject_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; parent fk_mt3a_cs_subject, fk_mt3a_subject_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `subject.uq_subject_name` | `institution_id,name` | institution_id = ? AND name = ?; UNIQUE; fk_mt3a_subject_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `submission_answer.idx_mt3a_submission_answer_question` | `institution_id,exam_id,question_id` | institution_id = ? AND exam_id = ? AND question_id = ?; fk_mt3a_submission_answer_owner, fk_mt3a_submission_answer_question | 3 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `submission_answer.idx_mt3a_submission_answer_submission` | `institution_id,exam_id,submission_id` | institution_id = ? AND exam_id = ? AND submission_id = ?; fk_mt3a_submission_answer_owner, fk_mt3a_submission_answer_submission | 3 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `submission_answer.uq_mt3a_answer_question` | `institution_id,question_id,id` | institution_id = ? AND question_id = ? AND id = ?; UNIQUE; fk_mt3a_submission_answer_owner, parent fk_mt3a_submission_answer_option_answer | 3 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |
| `submission_answer_option.idx_mt3a_submission_answer_option_answer` | `institution_id,question_id,submission_answer_id` | institution_id = ? AND question_id = ? AND submission_answer_id = ?; fk_mt3a_submission_answer_option_answer, fk_mt3a_submission_answer_option_owner | 3 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `submission_answer_option.idx_mt3a_submission_answer_option_option` | `institution_id,question_id,option_id` | institution_id = ? AND question_id = ? AND option_id = ?; fk_mt3a_submission_answer_option_option, fk_mt3a_submission_answer_option_owner | 3 componenti; B-tree per scrittura. Supporto FK/predicato esplicito. |
| `teacher.uq_mt3a_teacher_owner` | `institution_id,id` | institution_id = ? AND id = ?; UNIQUE; parent fk_mt3a_cs_teacher, parent fk_mt3a_teacher_profile_owner, fk_mt3a_teacher_owner | 2 componenti; B-tree per scrittura. Unicita scoped obbligatoria. |

Sei indici transitori di 006 sono rimossi da 010/011, dopo avere verificato i sostituti:

| Rimosso | Sostituto effettivo |
| --- | --- |
| question_option.uq_mt3a_question_option_owner | uq_mt3a_option_question; PRIMARY(id) per lookup id |
| submission_answer.uq_mt3a_submission_answer_owner | uq_mt3a_answer_question; PRIMARY(id) per lookup id |
| exam_violation.uq_mt3a_exam_violation_owner | idx_mt3a_exam_violation_submission per FK Institution; PRIMARY(id) |
| exam_submission.idx_mt3a_exam_submission_exam | uq_mt3a_submission_exam(institution_id,exam_id,id), stesso prefisso |
| question.idx_mt3a_question_exam | uq_mt3a_question_exam(institution_id,exam_id,id), stesso prefisso |
| question_option.idx_mt3a_question_option_question | uq_mt3a_option_question(institution_id,question_id,id), stesso prefisso |

Le FK non sono rimosse: InnoDB usa i B-tree sostitutivi. Nessun indice speculativo su stato/date del worker, nessun Redis/cache o EXPLAIN prestazionale dichiarato. Il PK(scope) e UNIQUE(institution_id) del manifest tecnico limitano configurazioni duplicate; non indicizzano una query scolastica.
