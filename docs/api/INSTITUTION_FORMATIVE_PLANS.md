# API istituzionali dei piani formativi

Prefisso `/api/institution`; JWT InstitutionAccess e membership corrente. Attivare
FormativePlans, SchoolApi e CourseApi solo dopo migrazione verificata. Default false;
il primo flag chiuso all'avvio esclude le nuove route (404); la chiusura runtime nega
le richieste e le scritture in corso prima del commit (403). Attivazione con restart.

A = InstitutionAdmin, S = Staff, T = Teacher, U = Student. A non implica T/U.
T/U usano le sole route dell'edizione, filtrate per assegnazioni/iscrizioni reali.
PlatformOperator e ruoli legacy non concedono privilegi. Staff+Teacher non scrive.

| Metodo | Route | Permessi | Risultato |
|---|---|---|---|
| GET | formative-plans | A,S | pagina di FormativePlanInfo |
| POST | formative-plans | A | FormativeReceipt |
| GET | formative-plans/{id} | A,S | FormativePlanInfo |
| PUT | formative-plans/{id} | A | rename/descrizione, Receipt |
| GET | formative-plans/{id}/versions | A,S | pagina di snapshot ordinati |
| POST | formative-plans/{id}/versions | A | nuova Draft, Receipt |
| GET | formative-plan-versions/{id} | A,S | snapshot con intera gerarchia |
| PUT | formative-plan-versions/{id} | A | sostituzione completa Draft, Receipt |
| POST | formative-plan-versions/{id}/clone | A | copia Published in nuova Draft |
| POST | formative-plan-versions/{id}/validate | A,S | summary e issue |
| GET | formative-plan-versions/{id}/hour-summary | A,S | summary e issue |
| POST | formative-plan-versions/{id}/publish | A | pubblicazione immutabile |
| POST | course-editions/{id}/formative-plan | A | primo binding, Receipt |
| GET | course-editions/{id}/formative-plan | A,S,T,U | snapshot Published autorizzato |
| GET | course-editions/{id}/formative-plan/modules/{moduleId} | A,S,T,U | modulo appartenente al binding |
| GET | formative-plans/{id}/history | A | pagina audit |

Liste con `page=1&size=20`, massimo 100, array senza total count; storia ordinata
OccurredAtUtc/EventId, versioni per VersionNumber/Id. Body massimo 2 MiB.
Snapshot contiene id, institutionId, studyPathId, planId, versionNumber, name,
status (`Draft`/`Published`), revision, theoryPlannedMinutes, internshipPlannedMinutes,
units, createdAtUtc e publishedAtUtc. UF include plannedMinutes derivato dai moduli.
Le viste personali non includono audit, motivazioni o identificativi degli attori.

## Contratti di scrittura

Tutti i DTO rifiutano campi sconosciuti, anche annidati. InstitutionId, parent/VersionId,
stato, pubblicazione, attori, audit e revisione corrente non sono assegnabili dal client.
Solo StudyPathId nella creazione del piano; rimane immutabile. GUID per identità,
minuti interi e timestamp tecnici UTC. Nessuna conversione ore floating-point.

Creazione piano:

```json
{
  "eventId": "6a690000-0000-4000-8000-000000000001",
  "studyPathId": "6a690000-0000-4000-8000-000000000002",
  "code": "CODING",
  "name": "Piano Coding",
  "description": null,
  "reason": "Definizione amministrativa iniziale"
}
```

Creazione versione: `eventId, name, theoryPlannedMinutes, internshipPlannedMinutes, reason`.
I budget sono espliciti; non c'è default normativo. Numerazione assegnata dal server
sotto lock. Rename piano: `eventId, expectedRevision, name, description, reason`.

PUT Draft è l'operazione atomica per aggiungere/modificare/rimuovere/riordinare UF,
moduli e argomenti. Inviare l'intera struttura voluta; l'omissione rimuove elementi Draft.
`id:null` crea, `id` letto dal dettaglio mantiene l'identità sotto lo stesso padre.
Non inviare ID generati dal client. Per spostare un elemento sotto un nuovo padre,
rimuoverlo e ricrearlo con ID null; il vecchio ID non viene riassegnato.

