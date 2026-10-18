| Tabella | Categoria | Percorso di attribuzione |
| --- | --- | --- |
| `audit_event` | E REQUIRES_DECISION (polimorfica: B oppure D per riga) | Exam/ExamSubmission -> Institution; platform esplicito -> NULL |
| `biennium` | B TENANT_OWNED_DIRECT | InstitutionId diretto, manifest legacy esplicito |
| `biennium_study_area` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Biennium, confrontato con StudyArea |
| `biennium_study_path` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da BienniumStudyArea, confrontato con StudyPath |
| `campus` | B TENANT_OWNED_DIRECT | institution_id -> Institution |
| `class` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da BienniumStudyPath |
| `class_subject` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Class; Subject e Teacher concordi |
| `exam` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da ClassSubject; retake stesso tenant |
| `exam_student` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Exam; Student concorde |
| `exam_submission` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Exam; Student e Grade concordi |
| `exam_violation` | B TENANT_OWNED_DIRECT | InstitutionId materializzato dalla Submission |
| `grade` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Student; ClassSubject concorde |
| `institution` | D PLATFORM_INTERNAL | Registro tenant; id identifica la Fondazione |
| `institution_membership` | B TENANT_OWNED_DIRECT | institution_id -> Institution; user_id -> User globale |
| `institution_membership_role` | C TENANT_OWNED_INDIRECT | membership_id -> Membership -> Institution |
| `institution_student_profile` | B TENANT_OWNED_DIRECT | Institution + Membership + Student dello stesso istituto |
| `institution_teacher_profile` | B TENANT_OWNED_DIRECT | Institution + Membership + Teacher dello stesso istituto |
| `mt3a_legacy_scope` | D PLATFORM_INTERNAL | Manifest SQL esplicito -> Institution provisioning di test |
| `question` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Exam |
| `question_option` | B TENANT_OWNED_DIRECT | InstitutionId materializzato da Question |
| `student` | B TENANT_OWNED_DIRECT | InstitutionId diretto; Class coerente se presente; membership opzionale |
| `study_area` | B TENANT_OWNED_DIRECT | InstitutionId diretto, manifest legacy esplicito |
| `study_path` | B TENANT_OWNED_DIRECT | InstitutionId diretto, manifest legacy esplicito |
| `subject` | B TENANT_OWNED_DIRECT | InstitutionId diretto, manifest legacy esplicito |
| `submission_answer` | B TENANT_OWNED_DIRECT | InstitutionId + exam_id materializzati dalla Submission |
| `submission_answer_option` | B TENANT_OWNED_DIRECT | InstitutionId + question_id materializzati da Answer |
| `teacher` | B TENANT_OWNED_DIRECT | InstitutionId diretto; membership opzionale tramite link |
| `user` | A GLOBAL_IDENTITY | Credenziali globali; User.Id |
