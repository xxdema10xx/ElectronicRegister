# Inserimento voti in blocco — `POST /api/institution/grades/batch`

Voti manuali in trentesimi (interi 0–30, sufficienza 18) per **una** classe-materia e **una** data,
per esempio una verifica svolta fuori piattaforma. Stessi gate di rollout, stessa risoluzione
classe-materia e stessi controlli di scope del `POST /api/institution/grades` singolo.
Scala e formula dei voti: [EXAM_FRONTEND_CONTRACTS.md](EXAM_FRONTEND_CONTRACTS.md#scala-dei-voti-dalla-release-di-schema-er-grade-scale-1).

## Accesso

Ruoli **InstitutionAdmin, Staff, Teacher** con permesso `GradeCreate`. Student: 403. Senza token: 401.
Il Teacher scrive solo sulle proprie classi-materia; per una classe-materia altrui la risposta è la
stessa del POST singolo (la lettura con scope la nasconde: 404).

## Richiesta

```json
{
  "eventId": "6f1c2a9e-3b0d-4c51-9a77-2f5c8d1e0b44",
  "subjectId": "0e7b9f52-81c4-4a3e-b6d2-5c9a1f3e7d20",
  "classId": "a3d5e8f1-2c4b-4e6a-9f0d-7b1c3e5a9d82",
  "date": "2026-11-17",
  "description": "Verifica scritta in presenza",
  "grades": [
    { "studentId": "5b2e7c91-4f3a-4d8e-a1b6-9c0d2e4f6a13", "value": 24 },
    { "studentId": "c8f1d3a5-7e9b-4c2d-8a6f-1b3e5d7c9f04", "value": 17 }
  ]
}
```

| Campo | Regola |
|---|---|
| `eventId` | Obbligatorio, generato dal client **una volta per invio** e riusato nei retry. |
| `classSubjectId` | Alternativa a `subjectId`/`classId`: identifica direttamente la classe-materia (come i selettori di `/exams/class-subjects`). Se presenti anche `subjectId`/`classId` devono coincidere, altrimenti 400. |
| `subjectId` | Obbligatorio se manca `classSubjectId`. |
| `classId` | Facoltativo se la materia identifica una sola classe-materia visibile al chiamante (per il Teacher: le sue). Più classi-materia possibili → 400 "indicare classId o classSubjectId". |
| `date` | Obbligatoria, `yyyy-MM-dd`. |
| `description` | Facoltativa; trim, 1–200 caratteri, niente caratteri di controllo. Vuota = assente. |
| `grades` | Da 1 a 100 righe; ogni studente al massimo una volta; `value` intero 0–30. |

Una sola POST conta una volta nel rate limit (60 POST/min per IP). 100 righe stanno ampiamente sotto
il limite di 1 MiB del body. Per lo Staff i campi sconosciuti di primo livello sono rifiutati (400),
come per le altre scritture.

## Risposta 201

```json
{
  "eventId": "6f1c2a9e-3b0d-4c51-9a77-2f5c8d1e0b44",
  "created": [
    { "studentId": "5b2e7c91-4f3a-4d8e-a1b6-9c0d2e4f6a13", "gradeId": "1d4f6a8c-0b2e-4d7f-9a3c-5e7b9d1f3a26" },
    { "studentId": "c8f1d3a5-7e9b-4c2d-8a6f-1b3e5d7c9f04", "gradeId": "7a9c1e3f-5b7d-4f2a-8c0e-2d4f6b8a0c39" }
  ]
}
```

`created` segue l'ordine di `grades`. I voti creati sono voti manuali ordinari: compaiono in
`GET /grades`, `/grades/paged`, `/grades/{id}` (con `description`) e si modificano/cancellano con
`PUT`/`DELETE /grades/{id}` come gli altri manuali (diversamente dai voti nati da esame, che restano 409).

## Tutto o niente

La validazione avviene prima di qualsiasi scrittura e l'inserimento è in un'unica transazione:
se anche una sola riga non è valida **non viene salvato nulla**.

### 400 — errori per riga

```json
{
  "title": "Voti del blocco non validi",
  "status": 400,
  "detail": "Voti del blocco non validi",
  "code": "GradeBatchInvalid",
  "errors": [
    { "index": 2, "studentId": "5b2e7c91-4f3a-4d8e-a1b6-9c0d2e4f6a13", "code": "duplicate_student", "message": "Lo studente compare più di una volta nel blocco." },
    { "index": 3, "studentId": "e2a4c6f8-1b3d-4e5f-a7c9-0d2f4b6e8a15", "code": "student_not_found", "message": "Lo studente non esiste." },
    { "index": 4, "studentId": "c8f1d3a5-7e9b-4c2d-8a6f-1b3e5d7c9f04", "code": "invalid_value", "message": "Il voto deve essere un numero intero compreso tra 0 e 30." }
  ]
}
```

| `code` | Significato |
|---|---|
| `duplicate_student` | Lo studente compare già in una riga precedente (`index` è la ripetizione). |
| `student_not_found` | Studente inesistente **o di un altro istituto**: le due situazioni sono indistinguibili. |
| `student_not_in_class` | Studente dell'istituto ma non della classe della classe-materia. |
| `invalid_value` | Valore fuori 0–30 o non intero. Può affiancare un errore di studente sulla stessa riga. |

Gli errori di forma della richiesta (eventId vuoto, 0 o più di 100 righe, data mancante, descrizione
non valida, classe-materia ambigua o incoerente) sono 400 senza `errors`:

```json
{ "title": "Richiesta non valida", "status": 400, "detail": "Richiesta non valida" }
```

### 403 / 404

```json
{ "title": "Non autorizzato", "status": 403, "detail": "Non autorizzato" }
{ "title": "La risorsa non esiste", "status": 404, "detail": "La risorsa non esiste" }
```

403: ruolo Student o permesso `GradeCreate` assente. 404: classe-materia inesistente, di un altro
istituto o, per il Teacher, non sua (come il POST singolo).

## Idempotenza (`eventId`) e 409

- Stesso `eventId` e stesso payload (classe-materia, data, descrizione, coppie studente/voto) →
  **201 con la stessa risposta**, nessun voto duplicato e nessun nuovo audit.
- Stesso `eventId` con payload diverso → **409**:

```json
{ "title": "Operazione non consentita", "status": 409, "detail": "Operazione non consentita" }
```

- Richieste concorrenti con lo stesso `eventId`: una crea i voti, le altre ricevono la stessa risposta 201.

L'idempotenza non usa un ledger separato: ogni voto del blocco conserva `batch_event_id` e la chiave
univoca `uq_grade_batch_student (institution_id, batch_event_id, student_id)` impedisce i duplicati
anche in concorrenza. Il replay confronta il payload con i voti salvati. Se nel frattempo uno di quei
voti è stato modificato o cancellato con `PUT`/`DELETE`, il replay non coincide più e risponde 409:
il client deve generare un nuovo `eventId` per un nuovo invio.

## Audit

Un solo evento `grade.batch.create` per blocco (`audit_event`, entità `grade`, primo voto creato) con
attore e membership, `eventId`, classe-materia, data e numero di voti. **Nessun valore di voto** è
registrato nei log applicativi o nell'audit.