Esempio **sintetico**, non curriculum produttivo:

```json
{
  "eventId": "6a690000-0000-4000-8000-000000000003",
  "expectedRevision": 0,
  "name": "Bozza sintetica",
  "theoryPlannedMinutes": 72000,
  "internshipPlannedMinutes": 48000,
  "units": [{
    "id": null, "code": "UF1", "name": "Unità sintetica", "description": null,
    "displayOrder": 0,
    "modules": [{
      "id": null, "code": "M1", "name": "Modulo sintetico", "description": null,
      "plannedMinutes": 75, "displayOrder": 0, "subjectId": null,
      "competencies": [{"id": null, "description": "Progettare un modello sintetico", "displayOrder": 0}],
      "contents": [{"id": null, "description": "Argomento sintetico", "displayOrder": 0}]
    }]
  }],
  "reason": "Costruzione della struttura Draft"
}
```

Questa bozza si salva ma non è pubblicabile: mancano Subject e 71925 minuti teorici.
La validazione restituisce `theoryPlannedMinutes, internshipPlannedMinutes,
totalPlannedMinutes, assignedMinutes, missingMinutes, excessMinutes, issues, canPublish`.
Ogni issue ha code/resourceId; esempio `theory.total-mismatch` oppure
`subject.required-or-unavailable`. Il server non corregge il budget.

Publish e clone: `eventId, expectedRevision, reason`. Clone richiede Published;
ExpectedRevision è quella della sorgente, la nuova Draft parte da zero. Bind:
`eventId, versionId, reason`; solo Published dello stesso StudyPath e tenant.
Binding preesistente non sostituibile; nessun endpoint di rettifica in questa fase.
Le edizioni legacy prive di piano restano valide, GET binding restituisce 404.

Receipt: `eventId, planId, versionId, revision, courseEditionId` (ultimi riferimenti
nullable secondo l'operazione). Non è il dettaglio corrente: rileggere dopo mutazione.
History include eventId, versionId/courseEditionId, operation, attori, revisioni,
beforeJson, afterJson, reason e occurredAtUtc. AfterJson conserva Receipt e State.

## Concorrenza ed errori

Generare EventId una sola volta per intenzione; riutilizzare payload/attore/ID al retry
dopo perdita di risposta. Replay restituisce ricevuta originale senza nuove scritture,
anche se la revisione corrente è avanzata. Cambiare il payload usando lo stesso EventId
produce 409. Nuovo intento deliberato richiede nuovo UUID. ExpectedRevision Int64
obbligatoria per update/publish/clone: preservarne la precisione nei client JavaScript.
Creazioni non aggiornano una risorsa preesistente; binding richiede assenza persistente.

400 contratto/testi/valori/Subject non disponibile nella struttura; 401 token assente,
Selection o tipo non ammesso; 403 ruolo/membership/generation/rollout non autorizzato;
404 risorsa assente o fuori tenant indistinguibili, binding mancante e viste personali
non autorizzate; 409 revision/stato/codice/ordinamento SQL/riuso EventId/binding già
presente/pubblicazione incompleta; 413 body troppo grande. Nessun dettaglio SQL.
Le issue dettagliate si ottengono con validate/hour-summary prima di publish;
il 409 di pubblicazione incompleta include anche `hourSummary` con deficit/eccedenze.

## React Native e futuro calendario

Caricare il dettaglio, conservare ID e revisione, modificare una copia della struttura,
inviare la PUT completa con EventId stabile. Presentare deficit/eccedenze e issue prima
di pubblicare; non correggere silenziosamente ore o Subject. Dopo 409 rileggere e
riconciliare con l'utente; non cambiare EventId per nascondere una risposta persa.

Student/Teacher devono usare l'edizione autorizzata; non il catalogo amministrativo.
Per il calendario futuro scegliere modulo per ID sotto il binding dell'edizione:
nome e Subject non identificano un modulo univoco. Subject guida l'integrazione con
ClassSubject senza crearne duplicati. Exam/Grade restano nel modello esistente.
Non esistono ora frontend, import calendario, lezioni, ore effettive o tirocinio operativo.
