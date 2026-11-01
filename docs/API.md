# Riferimento endpoint

**197 endpoint**: 195 nei controller (`src/Api/ElectronicRegisterAPI/Controllers/*.cs`) e 2 health check (`Operations/OperationalPipeline.cs`).
I tipi citati sono definiti in [MODELS.md](MODELS.md). Formato errori, paginazione e concorrenza: [CONVENTIONS.md](CONVENTIONS.md). Token e ruoli: [AUTH.md](AUTH.md).

## Come leggere questo documento

- **Ruoli**: ruoli istituzionali ammessi dall'endpoint. Il servizio applica poi regole di proprietà: un docente lavora solo sulle proprie classi-materie, uno studente solo sui propri dati.
- **Token**: `nessuno`, `ticket` (selection ticket) o `access` (access token istituzionale).
- **Flag**: feature flag necessari ([CONVENTIONS §8](CONVENTIONS.md#8-feature-flag-e-moduli-disattivati)). Tutti gli endpoint `/api/institution/*` richiedono `SchoolApi`, tranne admin membri, audit e inviti.
- **Errori comuni non ripetuti** per ogni endpoint autenticato:
  - **401**: token assente, scaduto o del tipo sbagliato.
  - **403**: ruolo non ammesso, flag spento, membership cambiata, profilo non collegato.
  - **429**: solo per i `POST`.
- **Esempi `curl`**: usano le variabili `API=http://127.0.0.1:5257`, `TICKET=<selection ticket>`, `TOKEN=<access token>`. Gli ID sono segnaposto.

## Indice

1. [Health](#1-health)
2. [Identità e account](#2-identità-e-account)
3. [Autenticazione e selezione istituto](#3-autenticazione-e-selezione-istituto)
4. [Inviti](#4-inviti)
5. [Amministrazione membri](#5-amministrazione-membri)
6. [Audit membership](#6-audit-membership)
7. [Aree di studio](#7-aree-di-studio)
8. [Percorsi di studio](#8-percorsi-di-studio)
9. [Bienni](#9-bienni)
10. [Biennio ↔ Area](#10-biennio--area)
11. [Biennio-Area ↔ Percorso](#11-biennio-area--percorso)
12. [Sedi (campus)](#12-sedi-campus)
13. [Classi](#13-classi)
14. [Classi-materie](#14-classi-materie)
15. [Edizioni corso](#15-edizioni-corso)
16. [Iscrizioni](#16-iscrizioni)
17. [Studenti](#17-studenti)
18. [Trasferimento di classe](#18-trasferimento-di-classe)
19. [Docenti](#19-docenti)
20. [Materie](#20-materie)
21. [Voti](#21-voti)
22. [Esami](#22-esami)
23. [Consegne (submissions)](#23-consegne-submissions)
24. [Calendario](#24-calendario)
25. [Piani formativi](#25-piani-formativi)
26. [Email istituzionali](#26-email-istituzionali)
27. [Import anagrafiche](#27-import-anagrafiche)
28. [Riepilogo e verifica copertura](#28-riepilogo-e-verifica-copertura)

---

## 1. Health

Fonte: `Operations/OperationalPipeline.cs`.

### `GET /health/live`
Indica se il processo è attivo. **Token:** nessuno.
- **200** `{"status":"live"}`

```bash
curl -s $API/health/live
```

### `GET /health/ready`
Indica se il servizio è pronto: database raggiungibile e schema aggiornato. **Token:** nessuno.
- **200** `{"status":"ready"}` · **503** senza body.

```bash
curl -s -i $API/health/ready
```

---

## 2. Identità e account

Fonti: `IdentityRegistrationController.cs`, `IdentityAccountController.cs`, `Application/Managers/IdentityAccountManager.cs`.

### `POST /api/identity/accounts`
Registra una nuova identità (email + password). **Token:** nessuno.
- **Body** [`RegisterIdentityAccountDto`](MODELS.md#32-identità-e-autenticazione):
  - `email`: obbligatorio, email valida, ≤ 255 caratteri, senza spazi ai bordi.
  - `password`: 8–72 caratteri, almeno un carattere tra `!@#$%^&*()_-+=<>?/[]{}`, ≤ 72 byte UTF-8.
  - Nessun altro campo ammesso.
- **201** → body: GUID dell'utente
  ```json
  "6b0f3c2e-8a71-4f0e-9d3b-2a1c4e5f6a7b"
  ```
- **Errori:**
  - **400**: email o password non valide, campi extra.
  - **409** `Operazione non consentita`: email già registrata.

```bash
curl -s -X POST $API/api/identity/accounts -H "Content-Type: application/json" \
  -d '{"email":"mario.rossi@example.org","password":"<PASSWORD>"}'
```

### `GET /api/identity/account`
Profilo dell'account corrente. **Token:** ticket.
- **200** [`IdentityAccountDto`](MODELS.md#32-identità-e-autenticazione)
  ```json
  { "userId": "6b0f3c2e-8a71-4f0e-9d3b-2a1c4e5f6a7b", "email": "mario.rossi@example.org", "displayName": "Mario Rossi", "hasLocalPassword": true }
  ```
- **Errori:** **403**: account non trovato.

```bash
curl -s $API/api/identity/account -H "Authorization: Bearer $TICKET"
```

### `PUT /api/identity/account`
Aggiorna il nome visualizzato. **Token:** ticket.
- **Body** `UpdateIdentityAccountDto`: `displayName` 1–160 caratteri dopo il trim, senza caratteri di controllo.
- **204**
- **Errori:**
  - **400**: valore non valido.
  - **401/403**: sessione revocata (token emesso prima di un cambio password).

```bash
curl -s -X PUT $API/api/identity/account -H "Authorization: Bearer $TICKET" -H "Content-Type: application/json" \
  -d '{"displayName":"Mario Rossi"}'
```

### `PUT /api/identity/account/password`
Cambia la password e **revoca tutti i token** dell'utente. **Token:** ticket.
- **Body** `ChangeIdentityPasswordDto`:
  - `currentPassword`: obbligatorio, ≤ 1024 caratteri.
  - `newPassword`: stesse regole della registrazione, diversa dalla precedente.
- **204**
- **Errori:**
  - **400**: nuova password non valida o uguale alla precedente.
  - **403**: password attuale errata, o account senza password locale (solo Microsoft).
  - **429**: tentativo concorrente, con `Retry-After: 60`.

```bash
curl -s -X PUT $API/api/identity/account/password -H "Authorization: Bearer $TICKET" -H "Content-Type: application/json" \
  -d '{"currentPassword":"<ATTUALE>","newPassword":"<NUOVA>"}'
```

---

## 3. Autenticazione e selezione istituto

Fonti: `InstitutionIdentityController.cs`, `Application/Managers/InstitutionAuthManager.cs`. Flusso completo in [AUTH.md](AUTH.md#2-flusso-completo).

### `POST /api/Auth/identity/login`
Login con email e password. **Token:** nessuno.
- **Body** `LoginDto`: `email`, `password` (entrambi obbligatori).
- **200** [`IdentityTicketDto`](MODELS.md#32-identità-e-autenticazione)
  ```json
  { "ticket": "eyJhbGciOiJIUzI1NiIsInR5cCI6ImVyLXNlbGVjdGlvbitqd3QifQ...", "expiresInSeconds": 300 }
  ```
- **Errori:** **401** `Credenziali non valide`.

```bash
curl -s -X POST $API/api/Auth/identity/login -H "Content-Type: application/json" \
  -d '{"email":"mario.rossi@example.org","password":"<PASSWORD>"}'
```

### `POST /api/Auth/identity/microsoft-login`
Login con un access token Microsoft Entra ID. **Token:** nessuno.
- **Body** `MicrosoftLoginDto`: `accessToken`.
- **200** `IdentityTicketDto`.
- **Errori:** **401** `Identita non autorizzata`: token non valido o identità Microsoft non collegata a un utente.

```bash
curl -s -X POST $API/api/Auth/identity/microsoft-login -H "Content-Type: application/json" \
  -d '{"accessToken":"<MS_ACCESS_TOKEN>"}'
```

### `GET /api/Auth/institutions`
Istituti in cui l'utente ha una membership attiva. **Token:** ticket.
- **200** `InstitutionChoiceDto[]`
  ```json
  [ { "institutionId": "8f2c1a6e-3b0d-4c55-9a1e-2f4b6c7d8e90", "membershipId": "1d3e5f70-9a2b-4c4d-8e6f-0a1b2c3d4e5f", "name": "ITS Esempio", "roles": ["Staff","Teacher"] } ]
  ```

```bash
curl -s $API/api/Auth/institutions -H "Authorization: Bearer $TICKET"
```

### `POST /api/Auth/select-institution`
Emette l'access token per un istituto. **Token:** ticket.
- **Body** `SelectInstitutionDto`:
  - `institutionId`, `membershipId`: obbligatori.
  - `requestedRole`: facoltativo, ≤ 32 caratteri, deve essere un ruolo della membership.
- **200** [`InstitutionAccessDto`](MODELS.md#32-identità-e-autenticazione)
  ```json
  { "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6ImVyLWluc3RpdHV0aW9uK2p3dCJ9...", "expiresInSeconds": 900 }
  ```
- **Errori:** **403**: membership non valida o non attiva, ruolo richiesto non posseduto, sessione revocata.

```bash
curl -s -X POST $API/api/Auth/select-institution -H "Authorization: Bearer $TICKET" -H "Content-Type: application/json" \
  -d '{"institutionId":"<INSTITUTION_ID>","membershipId":"<MEMBERSHIP_ID>"}'
```

### `GET /api/Auth/context`
Contesto dell'access token corrente. **Token:** access.
- **200** [`TenantContextDto`](MODELS.md#32-identità-e-autenticazione)
  ```json
  { "userId": "6b0f3c2e-8a71-4f0e-9d3b-2a1c4e5f6a7b", "institutionId": "8f2c1a6e-3b0d-4c55-9a1e-2f4b6c7d8e90", "membershipId": "1d3e5f70-9a2b-4c4d-8e6f-0a1b2c3d4e5f", "roles": ["Staff","Teacher"] }
  ```

```bash
curl -s $API/api/Auth/context -H "Authorization: Bearer $TOKEN"
```

---

## 4. Inviti

Fonti: `InstitutionInvitationController.cs`, `InvitationAcceptanceController.cs`, `Infrastructure/Repositories/InstitutionOnboardingRepository.cs`. Body ≤ 16 KiB.

> ⚠️ Nella build attuale il canale di consegna degli inviti è **chiuso** (`ClosedInvitationChannel`). Invito, ri-emissione e accettazione rispondono **403** `"Onboarding non operativo."` finché non viene installato un canale. Solo la revoca funziona. Vedi [OPEN_QUESTIONS Q-01](OPEN_QUESTIONS.md#q-01).

### `POST /api/institution/admin/invitations`
Invita un utente esistente (per `userId`) a diventare membro con i ruoli indicati. **Token:** access. **Ruoli:** InstitutionAdmin. **Flag:** `MembershipAdminApi`, `MembershipWriteApi`, `Onboarding`.
- **Body** [`InviteMemberRequest`](MODELS.md#33-membri-audit-inviti):
  - `eventId`: diventa anche **l'ID dell'invito**.
  - `recipientUserId`: diverso dall'utente corrente.
  - `roles`: 1–4 ruoli distinti, come stringhe esatte.
  - `reason`: ≤ 500 caratteri.
- **200** `InvitationResult`
  ```json
  { "eventId": "0c9d7e6f-5a4b-4c3d-9e2f-1a0b9c8d7e6f", "status": "accepted" }
  ```
  La risposta è **neutra**: è identica anche se l'utente non esiste o è già membro.
- **Errori:**
  - **400**: dati non validi.
  - **403**: onboarding non operativo.
  - **409**: conflitto di concorrenza.

```bash
curl -s -X POST $API/api/institution/admin/invitations -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","recipientUserId":"<USER_ID>","roles":["Teacher"],"reason":"Nuovo docente"}'
```

### `POST /api/institution/admin/invitations/{id}/revoke`
Revoca un invito pendente. **Token:** access. **Ruoli:** InstitutionAdmin. **Flag:** `MembershipAdminApi`, `MembershipWriteApi`, `Onboarding`.
- **Path:** `id` (GUID dell'invito, cioè l'`eventId` usato per crearlo).
- **Body** `InvitationChangeRequest`: `eventId`, `reason` (≤ 500).
- **200** `InvitationResult`.
- **Errori:** **400**, **404**, **409**.

```bash
curl -s -X POST $API/api/institution/admin/invitations/<INVITE_ID>/revoke -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","reason":"Invito errato"}'
```

### `POST /api/institution/admin/invitations/{id}/reissue`
Rigenera e rispedisce la sfida dell'invito, che scade di nuovo dopo 15 minuti. **Token:** access. **Ruoli:** InstitutionAdmin. **Flag:** come sopra.
- **Body** `InvitationChangeRequest`.
- **200** `InvitationResult`.
- **Errori:** **403**: canale chiuso. Inoltre **400**, **404**, **409**.

```bash
curl -s -X POST $API/api/institution/admin/invitations/<INVITE_ID>/reissue -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","reason":"Sfida scaduta"}'
```

### `POST /api/Auth/invitations/{id}/accept`
L'utente invitato accetta l'invito e diventa membro. **Token:** ticket. **Flag:** `Onboarding`.
- **Path:** `id` (GUID dell'invito).
- **Body** `InvitationAcceptanceRequest`:
  - `institutionId`, `eventId`: obbligatori.
  - `challenge`: codice ricevuto tramite il canale di consegna. Massimo 5 tentativi; scade dopo 15 minuti.
- **200** `InvitationResult`
  ```json
  { "eventId": "…", "status": "consumed", "membershipId": "…" }
  ```
- **Errori:**
  - **401**: header non `Bearer`.
  - **403**: invito revocato, scaduto, tentativi esauriti o canale chiuso.
  - **409**.
- Dopo l'accettazione: richiamare `GET /api/Auth/institutions` e `POST /api/Auth/select-institution`.

```bash
curl -s -X POST $API/api/Auth/invitations/<INVITE_ID>/accept -H "Authorization: Bearer $TICKET" -H "Content-Type: application/json" \
  -d '{"institutionId":"<INSTITUTION_ID>","eventId":"<UUID>","challenge":"<CODICE>"}'
```

---

## 5. Amministrazione membri

Fonti: `InstitutionMembershipAdminController.cs`, `Application/Managers/InstitutionMembershipAdminManager.cs`, `Domain/Policies/MembershipMutationRules.cs`.

**Ruoli:** InstitutionAdmin. **Flag:**
- `MembershipAdminApi` per tutti gli endpoint;
- anche `MembershipWriteApi` per i `POST`.

Questa area **non** richiede `SchoolApi`.

### `GET /api/institution/admin/members`
Elenco paginato dei membri dell'istituto.
- **Query:**
  - `pageNumber` (1), `pageSize` (20, max 100).
  - `status`: `pending`, `active`, `suspended`, `left` (**minuscolo**).
  - `role`: `InstitutionAdmin`, `Staff`, `Teacher`, `Student` (**case esatto**).
  - `search`: 1–100 caratteri.
  - Un parametro presente ma vuoto dà 400.
- **200** `InstitutionMemberDto[]` (senza totale)
  ```json
  [ { "membershipId": "1d3e…", "userId": "6b0f…", "email": "mario.rossi@example.org", "status": "active", "roles": ["Teacher"], "joinedAt": "2026-09-01T07:30:00Z" } ]
  ```
- **Errori:** **400**: filtri non validi.

```bash
curl -s "$API/api/institution/admin/members?status=active&role=Teacher&pageSize=50" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/admin/members/{membershipId}`
Dettaglio di un membro.
- **200** `InstitutionMemberDto`.
- **Errori:** **404**.

```bash
curl -s $API/api/institution/admin/members/<MEMBERSHIP_ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/admin/members/{membershipId}/mutations`
Aggiunge o rimuove un ruolo, sospende o riattiva una membership. Body ≤ 16 KiB.
- **Body** [`MembershipMutationRequest`](MODELS.md#33-membri-audit-inviti):
  - `action`: `AddRole` | `RemoveRole` | `Suspend` | `Reactivate` (stringa esatta).
  - `role`: obbligatorio per `AddRole`/`RemoveRole`, **vietato** negli altri casi.
  - `expectedRevision`: la *security generation* corrente della membership. Il valore arriva dalla risposta della mutazione precedente o da `revisionAfter` dell'audit; vedi [OPEN_QUESTIONS Q-06](OPEN_QUESTIONS.md#q-06).
  - `reason`: 1–500 caratteri.
- **Transizioni ammesse:**
  - `AddRole` / `RemoveRole`: membership `active` o `suspended`. Si può aggiungere solo un ruolo mancante e rimuovere solo un ruolo presente.
  - `Suspend`: da `active`.
  - `Reactivate`: da `suspended`.
- **200** `MembershipMutationResponse`
  ```json
  { "eventId": "…", "revision": 4, "status": "active", "roles": ["Staff","Teacher"] }
  ```
- **Errori:**
  - **400**: richiesta non valida.
  - **409** `Conflitto sulla membership istituzionale`: transizione non ammessa, revisione obsoleta, rimozione dell'**ultimo admin attivo**, concorrenza.
- **Effetto collaterale:** i token della membership modificata smettono di funzionare (403) e l'utente deve riselezionare l'istituto.

```bash
curl -s -X POST $API/api/institution/admin/members/<MEMBERSHIP_ID>/mutations -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":3,"action":"AddRole","role":"Staff","reason":"Incarico segreteria"}'
```

### `GET /api/institution/admin/members/{membershipId}/profile-acquisitions`
Storico dei collegamenti di profili storici (studente/docente) alla membership.
- **Query:** `page` (1), `size` (20, max 100), `status` (`linked` | `requires-review`).
- **200** `ProfileAcquisitionResult[]`
  ```json
  [ { "eventId": "…", "membershipId": "…", "profileId": "…", "kind": "Teacher", "status": "linked" } ]
  ```
- **Errori:** **400**, **404**: membership inesistente.

```bash
curl -s "$API/api/institution/admin/members/<MEMBERSHIP_ID>/profile-acquisitions?status=linked" -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/admin/members/{membershipId}/profile-acquisitions`
Collega un profilo studente o docente storico alla membership.
- **Body** `AcquireHistoricalProfile`:
  - `eventId`, `profileId`: GUID non vuoti.
  - `expectedRevision`: obbligatorio.
  - `kind`: `Student` | `Teacher`.
  - `reason`: 1–1000 caratteri.
- **Requisiti:** la membership deve essere `active` e avere il ruolo corrispondente a `kind`.
- **200** `ProfileAcquisitionResult`: `status` è `linked` se il profilo corrisponde all'utente, altrimenti `requires-review`.
- **Errori:** **400**, **404**, **409**.

```bash
curl -s -X POST $API/api/institution/admin/members/<MEMBERSHIP_ID>/profile-acquisitions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":3,"profileId":"<TEACHER_ID>","kind":"Teacher","reason":"Migrazione profilo"}'
```

---

## 6. Audit membership

Fonte: `InstitutionMembershipAuditController.cs`, `Application/Managers/MembershipAuditManager.cs`. **Ruoli:** InstitutionAdmin. **Flag:** `MembershipAdminApi`.

### `GET /api/institution/admin/audit`
Elenco degli eventi di modifica delle membership.
- **Query:**
  - `page` (1), `size` (20, max 100).
  - `target`: GUID di una membership, non vuoto.
  - `action`: `AddRole` | `RemoveRole` | `Suspend` | `Reactivate`.
- **200** [`MembershipAuditEntry[]`](MODELS.md#33-membri-audit-inviti)
  ```json
  [ { "eventId": "…", "actorUserId": "…", "actorMembershipId": "…", "targetMembershipId": "…", "action": "AddRole", "role": "Staff", "previousStatus": "active", "newStatus": "active", "previousRoles": ["Teacher"], "newRoles": ["Staff","Teacher"], "reason": "Incarico segreteria", "revisionBefore": 3, "revisionAfter": 4, "occurredAtUtc": "2026-10-30T09:12:00Z" } ]
  ```
- **Errori:** **400**: filtri non validi.

```bash
curl -s "$API/api/institution/admin/audit?target=<MEMBERSHIP_ID>" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/admin/audit/{eventId}`
Dettaglio di un evento.
- **200** `MembershipAuditEntry`.
- **Errori:** **400**: GUID vuoto. **404**.

```bash
curl -s $API/api/institution/admin/audit/<EVENT_ID> -H "Authorization: Bearer $TOKEN"
```


---

## Schema comune del catalogo (sezioni 7–13)

Le risorse di catalogo seguono lo stesso schema (`InstitutionStudyAreaController.cs`, `InstitutionStudyPathController.cs`, `InstitutionBienniumController.cs`, `InstitutionBiennium*Controller.cs`, `InstitutionCampusController.cs`, `InstitutionClassController.cs`, `Application/Managers/InstitutionAcademicCatalogManager.cs`).

**Ruoli:** InstitutionAdmin. **Flag:** `SchoolApi` (le sedi richiedono anche `CourseApi`).

- `GET` elenco: accetta solo `pageNumber`/`pageSize` più i filtri documentati. Qualunque altro parametro → **400** `Filtro non supportato.`.
- `GET /{id}`: header `ETag: "<revision>"`; **404** se non esiste.
- `POST`: **201**, body = GUID creato, header `Location`.
- `PUT /{id}`: header **`If-Match: "<revision>"`** obbligatorio.
  - **428** se manca.
  - **400** se malformato.
  - **409** `Operazione non consentita` se la revisione è obsoleta (`"Risorsa modificata. Ricaricare il dettaglio."`).
  - Successo: **204**.
- `DELETE /{id}`: **204**.
  - **404** se non esiste.
  - **409** se la risorsa ha dipendenze (altre entità la usano).

---

## 7. Aree di studio

### `GET /api/institution/study-areas`
Elenco paginato delle aree di studio.
- **Query:** `pageNumber` (1), `pageSize` (20, 1–100).
- **200** `InstitutionAcademicPage<InstitutionStudyAreaDto>`
  ```json
  { "items": [ { "id": "a1b2…", "name": "Tecnologie informatiche", "description": "Area ICT", "revision": 0 } ], "totalCount": 1, "pageNumber": 1, "pageSize": 20 }
  ```

```bash
curl -s "$API/api/institution/study-areas?pageNumber=1&pageSize=20" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/study-areas/{id}`
- **200** `InstitutionStudyAreaDto` + header `ETag`. **404**.

```bash
curl -s -i $API/api/institution/study-areas/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/study-areas`
- **Body** `CreateInstitutionStudyAreaDto`:
  - `name`: 1–100 caratteri dopo il trim.
  - `description`: **obbligatoria**, ≤ 65535 byte.
- **201** GUID.
- **Errori:** **400**, **409**: duplicato.

```bash
curl -s -X POST $API/api/institution/study-areas -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Tecnologie informatiche","description":"Area ICT"}'
```

### `PUT /api/institution/study-areas/{id}`
- **Header:** `If-Match: "<revision>"`.
- **Body** `UpdateInstitutionStudyAreaDto`: come la creazione.
- **204**.
- **Errori:** **400**, **404**, **409**, **428**.

```bash
curl -s -X PUT $API/api/institution/study-areas/<ID> -H "Authorization: Bearer $TOKEN" -H 'If-Match: "0"' -H "Content-Type: application/json" \
  -d '{"name":"ICT","description":"Area ICT aggiornata"}'
```

### `DELETE /api/institution/study-areas/{id}`
- **204**. **Errori:** **404**, **409**: usata da un biennio-area.

```bash
curl -s -X DELETE $API/api/institution/study-areas/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 8. Percorsi di studio

### `GET /api/institution/study-paths`
- **Query:** `pageNumber`, `pageSize`.
- **200** `InstitutionAcademicPage<InstitutionStudyPathDto>`
  ```json
  { "items": [ { "id": "c3d4…", "name": "Cloud Developer", "description": "Percorso biennale", "revision": 2 } ], "totalCount": 1, "pageNumber": 1, "pageSize": 20 }
  ```

```bash
curl -s $API/api/institution/study-paths -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/study-paths/{id}`
- **200** `InstitutionStudyPathDto` + `ETag`. **404**.

```bash
curl -s $API/api/institution/study-paths/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/study-paths`
- **Body** `CreateInstitutionStudyPathDto`:
  - `name`: 1–100 caratteri.
  - `description`: facoltativa, ≤ 65535 byte.
- **201** GUID.
- **Errori:** **400**, **409**.

```bash
curl -s -X POST $API/api/institution/study-paths -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Cloud Developer","description":null}'
```

### `PUT /api/institution/study-paths/{id}`
- **Header** `If-Match`. **Body** `UpdateInstitutionStudyPathDto`.
- **204**.
- **Errori:** **400**, **404**, **409**, **428**.

```bash
curl -s -X PUT $API/api/institution/study-paths/<ID> -H "Authorization: Bearer $TOKEN" -H 'If-Match: "2"' -H "Content-Type: application/json" \
  -d '{"name":"Cloud Developer","description":"Percorso biennale"}'
```

### `DELETE /api/institution/study-paths/{id}`
- **204**. **Errori:** **404**, **409**.

```bash
curl -s -X DELETE $API/api/institution/study-paths/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 9. Bienni

### `GET /api/institution/biennia`
- **200** `InstitutionAcademicPage<InstitutionBienniumDto>`
  ```json
  { "items": [ { "id": "e5f6…", "startYear": "2025-09-01", "endYear": "2027-07-31", "revision": 0 } ], "totalCount": 1, "pageNumber": 1, "pageSize": 20 }
  ```

```bash
curl -s $API/api/institution/biennia -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/biennia/{id}`
- **200** `InstitutionBienniumDto` + `ETag`. **404**.

```bash
curl -s $API/api/institution/biennia/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/biennia`
- **Body** `CreateInstitutionBienniumDto`:
  - `startYear`, `endYear`: **date** `YYYY-MM-DD`, anno ≥ 1000, `endYear ≥ startYear`.
- **201** GUID.
- **Errori:** **400**, **409**.

```bash
curl -s -X POST $API/api/institution/biennia -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"startYear":"2025-09-01","endYear":"2027-07-31"}'
```

### `PUT /api/institution/biennia/{id}`
- **Header** `If-Match`. **Body** `UpdateInstitutionBienniumDto`.
- **204**.
- **Errori:** **400**, **404**, **409**, **428**.

```bash
curl -s -X PUT $API/api/institution/biennia/<ID> -H "Authorization: Bearer $TOKEN" -H 'If-Match: "0"' -H "Content-Type: application/json" \
  -d '{"startYear":"2025-09-01","endYear":"2027-06-30"}'
```

### `DELETE /api/institution/biennia/{id}`
- **204**. **Errori:** **404**, **409**.

```bash
curl -s -X DELETE $API/api/institution/biennia/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 10. Biennio ↔ Area

Associazione tra un biennio e un'area di studio. **Non è modificabile**: per cambiarla si elimina e si ricrea.

### `GET /api/institution/biennium-study-areas`
- **200** `InstitutionAcademicPage<InstitutionBienniumStudyAreaDto>`
  ```json
  { "items": [ { "id": "f7a8…", "bienniumId": "e5f6…", "studyAreaId": "a1b2…", "revision": 0 } ], "totalCount": 1, "pageNumber": 1, "pageSize": 20 }
  ```

```bash
curl -s $API/api/institution/biennium-study-areas -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/biennium-study-areas/{id}`
- **200** `InstitutionBienniumStudyAreaDto` + `ETag`. **404**.

```bash
curl -s $API/api/institution/biennium-study-areas/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/biennium-study-areas`
- **Body** `CreateInstitutionBienniumStudyAreaDto`: `bienniumId`, `studyAreaId` (GUID non vuoti).
- **201** GUID.
- **Errori:** **400**, **404**, **409**: duplicato.

```bash
curl -s -X POST $API/api/institution/biennium-study-areas -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"bienniumId":"<BIENNIUM_ID>","studyAreaId":"<STUDY_AREA_ID>"}'
```

### `DELETE /api/institution/biennium-study-areas/{id}`
- **204**. **Errori:** **404**, **409**.

```bash
curl -s -X DELETE $API/api/institution/biennium-study-areas/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 11. Biennio-Area ↔ Percorso

Associa un percorso a un biennio-area. Le classi appartengono a un `bienniumStudyPathId`.

### `GET /api/institution/biennium-study-paths`
- **200** `InstitutionAcademicPage<InstitutionBienniumStudyPathDto>`
  ```json
  { "items": [ { "id": "b9c0…", "bienniumStudyAreaId": "f7a8…", "studyPathId": "c3d4…", "revision": 0 } ], "totalCount": 1, "pageNumber": 1, "pageSize": 20 }
  ```

```bash
curl -s $API/api/institution/biennium-study-paths -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/biennium-study-paths/{id}`
- **200** `InstitutionBienniumStudyPathDto` + `ETag`. **404**.

```bash
curl -s $API/api/institution/biennium-study-paths/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/biennium-study-paths`
- **Body** `CreateInstitutionBienniumStudyPathDto`: `bienniumStudyAreaId`, `studyPathId`.
- **201** GUID.
- **Errori:** **400**, **404**, **409**.

```bash
curl -s -X POST $API/api/institution/biennium-study-paths -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"bienniumStudyAreaId":"<BSA_ID>","studyPathId":"<STUDY_PATH_ID>"}'
```

### `DELETE /api/institution/biennium-study-paths/{id}`
- **204**. **Errori:** **404**, **409**: usato da classi.

```bash
curl -s -X DELETE $API/api/institution/biennium-study-paths/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 12. Sedi (campus)

Fonte: `InstitutionCampusController.cs`, `Domain/Policies/InstitutionCampusRules.cs`. **Flag:** `SchoolApi` + `CourseApi`.

### `GET /api/institution/campuses`
- **Query:** `pageNumber`, `pageSize`, `isActive` (`true`/`false`, facoltativo).
- **200** `InstitutionCampusDto[]` (**array**) + header **`X-Total-Count`**
  ```json
  [ { "id": "d1e2…", "campusId": "d1e2…", "name": "Sede centrale", "address": "Via Roma 1", "city": "Torino", "province": "TO", "isActive": true, "revision": 0 } ]
  ```

```bash
curl -s -i "$API/api/institution/campuses?isActive=true" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/campuses/{id}`
- **200** `InstitutionCampusDto` + `ETag`. **404**.

```bash
curl -s $API/api/institution/campuses/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/campuses`
- **Body** `CreateInstitutionCampusDto`:
  - `name`: 1–200 caratteri.
  - `address`: ≤ 300.
  - `city`, `province`: ≤ 100.
  - `isActive`: default `true`.
  - Le stringhe vuote diventano `null`.
- **201** GUID.
- **Errori:** **400**, **409**.

```bash
curl -s -X POST $API/api/institution/campuses -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Sede centrale","address":"Via Roma 1","city":"Torino","province":"TO"}'
```

### `PUT /api/institution/campuses/{id}`
- **Header** `If-Match`.
- **Body** `UpdateInstitutionCampusDto`: come la creazione, ma `isActive` è **obbligatorio**.
- **204**.
- **Errori:** **400**, **404**, **409**, **428**.

```bash
curl -s -X PUT $API/api/institution/campuses/<ID> -H "Authorization: Bearer $TOKEN" -H 'If-Match: "0"' -H "Content-Type: application/json" \
  -d '{"name":"Sede centrale","address":"Via Roma 1","city":"Torino","province":"TO","isActive":false}'
```

### `DELETE /api/institution/campuses/{id}`
- **204**. **Errori:** **404**, **409**: collegata a edizioni.

```bash
curl -s -X DELETE $API/api/institution/campuses/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 13. Classi

Fonte: `InstitutionClassController.cs`, `Domain/Policies/InstitutionClassRules.cs`, `Infrastructure/Repositories/InstitutionClassRepository.cs`.

### `GET /api/institution/classes`
- **Query:**
  - `pageNumber`, `pageSize`.
  - `bienniumStudyPathId`, `courseEditionId`: GUID facoltativi, non vuoti.
- **200** `InstitutionClassPageDto`
  ```json
  { "items": [ { "id": "1a2b…", "name": "CD25", "bienniumStudyPathId": "b9c0…", "courseEditionId": "7e8f…", "revision": 1 } ], "totalCount": 1, "pageNumber": 1, "pageSize": 20 }
  ```

```bash
curl -s "$API/api/institution/classes?courseEditionId=<EDITION_ID>" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/classes/{id}`
- **200** `InstitutionClassDto` + `ETag`. **404**.

```bash
curl -s $API/api/institution/classes/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/classes`
- **Body** `CreateInstitutionClassDto`:
  - `name`: 1–20 caratteri dopo il trim.
  - `bienniumStudyPathId`: non vuoto.
- **201** GUID.
- **Errori:** **400**, **409** `"Classe gia presente, modificata o utilizzata."`.

```bash
curl -s -X POST $API/api/institution/classes -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"CD25","bienniumStudyPathId":"<BSP_ID>"}'
```

### `PUT /api/institution/classes/{id}`
Rinomina la classe. Biennio-percorso ed edizione non si cambiano da qui: l'edizione si assegna con `POST /course-editions/{editionId}/classes/{classId}`.
- **Header** `If-Match`. **Body** `UpdateInstitutionClassDto`: `name` 1–20.
- **204**.
- **Errori:** **400**, **404**, **409**, **428**.

```bash
curl -s -X PUT $API/api/institution/classes/<ID> -H "Authorization: Bearer $TOKEN" -H 'If-Match: "1"' -H "Content-Type: application/json" \
  -d '{"name":"CD25-A"}'
```

### `DELETE /api/institution/classes/{id}`
- **204**.
- **Errori:** **404**, **409**: la classe è usata da studenti, materie, lezioni, ecc.

```bash
curl -s -X DELETE $API/api/institution/classes/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 14. Classi-materie

Assegnazione docente ↔ materia ↔ classe. Fonte: `InstitutionClassSubjectController.cs`, `Application/Managers/InstitutionClassSubjectManager.cs`. **Flag:** `SchoolApi` + `ExamApi`.

### `GET /api/institution/class-subjects`
Elenco delle assegnazioni. È la base per creare esami e voti.
- **Ruoli:** InstitutionAdmin, Teacher (il docente deve avere un profilo docente collegato).
- **Query:** `pageNumber`, `pageSize`, `classId`, `subjectId`, `teacherId` (GUID facoltativi, non vuoti). Nessun altro parametro.
- **200** `InstitutionClassSubjectDto[]` (**array**) + header **`X-Total-Count`**
  ```json
  [ { "id": "5c6d…", "classSubjectId": "5c6d…", "classId": "1a2b…", "className": "CD25", "subjectId": "9e0f…", "subjectName": "Matematica", "teacherId": "3a4b…", "teacherFirstName": "Laura", "teacherLastName": "Bianchi", "revision": 0 } ]
  ```
- Che il docente veda solo le proprie assegnazioni dipende dal repository; non è stato verificato ([OPEN_QUESTIONS Q-18](OPEN_QUESTIONS.md#q-18)).

```bash
curl -s "$API/api/institution/class-subjects?classId=<CLASS_ID>" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/class-subjects/{id}`
- **Ruoli:** InstitutionAdmin.
- **200** `InstitutionClassSubjectDto` + `ETag`. **404**.

```bash
curl -s $API/api/institution/class-subjects/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/class-subjects`
- **Ruoli:** InstitutionAdmin.
- **Body** `CreateInstitutionClassSubjectDto`: `classId`, `subjectId`, `teacherId` (non vuoti).
- **201** GUID.
- **Errori:** **400**, **404**, **409**: assegnazione duplicata.

```bash
curl -s -X POST $API/api/institution/class-subjects -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"classId":"<CLASS_ID>","subjectId":"<SUBJECT_ID>","teacherId":"<TEACHER_ID>"}'
```

### `PUT /api/institution/class-subjects/{id}`
Cambia il docente dell'assegnazione.
- **Ruoli:** InstitutionAdmin.
- **Header** `If-Match`. **Body** `UpdateInstitutionClassSubjectDto`: `teacherId`.
- **204**.
- **Errori:** **400**, **404**, **409**, **428**.

```bash
curl -s -X PUT $API/api/institution/class-subjects/<ID> -H "Authorization: Bearer $TOKEN" -H 'If-Match: "0"' -H "Content-Type: application/json" \
  -d '{"teacherId":"<TEACHER_ID>"}'
```

### `DELETE /api/institution/class-subjects/{id}`
- **Ruoli:** InstitutionAdmin.
- **204**. **Errori:** **404**, **409**: esistono esami o voti collegati.

```bash
curl -s -X DELETE $API/api/institution/class-subjects/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 15. Edizioni corso

Fonte: `InstitutionCourseEditionController.cs`, `Application/Managers/InstitutionCourseManager.cs`, `Domain/Policies/CourseStructureValidation.cs`.

**Ruoli:** InstitutionAdmin. **Flag:** `SchoolApi` + `CourseApi`.

Conflitti di database (duplicati, vincoli) e versione obsoleta → **409** `Conflitto sulla struttura formativa`. Paginazione: `pageNumber` 1–1.000.000, `pageSize` 1–100.

### `GET /api/institution/course-editions`
- **Query:** `studyPathId` (facoltativo), `pageNumber`, `pageSize`.
- **200** `CourseEditionDto[]`
  ```json
  [ { "id": "7e8f…", "institutionId": "8f2c…", "studyPathId": "c3d4…", "code": "CD-2025", "name": "Cloud Developer 2025-27", "startDate": "2025-10-06", "endDate": "2027-07-15", "status": "active", "createdAt": "2025-06-01T10:00:00Z", "updatedAt": "2025-06-01T10:00:00Z", "version": "3F2A…(64 caratteri esadecimali)" } ]
  ```

```bash
curl -s "$API/api/institution/course-editions?studyPathId=<STUDY_PATH_ID>" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/course-editions/{id}`
- **200** `CourseEditionDto`. **404**.

```bash
curl -s $API/api/institution/course-editions/<ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/course-editions/by-code/{code}`
Cerca un'edizione per codice esatto.
- **200** `CourseEditionDto`. **404**.

```bash
curl -s $API/api/institution/course-editions/by-code/CD-2025 -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/course-editions`
- **Body** `CreateCourseEditionDto`:
  - `studyPathId`: obbligatorio.
  - `code`: ≤ 64 caratteri.
  - `name`: ≤ 200 caratteri.
  - `startDate`, `endDate`: facoltative, con `endDate ≥ startDate`.
  - `status`: `planned` | `active` | `completed` | `cancelled`.
  - Non sono ammessi campi extra, compreso `institutionId`.
- **201** GUID.
- **Errori:** **400**, **409**: codice duplicato o percorso inesistente.

```bash
curl -s -X POST $API/api/institution/course-editions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"studyPathId":"<STUDY_PATH_ID>","code":"CD-2025","name":"Cloud Developer 2025-27","startDate":"2025-10-06","endDate":"2027-07-15","status":"planned"}'
```

### `PUT /api/institution/course-editions/{id}`
Aggiorna i dati descrittivi dell'edizione. **Lo stato non è modificabile da qui.**
- **Body** `UpdateCourseEditionDto`:
  - `code`, `name`, `startDate`, `endDate`.
  - `version`: valore ricevuto dal GET, esattamente 64 caratteri.
- **204**.
- **Errori:** **400**, **404**, **409**: versione cambiata o codice duplicato.

```bash
curl -s -X PUT $API/api/institution/course-editions/<ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"code":"CD-2025","name":"Cloud Developer 2025-2027","startDate":"2025-10-06","endDate":"2027-07-15","version":"<VERSION_64_HEX>"}'
```

### `GET /api/institution/course-editions/{id}/campuses`
Sedi collegate all'edizione.
- **Query:** `pageNumber`, `pageSize`.
- **200** `CampusDto[]`
  ```json
  [ { "campusId": "d1e2…", "name": "Sede centrale", "city": "Torino", "isActive": true } ]
  ```
- **Errori:** **404**: edizione inesistente.

```bash
curl -s $API/api/institution/course-editions/<ID>/campuses -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/course-editions/{id}/campuses`
Collega una sede all'edizione.
- **Body** `LinkEditionCampusDto`: `campusId`.
- **201** senza body; `Location` punta all'elenco delle sedi.
- **Errori:** **400**, **404**, **409**: già collegata.

```bash
curl -s -X POST $API/api/institution/course-editions/<ID>/campuses -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"campusId":"<CAMPUS_ID>"}'
```

### `GET /api/institution/course-editions/{id}/classes`
Classi assegnate all'edizione.
- **200** `EditionClassDto[]`
  ```json
  [ { "classId": "1a2b…", "name": "CD25", "bienniumStudyPathId": "b9c0…", "courseEditionId": "7e8f…" } ]
  ```
- **Errori:** **404**.

```bash
curl -s $API/api/institution/course-editions/<ID>/classes -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/course-editions/{editionId}/classes/{classId}`
Assegna una classe all'edizione. Nessun body.
- **204**.
- **Errori:** **404**, **409**: la classe è già assegnata a un'edizione.

```bash
curl -s -X POST $API/api/institution/course-editions/<EDITION_ID>/classes/<CLASS_ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/course-editions/{id}/enrollments`
Iscrizioni all'edizione.
- **200** `StudentEnrollmentDto[]` (vedi §16).
- **Errori:** **404**.

```bash
curl -s $API/api/institution/course-editions/<ID>/enrollments -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/course-editions/{id}/audit`
Eventi amministrativi dell'edizione: sedi, classi, iscrizioni.
- **200** `CourseAdministrationEventDto[]`
  ```json
  [ { "eventId": "…", "institutionId": "…", "actorUserId": "…", "actorMembershipId": "…", "courseEditionId": "7e8f…", "classId": "1a2b…", "action": "ClassAssigned", "afterJson": "{…}", "occurredAtUtc": "2026-09-01T08:00:00Z" } ]
  ```
  I valori possibili di `action` non sono enumerati nel codice esaminato.
- **Errori:** **404**.

```bash
curl -s $API/api/institution/course-editions/<ID>/audit -H "Authorization: Bearer $TOKEN"
```

---

## 16. Iscrizioni

Fonte: `InstitutionEnrollmentController.cs`, `Application/Managers/InstitutionCourseManager.cs`. **Flag:** `SchoolApi` + `CourseApi`.

### `GET /api/institution/enrollments`
Iscrizioni di uno studente. **Ruoli:** InstitutionAdmin.
- **Query:**
  - `studentId`: **obbligatorio di fatto**. Se manca vale il GUID vuoto e la risposta è 404 ([OPEN_QUESTIONS Q-07](OPEN_QUESTIONS.md#q-07)).
  - `pageNumber`, `pageSize`.
- **200** `StudentEnrollmentDto[]`
  ```json
  [ { "enrollmentId": "2b3c…", "courseEditionId": "7e8f…", "courseEditionCode": "CD-2025", "courseEditionName": "Cloud Developer 2025-27", "studentId": "4d5e…", "enrollmentDate": "2025-10-06", "status": "enrolled", "revision": 0 } ]
  ```
- **Errori:** **404**: studente inesistente.

```bash
curl -s "$API/api/institution/enrollments?studentId=<STUDENT_ID>" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/enrollments/{id}`
**Ruoli:** InstitutionAdmin.
- **200** `StudentEnrollmentDto`. **404**.

```bash
curl -s $API/api/institution/enrollments/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/enrollments`
Iscrive uno studente a un'edizione. **Ruoli:** InstitutionAdmin.
- **Body** `CreateStudentEnrollmentDto`:
  - `studentId`, `courseEditionId`.
  - `enrollmentDate`: anno ≥ 1000.
  - `completionDate`: facoltativa, ≥ `enrollmentDate`.
  - `status`: `enrolled` | `completed` | `withdrawn` | `cancelled`.
- **201** GUID.
- **Errori:** **400**, **409**: già iscritto o vincoli.

```bash
curl -s -X POST $API/api/institution/enrollments -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"studentId":"<STUDENT_ID>","courseEditionId":"<EDITION_ID>","enrollmentDate":"2025-10-06","completionDate":null,"status":"enrolled"}'
```

### `GET /api/institution/enrollments/me`
Iscrizioni dello studente corrente. **Ruoli:** Student (con profilo studente collegato).
- **Query:** `pageNumber`, `pageSize`.
- **200** `OwnEnrollmentDto[]`.

```bash
curl -s $API/api/institution/enrollments/me -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/enrollments/{id}/transition`
Cambia lo stato di un'iscrizione. **Ruoli:** InstitutionAdmin. Body ≤ 16 KiB.
- **Body** `TransitionEnrollmentDto`:
  - `eventId`: non vuoto.
  - `targetStatus`: `enrolled` | `completed` | `withdrawn` | `cancelled`.
  - `completionDate`: **obbligatoria**.
  - `reason`: 1–1000 caratteri.
  - `expectedRevision`: ≥ 0.
- **200** `EnrollmentMutationResult`
  ```json
  { "eventId": "…", "revision": 1, "status": "completed", "completionDate": "2027-07-15" }
  ```
- **Errori:** **400**, **404**, **409**: revisione obsoleta o transizione non ammessa.

```bash
curl -s -X POST $API/api/institution/enrollments/<ID>/transition -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","targetStatus":"completed","completionDate":"2027-07-15","reason":"Fine corso","expectedRevision":0}'
```

### `PUT /api/institution/enrollments/{id}/completion-date`
Corregge la data di completamento. **Ruoli:** InstitutionAdmin. Body ≤ 16 KiB.
- **Body** `CorrectEnrollmentCompletionDto`: `eventId`, `completionDate`, `reason` (1–1000), `expectedRevision`.
- **200** `EnrollmentMutationResult`.
- **Errori:** **400**, **404**, **409**.

```bash
curl -s -X PUT $API/api/institution/enrollments/<ID>/completion-date -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","completionDate":"2027-07-16","reason":"Correzione data","expectedRevision":1}'
```

### `GET /api/institution/enrollments/{id}/history`
Storico dell'iscrizione. **Ruoli:** InstitutionAdmin.
- **200** `CourseAdministrationEventDto[]`. **404**.

```bash
curl -s $API/api/institution/enrollments/<ID>/history -H "Authorization: Bearer $TOKEN"
```

---

## 17. Studenti

Fonte: `InstitutionStudentController.cs`, `Application/Managers/InstitutionStudentManager.cs`, `Infrastructure/Repositories/InstitutionStudentRepository.cs`. **Flag:** `SchoolApi`.

Per lo Staff i campi extra nel body sono rifiutati con 400 ([AUTH §4.3](AUTH.md#43-regola-speciale-per-staff-campi-sconosciuti)).

### `GET /api/institution/students`
Elenco degli studenti, **non paginato**. **Ruoli:** InstitutionAdmin, Staff, Teacher. Un docente deve avere il profilo collegato.
- **Query:** `classId` (facoltativo): solo gli studenti della classe.
- **200** `StudentDto[]`
  ```json
  [ { "id": "4d5e…", "firstName": "Giulia", "lastName": "Verdi" } ]
  ```

```bash
curl -s "$API/api/institution/students?classId=<CLASS_ID>" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/students/count`
**Ruoli:** InstitutionAdmin, Staff, Teacher.
- **200** numero intero, es. `128`.

```bash
curl -s $API/api/institution/students/count -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/students/{id}`
**Ruoli:** InstitutionAdmin, Staff, Teacher.
- **200** `StudentDto`. **404**.

```bash
curl -s $API/api/institution/students/<ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/students/bylastname/{lastName}`
Ricerca per cognome. **Ruoli:** InstitutionAdmin, Staff, Teacher.
- **200** `StudentDto[]`. Il tipo di corrispondenza (esatta o parziale) non è stato verificato.

```bash
curl -s $API/api/institution/students/bylastname/Verdi -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/students/me`
Profilo dello studente corrente. **Ruoli:** Student.
- **200** `StudentDto`.
- **Errori:** **403**: profilo non collegato. **404**.

```bash
curl -s $API/api/institution/students/me -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/students`
Crea uno studente. **Ruoli:** InstitutionAdmin, Staff (permesso `UserCreate`).
- **Query:**
  - `classId` (facoltativo): classe iniziale.
  - `membershipId` (facoltativo, **solo InstitutionAdmin**): collega subito lo studente a una membership.
- **Body** `CreateStudentDto`: `firstName`, `lastName`, 1–100 caratteri, non vuoti.
- **201** GUID.
- **Errori:**
  - **400**.
  - **403**: lo Staff ha passato `membershipId`.
  - **404**, **409**.

```bash
curl -s -X POST "$API/api/institution/students?classId=<CLASS_ID>" -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"firstName":"Giulia","lastName":"Verdi"}'
```

### `PUT /api/institution/students/{id}`
**Ruoli:** InstitutionAdmin, Staff (`UserUpdate`).
- **Body** `UpdateStudentDto`: `firstName`, `lastName`, nessun campo extra.
- **204**.
- **Errori:** **400**, **404**.

```bash
curl -s -X PUT $API/api/institution/students/<ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"firstName":"Giulia","lastName":"Verdi Rossi"}'
```

### `DELETE /api/institution/students/{id}`
**Ruoli:** InstitutionAdmin.
- **204**.
- **Errori:** **404**, **409**: `"Lo studente ha voti registrati."` o `"Studente non eliminabile."`.

```bash
curl -s -X DELETE $API/api/institution/students/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 18. Trasferimento di classe

Fonte: `InstitutionStudentTransferController.cs`, `Domain/Policies/StudentClassTransferPolicy.cs`.

**Ruoli:** InstitutionAdmin. **Flag:**
- `SchoolApi` per entrambi gli endpoint;
- anche `CourseApi` per `transfer-class`.

### `POST /api/institution/students/{id}/transfer-class`
Sposta lo studente in un'altra classe e aggiorna l'iscrizione al corso. Body ≤ 16 KiB, **nessun parametro di query ammesso**.
- **Body** `TransferStudentClassDto`:
  - `eventId`, `toClassId`.
  - `effectiveDate`: anno ≥ 1000.
  - `reason`: 1–1000 caratteri.
  - `expectedRevision`: valore `currentRevision` dello storico.
- **Regola:** il trasferimento è possibile solo entro `AcademicTransfers:MaxMonthsFromCourseStart` mesi (default 3) dalla data di inizio del corso, calcolata nel fuso `Europe/Rome`.
- **200** `StudentClassTransferResultDto`
  ```json
  { "eventId": "…", "studentId": "4d5e…", "fromClassId": "1a2b…", "toClassId": "6f7a…", "fromCourseEditionId": "7e8f…", "toCourseEditionId": "8a9b…", "effectiveDate": "2025-11-03", "appliedLimitMonths": 3, "revision": 1, "occurredAtUtc": "2025-11-03T09:00:00Z" }
  ```
- **Errori:**
  - **400**.
  - **403**: `CourseApi` spento.
  - **404**.
  - **409** `Trasferimento non consentito` con `detail` leggibile, ad esempio:
    - `"La finestra amministrativa per il trasferimento è scaduta."`
    - `"La data ufficiale di inizio corso non è configurata."`

```bash
curl -s -X POST $API/api/institution/students/<ID>/transfer-class -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","toClassId":"<CLASS_ID>","effectiveDate":"2025-11-03","reason":"Cambio indirizzo","expectedRevision":0}'
```

### `GET /api/institution/students/{id}/class-history`
Storico dei trasferimenti con la revisione corrente.
- **Query:** solo `pageNumber`, `pageSize`.
- **200** `StudentClassTransferPageDto`
  ```json
  { "items": [ { "receipt": { "eventId": "…", "studentId": "…", "fromClassId": "…", "toClassId": "…", "fromCourseEditionId": "…", "toCourseEditionId": "…", "effectiveDate": "2025-11-03", "appliedLimitMonths": 3, "revision": 1, "occurredAtUtc": "…" }, "fromClassName": "CD25", "toClassName": "WD25", "fromCourseName": "Cloud Developer", "toCourseName": "Web Developer", "sourceEnrollmentId": "…", "destinationEnrollmentId": "…", "reason": "Cambio indirizzo", "actorUserId": "…", "actorMembershipId": "…", "revisionBefore": 0, "sourceCourseStartDate": "2025-10-06", "destinationCourseStartDate": "2025-10-06" } ], "totalCount": 1, "pageNumber": 1, "pageSize": 20, "currentRevision": 1 }
  ```

```bash
curl -s $API/api/institution/students/<ID>/class-history -H "Authorization: Bearer $TOKEN"
```

---

## 19. Docenti

Fonte: `InstitutionTeacherController.cs`, `Application/Managers/InstitutionTeacherManager.cs`. **Flag:** `SchoolApi`. Per lo Staff i campi extra nel body sono rifiutati.

### `GET /api/institution/teachers`
Elenco dei docenti, **non paginato**. **Ruoli:** InstitutionAdmin, Staff, Teacher, Student.
- **200** `TeacherDto[]`
  ```json
  [ { "id": "3a4b…", "firstName": "Laura", "lastName": "Bianchi" } ]
  ```

```bash
curl -s $API/api/institution/teachers -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/teachers/count`
**Ruoli:** InstitutionAdmin, Staff, Teacher.
- **200** intero.

```bash
curl -s $API/api/institution/teachers/count -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/teachers/{id}`
**Ruoli:** InstitutionAdmin, Staff, Teacher, Student.
- **200** `TeacherDto`. **404**.

```bash
curl -s $API/api/institution/teachers/<ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/teachers/bylastname/{lastName}`
**Ruoli:** InstitutionAdmin, Staff, Teacher, Student.
- **200** `TeacherDto[]`.

```bash
curl -s $API/api/institution/teachers/bylastname/Bianchi -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/teachers/me`
**Ruoli:** Teacher.
- **200** `TeacherDto`.
- **Errori:** **403**: profilo non collegato. **404**.

```bash
curl -s $API/api/institution/teachers/me -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/teachers`
**Ruoli:** InstitutionAdmin, Staff (`UserCreate`).
- **Query:** `membershipId` (facoltativo, solo InstitutionAdmin).
- **Body** `CreateTeacherDto`: `firstName`, `lastName` (1–100).
- **201** GUID.
- **Errori:** **400**, **403**, **404**, **409**.

```bash
curl -s -X POST $API/api/institution/teachers -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"firstName":"Laura","lastName":"Bianchi"}'
```

### `PUT /api/institution/teachers/{id}`
**Ruoli:** InstitutionAdmin, Staff (`UserUpdate`).
- **Body** `UpdateTeacherDto`.
- **204**.
- **Errori:** **400**, **404**.

```bash
curl -s -X PUT $API/api/institution/teachers/<ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"firstName":"Laura","lastName":"Bianchi"}'
```

### `DELETE /api/institution/teachers/{id}`
**Ruoli:** InstitutionAdmin.
- **204**.
- **Errori:** **404**, **409** `"Il docente ha assegnazioni a classi e materie."`.

```bash
curl -s -X DELETE $API/api/institution/teachers/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 20. Materie

Fonte: `InstitutionSubjectController.cs`, `Application/Managers/InstitutionSubjectManager.cs`. **Flag:** `SchoolApi`.

### `GET /api/institution/subjects`
**Ruoli:** InstitutionAdmin, Teacher, Student (lo Staff è escluso, vedi [Q-08](OPEN_QUESTIONS.md#q-08)).
- **200** `SubjectDto[]`
  ```json
  [ { "id": "9e0f…", "name": "Matematica" } ]
  ```

```bash
curl -s $API/api/institution/subjects -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/subjects/count`
**Ruoli:** InstitutionAdmin, Teacher, Student.
- **200** intero.

```bash
curl -s $API/api/institution/subjects/count -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/subjects/{id}`
**Ruoli:** InstitutionAdmin, Teacher, Student.
- **200** `SubjectDto`. **404**.

```bash
curl -s $API/api/institution/subjects/<ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/subjects/byname/{name}`
**Ruoli:** InstitutionAdmin, Teacher, Student.
- **200** `SubjectDto` (un solo risultato). **404**.

```bash
curl -s $API/api/institution/subjects/byname/Matematica -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/subjects/byteacher/{teacherId}`
Materie insegnate da un docente. **Ruoli:** InstitutionAdmin, Teacher, Student.
- **200** `SubjectDto[]`.

```bash
curl -s $API/api/institution/subjects/byteacher/<TEACHER_ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/subjects`
**Ruoli:** InstitutionAdmin.
- **Body** `CreateSubjectDto`: `name`, 1–150 caratteri.
- **201** GUID.
- **Errori:** **400**, **409** `"Nome materia gia in uso nell'istituto."`.

```bash
curl -s -X POST $API/api/institution/subjects -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Matematica"}'
```

### `PUT /api/institution/subjects/{id}`
**Ruoli:** InstitutionAdmin.
- **Body** `UpdateSubjectDto`.
- **204**.
- **Errori:** **400**, **404**, **409**.

```bash
curl -s -X PUT $API/api/institution/subjects/<ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"name":"Matematica applicata"}'
```

### `DELETE /api/institution/subjects/{id}`
**Ruoli:** InstitutionAdmin.
- **204**.
- **Errori:** **404**, **409** `"La materia e assegnata a classi."`.

```bash
curl -s -X DELETE $API/api/institution/subjects/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 21. Voti

Fonte: `InstitutionGradeController.cs`, `Application/Managers/GradeManager.cs`, `Application/Managers/InstitutionExamManager.cs`, `Domain/Policies/GradeRules.cs`. **Flag:** `SchoolApi` + `ExamApi`.

**Visibilità:**
- lo studente vede **solo i propri** voti;
- il docente vede **solo** quelli delle classi-materie che insegna;
- InstitutionAdmin e Staff vedono tutti i voti dell'istituto.

Per lo Staff i campi extra nel body sono rifiutati.

### `GET /api/institution/grades`
**Ruoli:** InstitutionAdmin, Staff, Teacher, Student.
- **200** `GradeDto[]`
  ```json
  [ { "id": "0a1b…", "studentId": "4d5e…", "subjectId": "9e0f…", "subjectName": "Matematica", "teacherId": "3a4b…", "value": 7.5, "date": "2026-10-20", "student": { "id": "4d5e…", "firstName": "Giulia", "lastName": "Verdi" } } ]
  ```

```bash
curl -s $API/api/institution/grades -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/grades/{id}`
**Ruoli:** tutti e quattro.
- **200** `GradeDto`.
- **Errori:** **403**: voto non visibile. **404**.

```bash
curl -s $API/api/institution/grades/<ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/grades/count`
**Ruoli:** tutti e quattro.
- **200** intero, calcolato sui voti visibili.

```bash
curl -s $API/api/institution/grades/count -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/grades/paged`
**Ruoli:** tutti e quattro.
- **Query:**
  - `pageNumber`, `pageSize`: valori fuori range vengono corretti senza errore (1 e 20).
  - `subjectId`, `studentId`: facoltativi.
  - `date`: `YYYY-MM-DD`, facoltativo.
- **200** `GradePageDto`
  ```json
  { "items": [ { "id": "0a1b…", "studentId": "4d5e…", "subjectId": "9e0f…", "teacherId": "3a4b…", "value": 7.5, "date": "2026-10-20" } ], "totalCount": 42 }
  ```

```bash
curl -s "$API/api/institution/grades/paged?subjectId=<SUBJECT_ID>&pageSize=50" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/grades/filters`
Valori da usare nei filtri della UI. **Ruoli:** tutti e quattro.
- **200** `GradeFiltersDto`
  ```json
  { "subjects": [ { "id": "9e0f…", "name": "Matematica" } ], "students": [ { "id": "4d5e…", "firstName": "Giulia", "lastName": "Verdi" } ] }
  ```

```bash
curl -s $API/api/institution/grades/filters -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/grades/statistics`
Media annuale e medie mensili sui voti visibili. **Ruoli:** tutti e quattro.
- **200** `GradeStatisticsDto`
  ```json
  { "yearlyAverage": 7.12, "monthlyAverage": [null, null, null, null, null, null, null, null, null, 6.8, 7.3, null] }
  ```
- **404** se non c'è nessun voto.

```bash
curl -s $API/api/institution/grades/statistics -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/grades`
Inserisce un voto manuale. **Ruoli:** InstitutionAdmin, Staff, Teacher (`GradeCreate`).
- **Body** `CreateGradeDto`:
  - `studentId`, `subjectId`.
  - `value`: 1–10, decimali ammessi.
  - `date`: `YYYY-MM-DD`.
- **Requisiti:**
  - lo studente deve avere una classe;
  - la materia deve essere assegnata a quella classe;
  - un docente deve insegnare quella classe-materia.
- **201** GUID.
- **Errori:**
  - **400**: valore fuori 1–10.
  - **403**: il docente non insegna la classe-materia.
  - **404**: studente inesistente.
  - **409**: studente senza classe, o materia non assegnata alla sua classe.

```bash
curl -s -X POST $API/api/institution/grades -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"studentId":"<STUDENT_ID>","subjectId":"<SUBJECT_ID>","value":7.5,"date":"2026-10-20"}'
```

### `PUT /api/institution/grades/{id}`
**Ruoli:** InstitutionAdmin, Staff, Teacher (`GradeUpdate`).
- **Body** `UpdateGradeDto`: `subjectId`, `value` (1–10), `date`.
- **204**.
- **Errori:**
  - **400**, **403**, **404**.
  - **409**:
    - il voto è stato generato da un esame (si gestisce con la finalizzazione della consegna);
    - lo Staff ha provato a cambiare materia;
    - materia non assegnata alla classe.

```bash
curl -s -X PUT $API/api/institution/grades/<ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"subjectId":"<SUBJECT_ID>","value":8,"date":"2026-10-20"}'
```

### `DELETE /api/institution/grades/{id}`
**Ruoli:** InstitutionAdmin.
- **204**.
- **Errori:** **404**, **409**: voto generato da un esame.

```bash
curl -s -X DELETE $API/api/institution/grades/<ID> -H "Authorization: Bearer $TOKEN"
```

---

## 22. Esami

Fonte: `InstitutionExamController.cs`, `Application/Managers/ExamManager.cs`, `Business/Services/ExamService.cs`. **Flag:** `SchoolApi` + `ExamApi`.

- Il docente opera **solo sugli esami delle proprie classi-materie**. Per gli altri riceve 403.
- Gli enum `status` e `type` sono **numerici** ([MODELS §2.1](MODELS.md#21-enum-serializzati-come-numero)).
- Per lo Staff i campi extra nel body sono rifiutati.
- **Ciclo di vita:** `Draft (0)` → `Published (1)` → `Closed (2)`, oppure `Published (1)` → `Cancelled (3)`.
- **Modifiche:** domande e studenti si modificano **solo in `Draft`**.

### `GET /api/institution/exams`
**Ruoli:** InstitutionAdmin, Staff, Teacher.
- **200** `ExamSummaryDto[]`
  ```json
  [ { "id": "e1e2…", "classSubjectId": "5c6d…", "name": "Matematica", "description": "Verifica equazioni", "availableFrom": "2026-11-03T08:00:00Z", "availableUntil": "2026-11-03T12:00:00Z", "durationMinutes": 60, "passingPercent": 60, "totalPoints": 20, "status": 0, "assignedStudentsCount": 18, "isRetake": false } ]
  ```

```bash
curl -s $API/api/institution/exams -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/exams/{id}`
Dettaglio con domande, **risposte corrette incluse**, e studenti assegnati. **Ruoli:** InstitutionAdmin, Staff, Teacher.
- **200** `ExamDetailDto`
  ```json
  { "id": "e1e2…", "classSubjectId": "5c6d…", "name": "Matematica", "availableFrom": "2026-11-03T08:00:00Z", "availableUntil": "2026-11-03T12:00:00Z", "durationMinutes": 60, "passingPercent": 60, "totalPoints": 20, "status": 0, "isRetake": false,
    "questions": [ { "id": "q1…", "position": 0, "text": "2+2?", "type": 0, "points": 10, "required": true, "options": [ { "id": "o1…", "position": 0, "text": "4", "isCorrect": true }, { "id": "o2…", "position": 1, "text": "5", "isCorrect": false } ] },
                   { "id": "q2…", "position": 1, "text": "Spiega il teorema", "type": 2, "points": 10, "required": false, "options": [] } ],
    "assignedStudents": [ { "studentId": "4d5e…", "firstName": "Giulia", "lastName": "Verdi", "extraTimeMinutes": 15, "assignedAt": "2026-10-30T10:00:00Z" } ] }
  ```
- **Errori:** **403**: esame di un altro docente. **404**.

```bash
curl -s $API/api/institution/exams/<ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/exams/assigned`
Esami assegnati allo studente corrente, con lo stato della sua consegna. **Ruoli:** Student.
- **200** `AssignedExamDto[]`
  ```json
  [ { "id": "e1e2…", "classSubjectId": "5c6d…", "name": "Matematica", "availableFrom": "2026-11-03T08:00:00Z", "availableUntil": "2026-11-03T12:00:00Z", "durationMinutes": 60, "extraTimeMinutes": 15, "status": 1, "canStart": true, "isRetake": false } ]
  ```
  Dopo l'avvio contiene anche `submissionStatus`, `submissionId`, `startedAt`, `deadlineAt` ed eventualmente `submittedAt`.
- Gli esami in bozza (`Draft`) **non** compaiono. Compaiono invece gli esami chiusi e annullati: filtrarli in UI con `status`. Ordine: `availableFrom` decrescente.

```bash
curl -s $API/api/institution/exams/assigned -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/exams/{id}/eligible-students`
Studenti della classe dell'esame, cioè quelli assegnabili. **Ruoli:** InstitutionAdmin, Staff, Teacher.
- **200** `StudentDto[]`. **404**.

```bash
curl -s $API/api/institution/exams/<ID>/eligible-students -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/exams`
Crea un esame in bozza. **Ruoli:** InstitutionAdmin, Staff, Teacher (`ExamCreate`).
- **Body** [`CreateExamDto`](MODELS.md#38-esami).
- **Validazioni:**
  - `classSubjectId`: esistente; per un docente deve essere una sua classe-materia.
  - Finestra di disponibilità: `availableFrom < availableUntil`, `durationMinutes > 5` e ≤ ampiezza della finestra.
  - `passingPercent`: strettamente tra 0 e 100.
  - Domande: almeno una; `points > 0`; `position ≥ 0` e unica; `text` non vuoto.
  - Domanda **Single**: almeno 2 opzioni, esattamente 1 corretta.
  - Domanda **Multiple**: almeno 2 opzioni, almeno 2 corrette.
  - Domanda **Open**: 0 opzioni.
  - Opzioni: `text` 1–500 caratteri, `position` unica.
  - `assignedStudents`: senza duplicati, `extraTimeMinutes ≥ 0`, tutti della classe della classe-materia. Può essere vuoto in bozza, ma serve per pubblicare.
  - `retakeOfExamId`: stessa classe-materia, senza cicli.
  - `name` è **ignorato**: il nome dell'esame diventa il nome della materia.
- **201** GUID.
- **Errori:** **400**, **403**, **404**: classe-materia, studenti o esame originale inesistenti.

```bash
curl -s -X POST $API/api/institution/exams -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
  "classSubjectId":"<CLASS_SUBJECT_ID>","description":"Verifica equazioni",
  "availableFrom":"2026-11-03T08:00:00Z","availableUntil":"2026-11-03T12:00:00Z","durationMinutes":60,"passingPercent":60,
  "assignedStudents":[{"studentId":"<STUDENT_ID>","extraTimeMinutes":0}],
  "questions":[{"position":0,"text":"2+2?","type":0,"points":10,"required":true,"options":[{"position":0,"text":"4","isCorrect":true},{"position":1,"text":"5","isCorrect":false}]},
               {"position":1,"text":"Spiega il teorema","type":2,"points":10,"required":false,"options":[]}]}'
```

### `PUT /api/institution/exams/{id}`
Sostituisce dati e **intera struttura** delle domande: le domande vengono ricreate con nuovi ID. Solo in `Draft`. **Ruoli:** InstitutionAdmin, Staff, Teacher (`ExamUpdate`).
- **Body** `UpdateExamDto`: come la creazione ma senza `assignedStudents`. `classSubjectId` deve essere **uguale** a quello attuale.
- **204**.
- **Errori:** **400**: anche se si cambia `classSubjectId`. **403**, **404**, **409**: non in bozza.

```bash
curl -s -X PUT $API/api/institution/exams/<ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
  "classSubjectId":"<CLASS_SUBJECT_ID>","description":"Verifica aggiornata","availableFrom":"2026-11-03T08:00:00Z","availableUntil":"2026-11-03T12:00:00Z",
  "durationMinutes":45,"passingPercent":60,"questions":[{"position":0,"text":"2+2?","type":0,"points":10,"required":true,"options":[{"position":0,"text":"4","isCorrect":true},{"position":1,"text":"5","isCorrect":false}]}]}'
```

### `PUT /api/institution/exams/{id}/students`
Sostituisce l'elenco degli studenti assegnati. Solo in `Draft`. **Ruoli:** InstitutionAdmin, Staff, Teacher (`ExamAssign`).
- **Body** `UpdateExamStudentsDto`: `students: ExamStudentInputDto[]`.
- **204**.
- **Errori:** **400**: duplicati, minuti negativi, studenti di un'altra classe. **403**, **404**, **409**.

```bash
curl -s -X PUT $API/api/institution/exams/<ID>/students -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"students":[{"studentId":"<STUDENT_ID>","extraTimeMinutes":15}]}'
```

### `POST /api/institution/exams/{id}/publish`
Pubblica l'esame e lo rende visibile e avviabile dagli studenti. Nessun body. **Ruoli:** InstitutionAdmin, Staff, Teacher (`ExamPublish`).
- **204**.
- **Errori:**
  - **400**: struttura non valida.
  - **403**, **404**.
  - **409**:
    - l'esame non è in bozza;
    - `availableUntil` è già passato;
    - nessuno studente assegnato;
    - totale punti incoerente.

```bash
curl -s -X POST $API/api/institution/exams/<ID>/publish -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/exams/{id}/close`
Chiude un esame pubblicato. **Ruoli:** InstitutionAdmin, Staff, Teacher (`ExamClose`).
- **204**.
- **Errori:** **403**, **404**, **409**: non pubblicato, oppure ci sono consegne `InProgress`.

```bash
curl -s -X POST $API/api/institution/exams/<ID>/close -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/exams/{id}/cancel`
Annulla un esame pubblicato che **nessuno ha ancora iniziato**. **Ruoli:** InstitutionAdmin, Teacher (`ExamCancel`; lo Staff è sempre escluso).
- **204**.
- **Errori:** **403**, **404**, **409**: non pubblicato, o esistono consegne.

```bash
curl -s -X POST $API/api/institution/exams/<ID>/cancel -H "Authorization: Bearer $TOKEN"
```

### `DELETE /api/institution/exams/{id}`
Elimina un esame in bozza. **Ruoli:** InstitutionAdmin, Teacher (`ExamDelete`).
- **204**.
- **Errori:** **403**, **404**, **409**: non in bozza, ha consegne, oppure è l'originale di un recupero.

```bash
curl -s -X DELETE $API/api/institution/exams/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/exams/{id}/start`
Lo studente avvia l'esame e viene creata la consegna. Nessun body. **Ruoli:** Student.
- La scadenza personale è `deadlineAt = startedAt + durationMinutes + extraTimeMinutes`. L'ordine di domande e opzioni è personalizzato per lo studente.
- **200** `StartExamDto`
  ```json
  { "submissionId": "s1s2…", "startedAt": "2026-11-03T08:05:12.123456Z", "deadlineAt": "2026-11-03T09:20:12.123456Z",
    "exam": { "id": "e1e2…", "name": "Matematica", "availableUntil": "2026-11-03T12:00:00Z", "durationMinutes": 60, "totalPoints": 20,
              "questions": [ { "id": "q1…", "position": 0, "text": "2+2?", "type": 0, "points": 10, "required": true, "options": [ { "id": "o2…", "position": 1, "text": "5" }, { "id": "o1…", "position": 0, "text": "4" } ] } ] } }
  ```
- **Errori:**
  - **403**: profilo studente non collegato.
  - **404**: esame inesistente o studente non assegnato.
  - **409**: esame non pubblicato, fuori finestra, o già iniziato/completato.
- **Non è idempotente**: una seconda chiamata dà 409. Per riprendere usare `GET /submissions/{id}/session`.

```bash
curl -s -X POST $API/api/institution/exams/<ID>/start -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/exams/{id}/submissions`
Consegne di un esame. **Ruoli:** InstitutionAdmin, Staff, Teacher.
- **Query:** `status` (facoltativo, **numerico** come `SubmissionStatus`, es. `?status=2`).
- **200** `SubmissionSummaryDto[]`
  ```json
  [ { "id": "s1s2…", "examId": "e1e2…", "studentId": "4d5e…", "studentFirstName": "Giulia", "studentLastName": "Verdi", "status": 2, "startedAt": "…", "deadlineAt": "…", "submittedAt": "…", "submitReason": 0, "violationCount": 1, "totalScore": 10, "maxScore": 20 } ]
  ```
- **Errori:** **400**: stato non valido. **403**, **404**.

```bash
curl -s "$API/api/institution/exams/<ID>/submissions?status=2" -H "Authorization: Bearer $TOKEN"
```

---

## 23. Consegne (submissions)

Fonte: `InstitutionSubmissionController.cs`, `Application/Services/ExamSubmissionProcessor.cs`, `Application/Managers/ExamSubmissionManager.cs`, `Infrastructure/Repositories/ExamSubmissionRepository.cs`. **Flag:** `SchoolApi` + `ExamApi`.

Flusso completo in [FLOWS §5](FLOWS.md#5-svolgimento-esame-studente) e [§6](FLOWS.md#6-correzione-e-voto-finale-docente).

### `GET /api/institution/submissions/{id}/session`
Riprende una sessione d'esame: domande nell'ordine personale e risposte salvate. **Ruoli:** Student (proprietario).
- **200** `SubmissionSessionDto`
  ```json
  { "submissionId": "s1s2…", "examId": "e1e2…", "status": 0, "startedAt": "…", "deadlineAt": "…", "currentRevision": 3,
    "exam": { "id": "e1e2…", "name": "Matematica", "availableUntil": "…", "durationMinutes": 60, "totalPoints": 20, "questions": [ … ] },
    "answers": [ { "questionId": "q1…", "selectedOptionIds": ["o1…"] } ] }
  ```
- **Errori:** **403**, **404**.

```bash
curl -s $API/api/institution/submissions/<SUBMISSION_ID>/session -H "Authorization: Bearer $TOKEN"
```

### `PUT /api/institution/submissions/{id}/answers`
Autosalvataggio: **sostituisce tutte** le risposte salvate. **Ruoli:** Student. **Non soggetto al rate limit** (è un `PUT`).
- **Body** `SaveExamAnswersDto`:
  - `clientRevision`: deve essere **maggiore** dell'ultima revisione salvata.
  - `answers`:
    - una sola risposta per domanda;
    - le opzioni devono appartenere alla domanda, senza duplicati;
    - domanda Single: al massimo 1 opzione;
    - domanda Open: nessuna opzione.
- **200** `SaveAnswersResponseDto`
  ```json
  { "accepted": true, "currentRevision": 4, "savedAt": "2026-11-03T08:21:00Z" }
  ```
  - `accepted: false` (senza `savedAt`) se `clientRevision ≤ currentRevision` oppure se la scadenza è passata. In quel caso **le risposte non sono state salvate**.
- **Errori:**
  - **400**: duplicati, opzioni non coerenti col tipo.
  - **403**: consegna di un altro studente.
  - **404**: domanda o opzione estranea all'esame.
  - **409**: consegna non più in corso.

```bash
curl -s -X PUT $API/api/institution/submissions/<SUBMISSION_ID>/answers -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"clientRevision":4,"answers":[{"questionId":"<Q1>","selectedOptionIds":["<O1>"]},{"questionId":"<Q2>","openText":"Risposta…","selectedOptionIds":[]}]}'
```

### `POST /api/institution/submissions/{id}/submit`
Consegna manuale, con le ultime risposte. **Ruoli:** Student.
- **Body** `SubmitExamDto`:
  - `clientRevision`: **maggiore** dell'ultima salvata.
  - `answers`: l'insieme completo delle risposte.
  - Le domande `required` devono avere una risposta: testo non vuoto per Open, almeno un'opzione per Single e Multiple.
- **200** `SubmissionResultDto`
  ```json
  { "submissionId": "s1s2…", "examId": "e1e2…", "status": 2, "submittedAt": "2026-11-03T08:58:00Z", "submitReason": 0 }
  ```
  - Per lo studente punteggio e voto compaiono **solo** con stato `Graded (1)` o `Finalized (4)`.
  - **Idempotente**: se la consegna è già stata effettuata restituisce il risultato esistente.
- **Errori:**
  - **400** `Richiesta non valida`: domande obbligatorie senza risposta.
  - **409** `Operazione non consentita`:
    - scadenza superata (usare `submit-expired`);
    - `clientRevision` non maggiore dell'ultima (`"Impossibile salvare le risposte inviate."`).

```bash
curl -s -X POST $API/api/institution/submissions/<SUBMISSION_ID>/submit -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"clientRevision":5,"answers":[{"questionId":"<Q1>","selectedOptionIds":["<O1>"]}]}'
```

### `POST /api/institution/submissions/{id}/submit-expired`
Consegna a tempo scaduto: vengono usate le risposte già salvate. **Nessun body**: `Content-Length` deve essere 0 e senza `Transfer-Encoding`. **Ruoli:** Student.
- **200** `SubmissionResultDto` con `submitReason: 1` (TimeExpired). Idempotente.
- **Errori:**
  - **400**: è stato inviato un body.
  - **409**: la scadenza non è ancora arrivata (`"Non è ancora tempo di eseguire il submit automatico."`).

```bash
curl -s -X POST $API/api/institution/submissions/<SUBMISSION_ID>/submit-expired -H "Authorization: Bearer $TOKEN" -H "Content-Length: 0"
```

### `POST /api/institution/submissions/{id}/violations`
Segnala un evento anti-cheat durante lo svolgimento. **Ruoli:** Student. Body ≤ 4 KiB.
- **Body** `InstitutionViolationDto`:
  - `eventId`: nuovo per ogni evento.
  - `type`: `tab_hidden` | `window_blur` | `fullscreen_exit`.
  - `metadata`: facoltativo; **stringa** contenente un oggetto JSON, ≤ 1024 byte, profondità ≤ 4.
- **Limiti:**
  - massimo 5 eventi ogni 10 secondi;
  - massimo 128 per consegna;
  - solo mentre la consegna è in corso e prima della scadenza.
- **200** `ViolationReceiptDto`
  ```json
  { "eventId": "…", "violationCount": 2, "duplicate": false }
  ```
  Con lo stesso `eventId` e lo stesso payload la risposta ha `duplicate: true`.
- **Le violazioni non chiudono la consegna**: vengono solo conteggiate.
- **Errori:**
  - **400**: tipo o metadata non validi.
  - **404**.
  - **409**: consegna non attiva, limite raggiunto, `eventId` già usato con dati diversi.

```bash
curl -s -X POST $API/api/institution/submissions/<SUBMISSION_ID>/violations -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","type":"tab_hidden","metadata":"{\"durationMs\":3200}"}'
```

### `GET /api/institution/submissions/{id}/violations`
Elenco delle violazioni, al massimo 128 in ordine cronologico. **Ruoli:** InstitutionAdmin, Staff, Teacher.
- **200** `ViolationEventDto[]`
  ```json
  [ { "eventId": "…", "type": "tab_hidden", "occurredAt": "2026-11-03T08:30:01Z", "metadata": "{\"durationMs\":3200}" } ]
  ```
- **Errori:** **404**.

```bash
curl -s $API/api/institution/submissions/<SUBMISSION_ID>/violations -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/submissions/{id}/work`
Lo studente rivede il proprio elaborato. **Ruoli:** Student (proprietario).
- **200** `SubmissionReviewDto`, vedi l'esempio sotto, **mascherato per lo studente** (`Application/Mappers/SubmissionVisibility.cs`):
  - sempre: `suggestedGrade` e `reviewedByUserId` omessi; per ogni risposta `isCorrect`, `awardedPoints` e `teacherComment` omessi, e `options[].isCorrect` sempre `false`;
  - finché non c'è un voto ufficiale (stato `Graded` o `Finalized`): omessi anche `finalGrade`, `maxScore`, `passingScore`, `closedScore`, `openMaxPoints` e gli altri punteggi.
- **Errori:** **403**, **404**.

```bash
curl -s $API/api/institution/submissions/<SUBMISSION_ID>/work -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/submissions/{id}/review`
Dettaglio per la correzione. **Ruoli:** InstitutionAdmin, Staff, Teacher (docente della classe-materia).
- **200** `SubmissionReviewDto`
  ```json
  { "id": "s1s2…", "examId": "e1e2…", "examName": "Matematica", "studentId": "4d5e…", "studentFirstName": "Giulia", "studentLastName": "Verdi",
    "status": 2, "startedAt": "…", "deadlineAt": "…", "submittedAt": "…", "submitReason": 0, "violationCount": 1,
    "maxScore": 20, "passingScore": 12, "closedScore": 10, "openMaxPoints": 10, "totalScore": 10,
    "answers": [ { "answerId": "a1…", "questionId": "q2…", "position": 1, "questionText": "Spiega il teorema", "questionType": 2, "maxPoints": 10, "required": false, "openText": "…", "options": [] } ] }
  ```
- **Errori:** **403**, **404**.

```bash
curl -s $API/api/institution/submissions/<SUBMISSION_ID>/review -H "Authorization: Bearer $TOKEN"
```

### `PUT /api/institution/submissions/{id}/review`
Assegna i punti alle risposte aperte. **Ruoli:** InstitutionAdmin, Teacher (della classe-materia).
- **Body** `ReviewOpenAnswersDto`:
  - `answers[]` con `answerId`, `awardedPoints`, `comment`.
  - Solo risposte a domande di tipo Open.
  - `awardedPoints` entro i punti della domanda.
- **Effetto:** stato `PendingReview`. Quando tutte le risposte aperte sono corrette viene calcolato `suggestedGrade`.
- **204**.
- **Errori:**
  - **400**: risposta non aperta, punti non validi.
  - **403**, **404**.
  - **409**: consegna non consegnata, `InProgress`, `Finalized` o già con voto ufficiale.

```bash
curl -s -X PUT $API/api/institution/submissions/<SUBMISSION_ID>/review -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"answers":[{"answerId":"<ANSWER_ID>","awardedPoints":7,"comment":"Buona argomentazione"}]}'
```

### `POST /api/institution/submissions/{id}/finalize`
Registra il voto ufficiale della consegna, creando un `Grade`. **Ruoli:** InstitutionAdmin, Teacher.
- **Body** `FinalizeSubmissionDto`:
  - `finalGrade`: 1–10, oppure `null` per usare il voto suggerito.
  - `overrideReason`: **obbligatorio** se `finalGrade` è diverso dal voto suggerito.
- **204**. È idempotente se ripetuto con gli stessi valori.
- **Errori:**
  - **400**: voto fuori range, motivazione mancante.
  - **403**, **404**.
  - **409**:
    - consegna non consegnata o `InProgress`;
    - risposte aperte non ancora corrette;
    - una finalizzazione precedente aveva valori diversi.

```bash
curl -s -X POST $API/api/institution/submissions/<SUBMISSION_ID>/finalize -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"finalGrade":null,"overrideReason":null}'
```

### `GET /api/institution/submissions/{id}/result`
Esito sintetico della consegna. **Ruoli:** InstitutionAdmin, Staff, Teacher, Student (proprietario).
- **200** `SubmissionResultDto`
  ```json
  { "submissionId": "s1s2…", "examId": "e1e2…", "status": 4, "submittedAt": "…", "submitReason": 0, "totalScore": 17, "maxScore": 20, "grade": 8.4, "passed": true }
  ```
  - `passed` è calcolato dal voto ufficiale (≥ 6).
  - Per lo studente, finché non c'è un voto ufficiale, punteggi e voto sono omessi.
- **Errori:** **403**, **404**.

```bash
curl -s $API/api/institution/submissions/<SUBMISSION_ID>/result -H "Authorization: Bearer $TOKEN"
```

---

## 24. Calendario

Fonte: `InstitutionCalendarController.cs`, `Application/Managers/InstitutionCalendarManager.cs`, `Domain/Policies/InstitutionCalendarRules.cs`, `Infrastructure/Repositories/InstitutionCalendarRepository*.cs`, `ExceptionHandling/CalendarErrorsAttribute.cs`.

**Flag:** `SchoolApi` + `InstitutionCalendar`. Se il calendario è spento gli endpoint **non esistono** e rispondono 404. Body ≤ 12 MiB.

**Regole generali:**
- Errori con `code` `calendar.*`: vedi [CONVENTIONS §6.1](CONVENTIONS.md#61-calendario-apiexceptionhandlingcalendarerrorsattributecs).
- Ogni comando richiede `eventId` (idempotenza) ed `expectedRevision`.
- `reason`: 1–500 caratteri, senza caratteri di controllo.
- Orari al minuto (`HH:mm:00`), nello stesso giorno, nel fuso configurato per l'istituto.
- **Stati della lezione:**
  - `publication`: `Draft` → `Published` (pubblicazione mensile per classe).
  - `status`: `Scheduled` → `Cancelled` oppure `Scheduled` → `Completed`.
- **Prerequisiti:**
  - la classe deve avere un'edizione corso con un **piano formativo pubblicato** collegato;
  - il modulo della lezione deve appartenere a quel piano;
  - la materia del modulo deve essere assegnata alla classe (classe-materia).

### `GET /api/institution/calendar/settings`
Impostazioni del calendario dell'istituto. **Ruoli:** InstitutionAdmin, Staff.
- **200** `CalendarSettings`
  ```json
  { "timeZoneId": "Europe/Rome", "approvalLeadDays": 5, "revision": 1 }
  ```
- **Errori:** **409** `calendar.settings-required` se il calendario non è ancora configurato (`InstitutionCalendarRepository.cs` riga 196). Senza impostazioni non si possono creare lezioni.

```bash
curl -s $API/api/institution/calendar/settings -H "Authorization: Bearer $TOKEN"
```

### `PUT /api/institution/calendar/settings`
Configura fuso orario e giorni di preavviso per l'approvazione mensile. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarSettingsCommand`:
  - `eventId`.
  - `expectedRevision`: **`-1` per la prima configurazione**, poi la `revision` corrente. Le revisioni partono da 0.
  - `timeZoneId`: ID IANA, es. `Europe/Rome`.
  - `approvalLeadDays`: 0–366.
  - `reason`.
- **200** `CalendarReceipt`
  ```json
  { "eventId": "…", "resourceId": "…", "revision": 2 }
  ```
- **Errori:**
  - **400**: `calendar.invalid-time-zone`, `calendar.invalid-approval-lead-days`, `calendar.reason-required`.
  - **409**: `calendar.revision-conflict`.

```bash
curl -s -X PUT $API/api/institution/calendar/settings -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":-1,"timeZoneId":"Europe/Rome","approvalLeadDays":5,"reason":"Configurazione iniziale"}'
```

### `GET /api/institution/calendar`
Lezioni dell'istituto, vista operatore. Comprende anche le bozze. **Ruoli:** InstitutionAdmin, Staff.
- **Query** [`CalendarQuery`](MODELS.md#39-calendario):
  - `from`, `to`: obbligatori, `to - from ≤ 366` giorni.
  - Filtri facoltativi: `classId`, `teacherId`, `moduleId`, `courseEditionId`.
  - `publication`: `Draft` | `Published`.
  - `status`: `Scheduled` | `Cancelled` | `Completed`.
  - `includeCancelled`: default `false`.
  - `page`: default 1. `size`: default 100, max 100.
  - Ordine: per `startUtc`.
- **200** `CalendarLesson[]`
  ```json
  [ { "id": "l1…", "periodId": "p1…", "classId": "1a2b…", "courseEditionId": "7e8f…", "versionId": "v1…", "moduleId": "m1…", "subjectId": "9e0f…", "classSubjectId": "5c6d…",
      "originalTeacherId": "3a4b…", "teacherId": "3a4b…", "date": "2026-11-04", "startsAt": "09:00:00", "endsAt": "11:00:00", "timeZoneId": "Europe/Rome",
      "startUtc": "2026-11-04T08:00:00Z", "endUtc": "2026-11-04T10:00:00Z", "publication": "Published", "status": "Scheduled", "revision": 1,
      "room": "Aula 3", "className": "CD25", "moduleName": "Algebra", "teacherName": "Laura Bianchi", "durationMinutes": 120 } ]
  ```
- **Errori:** **400**: range oltre 366 giorni, paginazione, valori di filtro non ammessi.

```bash
curl -s "$API/api/institution/calendar?from=2026-11-01&to=2026-11-30&classId=<CLASS_ID>" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/calendar/classes/{classId}`
Come l'endpoint precedente, filtrato sulla classe. **Ruoli:** InstitutionAdmin, Staff.
- **Query:** come `GET /calendar` (`classId` viene preso dal path).
- **200** `CalendarLesson[]`.

```bash
curl -s "$API/api/institution/calendar/classes/<CLASS_ID>?from=2026-11-01&to=2026-11-30" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/calendar/teachers/me`
Calendario del docente corrente: solo lezioni **pubblicate** in cui è il docente. **Ruoli:** Teacher.
- **Query:** `CalendarQuery`.
- **200** `CalendarLesson[]`.

```bash
curl -s "$API/api/institution/calendar/teachers/me?from=2026-11-01&to=2026-11-30" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/calendar/students/me`
Calendario dello studente corrente: solo lezioni **pubblicate** della sua classe. **Ruoli:** Student.
- **Query:** `CalendarQuery`.
- **200** `CalendarLesson[]`.

```bash
curl -s "$API/api/institution/calendar/students/me?from=2026-11-01&to=2026-11-07" -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/calendar/months/{year}/{month}/classes/{classId}`
Stato della pubblicazione mensile di una classe, con la scadenza di approvazione. **Ruoli:** InstitutionAdmin, Staff.
- **200** `CalendarPeriod`
  ```json
  { "id": "p1…", "classId": "1a2b…", "month": "2026-11-01", "status": "Draft", "revision": 3,
    "approval": { "deadlineLocalDate": "2026-10-27", "isPublished": false, "isLate": true, "isOverdue": true } }
  ```
- **Errori:** **400**: `calendar.invalid-period`. **404**.

```bash
curl -s $API/api/institution/calendar/months/2026/11/classes/<CLASS_ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/calendar/months/{year}/{month}/classes/{classId}/publish`
Pubblica tutte le lezioni in bozza della classe nel mese indicato. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarCommand`: `eventId`, `expectedRevision` (revisione del periodo), `reason`.
- **200** `CalendarReceipt` con `lessonIds` (le lezioni pubblicate).
- **Errori:** **409**: `calendar.no-draft-lessons`, `calendar.revision-conflict`, conflitti di sovrapposizione.

```bash
curl -s -X POST $API/api/institution/calendar/months/2026/11/classes/<CLASS_ID>/publish -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":3,"reason":"Approvazione calendario novembre"}'
```

### `GET /api/institution/calendar/lessons/{id}`
Dettaglio di una lezione. **Ruoli:** tutti e quattro. Docente e studente vedono solo le lezioni pubblicate che li riguardano.
- **200** `CalendarLesson`. **404**.

```bash
curl -s $API/api/institution/calendar/lessons/<LESSON_ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/calendar/lessons/{id}/module`
Modulo del piano formativo associato alla lezione: contenuti e competenze. **Ruoli:** tutti e quattro.
- **200** [`FormativeModuleSnapshot`](MODELS.md#310-piani-formativi). **404**.

```bash
curl -s $API/api/institution/calendar/lessons/<LESSON_ID>/module -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/calendar/lessons`
Crea una lezione in bozza. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarSaveLesson`:
  - `eventId`.
  - `expectedRevision: -1`: obbligatorio per la creazione, ogni altro valore dà `calendar.revision-conflict`.
  - `lesson`: `CalendarLessonInput`.
  - `reason`.
- **`lesson`** (`CalendarLessonInput`):
  - `classId`, `moduleId`, `teacherId`, `date`, `startsAt`, `endsAt`: obbligatori.
  - `room` (≤ 200), `description` (≤ 2000), `externalReference` (≤ 100): facoltativi.
  - `substitutionReason`: **obbligatorio** se `teacherId` è diverso dal docente della classe-materia.
  - `recoveryOfLessonId`: facoltativo, se è il recupero di una lezione annullata.
- La lezione nasce con `publication: "Draft"`, `status: "Scheduled"`, `revision: 0`.
- **200** `CalendarReceipt` (`resourceId` = ID della lezione).
- **Errori:**
  - **400**: orari, testi, fuso.
  - **409**: sovrapposizioni, modulo/classe non disponibili, piano non pubblicato.

```bash
curl -s -X POST $API/api/institution/calendar/lessons -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":-1,"reason":"Pianificazione","lesson":{"classId":"<CLASS_ID>","moduleId":"<MODULE_ID>","teacherId":"<TEACHER_ID>","date":"2026-11-04","startsAt":"09:00:00","endsAt":"11:00:00","room":"Aula 3"}}'
```

### `PUT /api/institution/calendar/lessons/{id}`
Modifica una lezione `Scheduled`, sia in bozza sia già pubblicata; lo stato di pubblicazione resta invariato. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarSaveLesson` con la `expectedRevision` corrente.
- **Non si possono cambiare** classe, edizione e versione del piano: errore `calendar.ownership-immutable`.
- **200** `CalendarReceipt`.
- **Errori:** come la creazione, più:
  - `calendar.revision-conflict`;
  - `calendar.ownership-immutable`;
  - `calendar.scheduled-required` (lezione annullata o svolta).

```bash
curl -s -X PUT $API/api/institution/calendar/lessons/<LESSON_ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":1,"reason":"Cambio aula","lesson":{"classId":"<CLASS_ID>","moduleId":"<MODULE_ID>","teacherId":"<TEACHER_ID>","date":"2026-11-04","startsAt":"09:00:00","endsAt":"11:00:00","room":"Aula 5"}}'
```

### `POST /api/institution/calendar/lessons/{id}/cancel`
Annulla una lezione `Scheduled`. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarCommand`.
- **200** `CalendarReceipt`.
- **Errori:** **409**: `calendar.scheduled-required`, `calendar.revision-conflict`.

```bash
curl -s -X POST $API/api/institution/calendar/lessons/<LESSON_ID>/cancel -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":2,"reason":"Docente assente"}'
```

### `POST /api/institution/calendar/lessons/{id}/reschedule`
Sposta una lezione `Scheduled`. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarRescheduleCommand`: `eventId`, `expectedRevision`, `date`, `startsAt`, `endsAt`, `reason`.
- **200** `CalendarReceipt`.
- **Errori:** **400**: orari. **409**: stato, revisione, sovrapposizioni.

```bash
curl -s -X POST $API/api/institution/calendar/lessons/<LESSON_ID>/reschedule -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":2,"date":"2026-11-05","startsAt":"14:00:00","endsAt":"16:00:00","reason":"Spostamento"}'
```

### `POST /api/institution/calendar/lessons/{id}/substitute`
Sostituisce il docente di una lezione `Scheduled`. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarSubstituteCommand`: `eventId`, `expectedRevision`, `teacherId`, `reason`.
- **200** `CalendarReceipt`.
- **Errori:** **409**: `calendar.invalid-substitution`, `calendar.teacher-unavailable`, `calendar.revision-conflict`.

```bash
curl -s -X POST $API/api/institution/calendar/lessons/<LESSON_ID>/substitute -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":2,"teacherId":"<TEACHER_ID>","reason":"Supplenza"}'
```

### `POST /api/institution/calendar/lessons/{id}/complete`
Segna come svolta una lezione **pubblicata** e `Scheduled`, indicando i minuti effettivi. **Ruoli:** InstitutionAdmin, Staff, Teacher (il docente solo per le proprie lezioni).
- **Body** `CalendarDeliveredCommand`:
  - `eventId`, `expectedRevision`.
  - `deliveredMinutes`: 1–1440.
  - `reason`.
- **200** `CalendarReceipt`.
- **Errori:**
  - **400**: `calendar.invalid-delivered-minutes`.
  - **409**: `calendar.published-scheduled-required`, `calendar.revision-conflict`.

```bash
curl -s -X POST $API/api/institution/calendar/lessons/<LESSON_ID>/complete -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":2,"deliveredMinutes":120,"reason":"Lezione svolta"}'
```

### `POST /api/institution/calendar/lessons/{id}/correct-delivered-time`
Corregge i minuti erogati di una lezione `Completed`. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarDeliveredCommand`.
- **200** `CalendarReceipt`.
- **Errori:** **409**: `calendar.completed-required`.

```bash
curl -s -X POST $API/api/institution/calendar/lessons/<LESSON_ID>/correct-delivered-time -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":3,"deliveredMinutes":110,"reason":"Uscita anticipata"}'
```

### `GET /api/institution/calendar/lessons/{id}/history`
Storico delle modifiche di una lezione. **Ruoli:** InstitutionAdmin, Staff.
- **Query:** `page` (1), `size` (20, max 100).
- **200** `CalendarHistory[]`
  ```json
  [ { "eventId": "…", "operation": "LessonCompleted", "actorUserId": "…", "actorMembershipId": "…", "revisionBefore": 2, "revisionAfter": 3, "stateJson": "{…}", "reason": "Lezione svolta", "occurredAtUtc": "…" } ]
  ```
  Valori di `operation` usati dal controller: `LessonCancelled`, `LessonRescheduled`, `TeacherSubstituted`, `LessonCompleted`, `DeliveredMinutesCorrected`. Altri valori non verificati.

```bash
curl -s $API/api/institution/calendar/lessons/<LESSON_ID>/history -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/calendar/hours`
Report ore della classe: pianificate, programmate ed erogate, per modulo, unità e mese. **Ruoli:** InstitutionAdmin, Staff.
- **Query:** `classId` (obbligatorio).
- **200** `CalendarHoursReport`
  ```json
  { "classId": "1a2b…", "courseEditionId": "7e8f…", "versionId": "v1…",
    "modules": [ { "classId": "1a2b…", "moduleId": "m1…", "plannedMinutes": 1200, "scheduledMinutes": 600, "deliveredMinutes": 240, "remainingToScheduleMinutes": 600, "remainingToDeliverMinutes": 960, "scheduledExcessMinutes": 0, "deliveredExcessMinutes": 0, "warnings": ["calendar.under-scheduled","calendar.delivery-incomplete"] } ],
    "units": [ { "unitId": "u1…", "plannedMinutes": 1200, "scheduledMinutes": 600, "deliveredMinutes": 240 } ],
    "months": [ { "year": 2026, "month": 11, "scheduledMinutes": 600, "deliveredMinutes": 240 } ],
    "plannedMinutes": 1200, "scheduledMinutes": 600, "deliveredMinutes": 240 }
  ```

```bash
curl -s "$API/api/institution/calendar/hours?classId=<CLASS_ID>" -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/calendar/imports/preview`
Carica un file CSV o XLSX di lezioni e ne genera l'anteprima. **Ruoli:** InstitutionAdmin, Staff.
- **Content-Type:** `multipart/form-data`, al massimo 12 MiB.
- **Campi form:**
  - `file`: esattamente uno.
  - `settings`: stringa JSON `CalendarImportSettings`.
  - `parsing`: stringa JSON `ImportParsingOptions`, facoltativa.
  - Nessun altro campo.
- **`settings`** (`CalendarImportSettings`):
  - `eventId`.
  - `year`, `month`: il mese da importare.
  - `classIds`: le classi coinvolte.
  - `mode`:
    - `AppendDraft`: aggiunge le lezioni alle bozze esistenti;
    - `ReplaceDraftScope`: sostituisce le bozze nello scope.
  - `mapping`: colonne → campi; facoltativo.
  - `reason`.
- **200** `CalendarImportPreview`: righe con problemi e conflitti, impatto sulle ore, `canConfirm`.
- **Errori:**
  - **400** `{"code":"calendar.invalid-import-settings"}`: JSON di `settings`/`parsing` non valido. Il body **non** è un ProblemDetails.
  - **400** senza body: numero di file o campi errato.
  - **413**, **415**, oppure **400** con `code` di import.

```bash
curl -s -X POST $API/api/institution/calendar/imports/preview -H "Authorization: Bearer $TOKEN" \
  -F "file=@lezioni-novembre.xlsx" \
  -F 'settings={"eventId":"<UUID>","year":2026,"month":11,"classIds":["<CLASS_ID>"],"mode":"AppendDraft","mapping":null,"reason":"Import novembre"}'
```

### `GET /api/institution/calendar/imports/{id}`
Rilegge un'anteprima di import calendario. **Ruoli:** InstitutionAdmin, Staff.
- **200** `CalendarImportPreview`.
- **Errori:** **404**, **409**: `calendar.preview-expired-or-consumed`.

```bash
curl -s $API/api/institution/calendar/imports/<IMPORT_ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/calendar/imports/{id}/confirm`
Applica l'anteprima e crea le lezioni in bozza. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `CalendarConfirmImport`:
  - `eventId`.
  - `expectedRevision`, `digest`: valori `revision` e `digest` dell'anteprima.
  - `acknowledgeWarnings`: deve essere `true` se ci sono warning.
  - `reason`.
- **200** `CalendarReceipt` con `lessonIds`.
- **Errori:** **409**:
  - `calendar.preview-digest-conflict`, `calendar.preview-stale-or-invalid`, `calendar.preview-expired-or-consumed`;
  - `calendar.warnings-not-acknowledged`;
  - conflitti di sovrapposizione.

```bash
curl -s -X POST $API/api/institution/calendar/imports/<IMPORT_ID>/confirm -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":1,"digest":"<DIGEST>","acknowledgeWarnings":true,"reason":"Conferma import"}'
```

---

## 25. Piani formativi

Fonte: `InstitutionFormativePlanController.cs`, `Application/Managers/InstitutionFormativePlanManager.cs`, `Domain/Policies/FormativePlanRules.cs`, `Infrastructure/Repositories/InstitutionFormativePlanRepository.cs`.

**Flag:** `SchoolApi` + `FormativePlans`. Se il modulo è spento gli endpoint **non esistono** e rispondono 404. Body ≤ 2 MiB.

**Modello:**
- Un **piano** appartiene a un percorso di studio.
- Il piano ha **versioni** numerate. Una versione `Draft` si modifica sostituendone l'intero contenuto; una versione `Published` è immutabile.
- Una versione pubblicata si **collega** a un'edizione corso, ed è quella usata dal calendario.

**Errori:**
- Conflitti di stato, revisione o database → **409** `Operazione non consentita` (`"Conflitto sul piano formativo."`).
- Pubblicazione di un piano incompleto → **409** con `hourSummary`.

### `GET /api/institution/formative-plans`
**Ruoli:** InstitutionAdmin, Staff.
- **Query:** `page`, `size`.
- **200** `FormativePlanInfo[]`
  ```json
  [ { "id": "fp1…", "studyPathId": "c3d4…", "code": "CD-PLAN", "name": "Piano Cloud Developer", "description": "Biennio 2025-27", "revision": 0, "createdAtUtc": "2025-06-01T10:00:00Z" } ]
  ```

```bash
curl -s $API/api/institution/formative-plans -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/formative-plans/{id}`
**Ruoli:** InstitutionAdmin, Staff.
- **200** `FormativePlanInfo`. **404**.

```bash
curl -s $API/api/institution/formative-plans/<PLAN_ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/formative-plans`
**Ruoli:** InstitutionAdmin.
- **Body** `CreateFormativePlan`:
  - `eventId`, `studyPathId`.
  - `code`: obbligatorio, ≤ 64 caratteri.
  - `name`: ≤ 200 caratteri.
  - `description`: ≤ 65535 byte.
  - `reason`.
- **200** `FormativeReceipt`
  ```json
  { "eventId": "…", "planId": "fp1…", "revision": 0 }
  ```
  Restituisce **200**, non 201.
- **Errori:** **400**, **409**: codice duplicato.

```bash
curl -s -X POST $API/api/institution/formative-plans -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","studyPathId":"<STUDY_PATH_ID>","code":"CD-PLAN","name":"Piano Cloud Developer","description":null,"reason":"Nuovo piano"}'
```

### `PUT /api/institution/formative-plans/{id}`
Rinomina il piano. **Ruoli:** InstitutionAdmin.
- **Body** `RenameFormativePlan`: `eventId`, `expectedRevision`, `name`, `description`, `reason`.
- **200** `FormativeReceipt`.
- **Errori:** **400**, **404**, **409**.

```bash
curl -s -X PUT $API/api/institution/formative-plans/<PLAN_ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":0,"name":"Piano CD 2025","description":null,"reason":"Rinomina"}'
```

### `GET /api/institution/formative-plans/{id}/versions`
Versioni del piano. **Ruoli:** InstitutionAdmin, Staff.
- **Query:** `page`, `size`.
- **200** `FormativePlanSnapshot[]`.

```bash
curl -s $API/api/institution/formative-plans/<PLAN_ID>/versions -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/formative-plans/{id}/versions`
Crea una nuova versione in bozza, vuota. **Ruoli:** InstitutionAdmin.
- **Body** `CreateFormativeVersion`:
  - `eventId`.
  - `name`.
  - `theoryPlannedMinutes`, `internshipPlannedMinutes`: obbligatori.
  - `reason`.
- **200** `FormativeReceipt` con `versionId`.
- **Errori:** **400**, **404**, **409**.

```bash
curl -s -X POST $API/api/institution/formative-plans/<PLAN_ID>/versions -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","name":"Versione 1","theoryPlannedMinutes":48000,"internshipPlannedMinutes":48000,"reason":"Prima versione"}'
```

### `GET /api/institution/formative-plan-versions/{id}`
Una versione con unità, moduli, competenze e contenuti. **Ruoli:** InstitutionAdmin, Staff.
- **200** `FormativePlanSnapshot`
  ```json
  { "id": "v1…", "institutionId": "8f2c…", "studyPathId": "c3d4…", "planId": "fp1…", "versionNumber": 1, "name": "Versione 1", "status": "Draft", "revision": 2,
    "theoryPlannedMinutes": 1200, "internshipPlannedMinutes": 0,
    "units": [ { "id": "u1…", "code": "U1", "name": "Fondamenti", "displayOrder": 0, "plannedMinutes": 1200,
                 "modules": [ { "id": "m1…", "code": "M1", "name": "Algebra", "plannedMinutes": 1200, "displayOrder": 0, "subjectId": "9e0f…",
                                "competencies": [ { "id": "t1…", "description": "Risolvere equazioni", "displayOrder": 0 } ], "contents": [] } ] } ],
    "createdAtUtc": "2025-06-01T10:00:00Z" }
  ```
  `status` è una **stringa** (`Draft` | `Published`).
- **Errori:** **404**.

```bash
curl -s $API/api/institution/formative-plan-versions/<VERSION_ID> -H "Authorization: Bearer $TOKEN"
```

### `PUT /api/institution/formative-plan-versions/{id}`
**Sostituisce interamente** il contenuto di una bozza. **Ruoli:** InstitutionAdmin.
- **Body** `ReplaceFormativeDraft`:
  - `eventId`, `expectedRevision`.
  - `name`, `theoryPlannedMinutes`, `internshipPlannedMinutes`.
  - `units[]` con `modules[]`, e per ogni modulo `competencies[]` e `contents[]`.
  - `reason`.
- **Regole per unità, moduli e argomenti:**
  - Gli `id` sono facoltativi: si riusano quelli esistenti per mantenere l'identità.
  - `displayOrder` è obbligatorio.
  - Testi degli argomenti ≤ 4000 caratteri; codici ≤ 64.
- **Limiti:**
  - minuti per componente ≤ 10.000.000;
  - ≤ 500 unità, ≤ 500 moduli per unità, ≤ 200 argomenti per modulo.
- **200** `FormativeReceipt`.
- **Errori:** **400**, **404**, **409**: versione non in bozza o revisione obsoleta.

```bash
curl -s -X PUT $API/api/institution/formative-plan-versions/<VERSION_ID> -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" -d '{
  "eventId":"<UUID>","expectedRevision":1,"name":"Versione 1","theoryPlannedMinutes":1200,"internshipPlannedMinutes":0,"reason":"Struttura",
  "units":[{"id":null,"code":"U1","name":"Fondamenti","description":null,"displayOrder":0,
    "modules":[{"id":null,"code":"M1","name":"Algebra","description":null,"plannedMinutes":1200,"displayOrder":0,"subjectId":"<SUBJECT_ID>",
      "competencies":[{"id":null,"description":"Risolvere equazioni","displayOrder":0}],"contents":[]}]}]}'
```

### `POST /api/institution/formative-plan-versions/{id}/clone`
Crea una nuova bozza copiando questa versione. **Ruoli:** InstitutionAdmin.
- **Body** `FormativeCommand`: `eventId`, `expectedRevision`, `reason`.
- **200** `FormativeReceipt` con il nuovo `versionId`.
- **Errori:** **404**, **409**.

```bash
curl -s -X POST $API/api/institution/formative-plan-versions/<VERSION_ID>/clone -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":3,"reason":"Nuova revisione"}'
```

### `POST /api/institution/formative-plan-versions/{id}/publish`
Pubblica e congela la versione. **Ruoli:** InstitutionAdmin.
- **Body** `FormativeCommand`.
- **200** `FormativeReceipt`.
- **Errori:**
  - **409** `Piano formativo non pubblicabile` + `hourSummary` (vedi validazione).
  - **409** per revisione obsoleta o versione non in bozza.

```bash
curl -s -X POST $API/api/institution/formative-plan-versions/<VERSION_ID>/publish -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":2,"reason":"Approvazione"}'
```

### `POST /api/institution/formative-plan-versions/{id}/validate`
Verifica la coerenza del monte ore senza modificare nulla. **Ruoli:** InstitutionAdmin, Staff. Nessun body.
- **200** `FormativeHourSummary`
  ```json
  { "theoryPlannedMinutes": 1200, "internshipPlannedMinutes": 0, "totalPlannedMinutes": 1200, "assignedMinutes": 900, "missingMinutes": 300, "excessMinutes": 0,
    "issues": [ { "code": "theory.total-mismatch", "resourceId": "v1…" } ], "canPublish": false }
  ```

```bash
curl -s -X POST $API/api/institution/formative-plan-versions/<VERSION_ID>/validate -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/formative-plan-versions/{id}/hour-summary`
Uguale a `validate`, ma in `GET`: è lo stesso metodo applicativo. **Ruoli:** InstitutionAdmin, Staff.
- **200** `FormativeHourSummary`.

```bash
curl -s $API/api/institution/formative-plan-versions/<VERSION_ID>/hour-summary -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/course-editions/{id}/formative-plan`
Collega una versione **pubblicata** all'edizione corso. **Ruoli:** InstitutionAdmin.
- **Body** `BindFormativeVersion`: `eventId`, `versionId`, `reason`.
- **200** `FormativeReceipt` con `courseEditionId`.
- **Errori:** **404**, **409**: versione non pubblicata, percorso diverso, ecc.

```bash
curl -s -X POST $API/api/institution/course-editions/<EDITION_ID>/formative-plan -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","versionId":"<VERSION_ID>","reason":"Piano per edizione 2025"}'
```

### `GET /api/institution/course-editions/{id}/formative-plan`
Versione collegata all'edizione. **Ruoli:** tutti e quattro.
- **200** `FormativePlanSnapshot`.
- **Errori:** **404**: nessun piano collegato.

```bash
curl -s $API/api/institution/course-editions/<EDITION_ID>/formative-plan -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/course-editions/{id}/formative-plan/modules/{moduleId}`
Un singolo modulo del piano collegato all'edizione. **Ruoli:** tutti e quattro.
- **200** `FormativeModuleSnapshot`. **404**.

```bash
curl -s $API/api/institution/course-editions/<EDITION_ID>/formative-plan/modules/<MODULE_ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/formative-plans/{id}/history`
Storico del piano e delle sue versioni. **Ruoli:** InstitutionAdmin.
- **Query:** `page`, `size`.
- **200** `FormativeHistory[]`.

```bash
curl -s $API/api/institution/formative-plans/<PLAN_ID>/history -H "Authorization: Bearer $TOKEN"
```

---

## 26. Email istituzionali

Fonte: `InstitutionEmailController.cs`, `Application/Managers/InstitutionEmailManager.cs`, `Infrastructure/Repositories/InstitutionEmailRepository.cs`, `Business/Services/InstitutionEmailGenerator.cs`.

**Flag:** `SchoolApi` + `InstitutionEmail`. Body ≤ 16 KiB.

**Regole generali:**
- Il parametro `{category}` nel path è `Student` | `Teacher` | `Staff` | `InstitutionAdmin`.
- Lo Staff opera solo sulle categorie `Student` e `Teacher`.
- `reason`: 1–500 caratteri.
- Errori di regola → **409** `Operazione non consentita` (es. `"Policy non pubblicata o revision obsoleta."`, `"Indirizzo non disponibile; revisione amministrativa richiesta."`).
- **Gli indirizzi sono solo riservati nel registro**: il backend non crea caselle email reali (`mailboxVerified`, `domainVerified` e `loginEnabled` sono sempre `false`).

### `GET /api/institution/email-policies`
Ultima versione della policy di ciascuna categoria. **Ruoli:** InstitutionAdmin, Staff.
- **200** `EmailPolicyVersion[]`
  ```json
  [ { "policyId": "ep1…", "category": "Student", "revision": 2, "state": "Published",
      "template": { "localPartTemplate": "{nome}.{cognome}", "domain": "studenti.example.org", "names": { "case": "Lower", "accents": "LatinFoldV1", "wordSeparator": "", "punctuation": "Remove" }, "collision": "NumericSuffix", "numericSuffix": "{n}", "suffixStart": 2, "maximumAttempts": 32 },
      "actorUserId": "…", "actorMembershipId": "…", "createdAtUtc": "…", "domainVerificationState": "ConfiguredUnverified" } ]
  ```

```bash
curl -s $API/api/institution/email-policies -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/email-policies/{category}`
Policy di una categoria. **Ruoli:** InstitutionAdmin, Staff.
- **Query:** `revision` (facoltativo; senza, restituisce l'ultima).
- **200** `EmailPolicyVersion`. **404**.

```bash
curl -s "$API/api/institution/email-policies/Student?revision=2" -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/email-policies/{category}`
Crea la prima versione della policy. **Ruoli:** InstitutionAdmin.
- **Body** `EmailPolicyWrite`:
  - `eventId`.
  - `expectedRevision`: deve essere **0**, altrimenti 400.
  - `state`: `Draft` | `Published` | `Disabled`.
  - `template`: `EmailTemplate`.
  - `reason`.
- **Sintassi di `template.localPartTemplate`:**
  - testo letterale dot-atom;
  - placeholder `{nome}` e `{cognome}`;
  - trasformazioni concatenate con `:` → `:iniziale`, `:minuscolo`, `:maiuscolo`, `:primi(N)` (N da 1 a 64);
  - massimo 32 placeholder e 512 caratteri;
  - `{{` e `}}` per scrivere le graffe letterali.
  - Esempio: `{nome:iniziale}.{cognome}`.
- **200** `EmailPolicyVersion`.
- **Errori:** **400** `"Configurazione email non valida: …"`, **409**.

```bash
curl -s -X POST $API/api/institution/email-policies/Student -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":0,"state":"Published","reason":"Policy studenti","template":{"localPartTemplate":"{nome}.{cognome}","domain":"studenti.example.org","names":{"case":"Lower","accents":"LatinFoldV1","wordSeparator":"","punctuation":"Remove"},"collision":"NumericSuffix","numericSuffix":"{n}","suffixStart":2,"maximumAttempts":32}}'
```

### `PUT /api/institution/email-policies/{category}`
Aggiunge una nuova versione **immutabile** della policy. **Ruoli:** InstitutionAdmin.
- **Body** `EmailPolicyWrite` con `expectedRevision` ≥ 1, uguale alla revisione corrente.
- **200** `EmailPolicyVersion`.
- **Errori:** **400**, **409** `"Revision della policy obsoleta."`.

```bash
curl -s -X PUT $API/api/institution/email-policies/Student -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":2,"state":"Disabled","reason":"Sospensione","template":{"localPartTemplate":"{nome}.{cognome}","domain":"studenti.example.org","names":{}}}'
```

### `GET /api/institution/email-policies/{category}/history`
Storico della policy. **Ruoli:** InstitutionAdmin.
- **Query:** `page`, `size`.
- **200** `EmailHistory[]`.

```bash
curl -s $API/api/institution/email-policies/Student/history -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/email-policies/preview`
Genera un indirizzo di esempio. Non legge e non scrive il registro, quindi **non verifica la disponibilità**. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `EmailPreviewRequest`:
  - `category`.
  - `policyRevision`: facoltativo.
  - `firstName`, `lastName`.
  - `draft`: template non salvato, **solo InstitutionAdmin**.
- Lo Staff può fare l'anteprima solo di policy `Published`.
- **200** `GeneratedInstitutionEmail`
  ```json
  { "email": "giulia.verdi@studenti.example.org", "normalizedEmail": "giulia.verdi@studenti.example.org", "localPart": "giulia.verdi", "domain": "studenti.example.org", "warnings": [] }
  ```
- **Errori:** **400**: nome non convertibile o template non valido. **403**, **404**.

```bash
curl -s -X POST $API/api/institution/email-policies/preview -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"category":"Student","firstName":"Giulia","lastName":"Verdi"}'
```

### `GET /api/institution/email-assignments`
Indirizzi assegnati. **Ruoli:** tutti e quattro.
- **Visibilità:**
  - Admin: tutti gli indirizzi.
  - Staff: quelli di studenti e docenti.
  - Docente/studente: solo i propri.
- **Query:** `page`, `size`.
- **200** `EmailPage<EmailAssignment>`
  ```json
  { "pageNumber": 1, "pageSize": 20, "totalCount": 1, "items": [ { "id": "ea1…", "studentId": "4d5e…", "category": "Student", "policyRevision": 2, "email": "giulia.verdi@studenti.example.org", "state": "reserved", "revision": 1, "firstNameSnapshot": "Giulia", "lastNameSnapshot": "Verdi", "createdAtUtc": "…", "updatedAtUtc": "…", "mailboxVerified": false, "domainVerified": false, "loginEnabled": false } ] }
  ```

```bash
curl -s $API/api/institution/email-assignments -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/email-assignments/{id}`
**Ruoli:** tutti e quattro, con la stessa visibilità dell'elenco.
- **200** `EmailAssignment`. **404**.

```bash
curl -s $API/api/institution/email-assignments/<ID> -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/email-assignments`
Riserva un indirizzo per una persona esistente, applicando la policy pubblicata. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `EmailReserve`:
  - `eventId`, `category`.
  - `policyRevision`: deve essere la revisione **pubblicata** corrente.
  - `recipient`: uno tra `studentId`, `teacherId`, `membershipId`.
- **200** `EmailAssignment` (`state: "reserved"`).
- **Errori:**
  - **400**, **403**, **404**.
  - **409**: policy non pubblicata o obsoleta; indirizzo non disponibile.

```bash
curl -s -X POST $API/api/institution/email-assignments -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","category":"Student","policyRevision":2,"recipient":{"studentId":"<STUDENT_ID>","teacherId":null,"membershipId":null}}'
```

### `POST /api/institution/email-assignments/create-person`
Crea uno **studente o docente** e gli riserva l'indirizzo, in una sola operazione. **Ruoli:** InstitutionAdmin, Staff.
- **Body** `EmailCreatePerson`:
  - `eventId`.
  - `category`: `Student` | `Teacher`.
  - `policyRevision`: ≥ 1.
  - `firstName`, `lastName`: 1–100 caratteri.
  - `classId`: solo per `Student`.
- **200** `EmailAssignment`.
- **Errori:** **400**, **403**, **409**.

```bash
curl -s -X POST $API/api/institution/email-assignments/create-person -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","category":"Student","policyRevision":2,"firstName":"Giulia","lastName":"Verdi","classId":"<CLASS_ID>"}'
```

### `POST /api/institution/email-assignments/{id}/override`
Sostituisce manualmente l'indirizzo. **Ruoli:** InstitutionAdmin.
- **Body** `EmailOverride`:
  - `eventId`, `expectedRevision`.
  - `policyRevision`: policy pubblicata corrente.
  - `email`: nuovo indirizzo, non già riservato.
  - `reason`.
- **200** `EmailAssignment`.
- **Errori:** **400**, **404**, **409**.

```bash
curl -s -X POST $API/api/institution/email-assignments/<ID>/override -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":1,"policyRevision":2,"email":"g.verdi@studenti.example.org","reason":"Omonimia"}'
```

### `POST /api/institution/email-assignments/{id}/retire`
Ritira un indirizzo in stato `reserved`. **Ruoli:** InstitutionAdmin.
- **Body** `EmailRetire`: `eventId`, `expectedRevision`, `reason`.
- **200** `EmailAssignment` (`state: "retired"`).
- **Errori:** **404**, **409** `"Revision o stato non valido."`.

```bash
curl -s -X POST $API/api/institution/email-assignments/<ID>/retire -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":2,"reason":"Studente ritirato"}'
```

### `GET /api/institution/email-assignments/{id}/history`
**Ruoli:** InstitutionAdmin.
- **Query:** `page`, `size`.
- **200** `EmailHistory[]`.

```bash
curl -s $API/api/institution/email-assignments/<ID>/history -H "Authorization: Bearer $TOKEN"
```

---

## 27. Import anagrafiche

Fonte: `InstitutionImportController.cs`, `Application/Managers/InstitutionImportManager.cs`, `Infrastructure/Services/InstitutionImportFileReader*.cs`.

**Ruoli:** InstitutionAdmin, Staff. **Flag:** `SchoolApi` + `InstitutionImport`.

**Limiti:**
- body ≤ 256 KiB, tranne `preview`;
- le anteprime scadono dopo 30 minuti;
- al massimo 20 anteprime pendenti.

Codici di errore: [CONVENTIONS §6.3](CONVENTIONS.md#63-import-anagrafiche-e-calendario-code-nellestensione).

### `GET /api/institution/imports/templates/students.csv`
Scarica il modello CSV per l'import degli studenti.
- **200** `text/csv; charset=utf-8`, file `students.csv`. Le colonne sono generate dal server; il contenuto esatto non è stato verificato.

```bash
curl -s -OJ $API/api/institution/imports/templates/students.csv -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/imports/templates/teachers.csv`
Scarica il modello CSV per l'import dei docenti.
- **200** `text/csv`, file `teachers.csv`.

```bash
curl -s -OJ $API/api/institution/imports/templates/teachers.csv -H "Authorization: Bearer $TOKEN"
```

### `POST /api/institution/imports/preview`
Carica un file e crea l'anteprima. È **sincrono**: nessuna risposta 202.
- **Content-Type:** `multipart/form-data`.
- **Campi form:**
  - `file`: uno solo, `.csv` UTF-8 oppure `.xlsx`.
  - `settings`: stringa JSON `ImportPreviewSettings`, ≤ 262144 caratteri.
  - `parsing`: stringa JSON `ImportParsingOptions`, facoltativa, ≤ 2048 caratteri.
- **Rate limit:** al massimo 2 upload contemporanei per istanza (default).
- **201** `ImportBatch` + `Location`
  ```json
  { "id": "ib1…", "category": "Student", "state": "Preview", "revision": 0, "fileSha256": "…", "previewDigest": "…", "totalRows": 25, "validRows": 23, "invalidRows": 2,
    "createdAtUtc": "…", "expiresAtUtc": "…", "warnings": [], "columns": ["Nome","Cognome","Classe"], "mapping": [ { "columnIndex": 0, "field": "FirstName" }, { "columnIndex": 1, "field": "LastName" }, { "columnIndex": 2, "field": "Class" } ] }
  ```
- **Errori:**
  - **400** senza body: form non valido.
  - **400**, **413**, **415** con `code`.
  - **409** `PreviewQuotaExceeded`.
  - **429**: troppi upload contemporanei.

```bash
curl -s -X POST $API/api/institution/imports/preview -H "Authorization: Bearer $TOKEN" \
  -F "file=@studenti.csv" -F 'settings={"category":"Student","emailMode":"None"}' -F 'parsing={"delimiter":";","encoding":"utf-8"}'
```

### `GET /api/institution/imports`
Elenco degli import.
- **Query:** `page`, `size`.
- **200** `ImportBatchPage`.

```bash
curl -s $API/api/institution/imports -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/imports/{id}`
- **200** `ImportBatch`. **404**.

```bash
curl -s $API/api/institution/imports/<IMPORT_ID> -H "Authorization: Bearer $TOKEN"
```

### `GET /api/institution/imports/{id}/rows`
Righe validate dell'anteprima.
- **Query:** `page`, `size`.
- **200** `ImportRowsPage`
  ```json
  { "pageNumber": 1, "pageSize": 20, "totalCount": 25, "items": [ { "data": { "rowNumber": 2, "status": "Valid", "firstName": "Giulia", "lastName": "Verdi", "classReference": "CD25", "issues": [] }, "classId": "1a2b…" },
    { "data": { "rowNumber": 3, "status": "AmbiguousReference", "firstName": "Marco", "lastName": "Neri", "classReference": "CD", "issues": [ { "field": "Class", "code": "…", "message": "…", "warning": false } ] }, "classCandidates": ["1a2b…","6f7a…"] } ] }
  ```

```bash
curl -s "$API/api/institution/imports/<IMPORT_ID>/rows?page=1&size=50" -H "Authorization: Bearer $TOKEN"
```

### `PUT /api/institution/imports/{id}/mapping`
Ricalcola l'anteprima con un nuovo mapping colonne, la scelta delle classi ambigue o i candidati email.
- **Body** `ImportRemap`: `eventId`, `expectedRevision`, `settings` (`ImportPreviewSettings`).
- **200** `ImportBatch` con revisione aggiornata.
- **Errori:** **400** (`InvalidMapping`, `InvalidClassChoice`, `InvalidEmailCandidate`), **409** (`StalePreview`, `PreviewExpired`, `EventIdConflict`).

```bash
curl -s -X PUT $API/api/institution/imports/<IMPORT_ID>/mapping -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":0,"settings":{"category":"Student","classes":[{"rowNumber":3,"classId":"<CLASS_ID>"}]}}'
```

### `POST /api/institution/imports/{id}/confirm`
Conferma e crea le anagrafiche.
- **Body** `ImportConfirmation`:
  - `eventId`, `expectedRevision`.
  - `previewDigest`: valore dell'anteprima.
  - `acknowledgeWarnings`: deve essere `true` se ci sono warning.
  - `allowRepeatedFile`: `true` per reimportare lo stesso file, insieme a una `reason` obbligatoria.
- **200** `ImportResult`
  ```json
  { "importId": "ib1…", "revision": 2, "createdCount": 23, "rows": [ { "rowNumber": 2, "studentId": "4d5e…" } ] }
  ```
- **Errori:**
  - **400**: motivazione mancante.
  - **409**: `StalePreview`, `PreviewContextChanged`, `WarningsNotAcknowledged`, `RepeatedFileNotAuthorized`, `EmailAvailabilityChanged`, `EventIdConflict`, `AlreadyCompleted`, `PreviewExpired`.
  - **422** `RowsInvalid`.

```bash
curl -s -X POST $API/api/institution/imports/<IMPORT_ID>/confirm -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"eventId":"<UUID>","expectedRevision":1,"previewDigest":"<DIGEST>","acknowledgeWarnings":true}'
```

### `GET /api/institution/imports/{id}/result`
Risultato di un import confermato.
- **200** `ImportResult`.
- **Errori:** **404**, **409** `NotCompleted`.

```bash
curl -s $API/api/institution/imports/<IMPORT_ID>/result -H "Authorization: Bearer $TOKEN"
```

---

## 28. Riepilogo e verifica copertura

Conteggio ottenuto con `grep` degli attributi `[Http*]` nei controller, più i 2 endpoint minimal. Confrontato con le sezioni di questo documento.

| # | Controller / origine | Endpoint nel codice | Documentati | Sezione |
|---|---|---:|---:|---|
| 1 | `OperationalPipeline.MapOperationalEndpoints` | 2 | 2 | §1 |
| 2 | `IdentityRegistrationController` | 1 | 1 | §2 |
| 3 | `IdentityAccountController` | 3 | 3 | §2 |
| 4 | `InstitutionIdentityController` | 5 | 5 | §3 |
| 5 | `InstitutionInvitationController` | 3 | 3 | §4 |
| 6 | `InvitationAcceptanceController` | 1 | 1 | §4 |
| 7 | `InstitutionMembershipAdminController` | 5 | 5 | §5 |
| 8 | `InstitutionMembershipAuditController` | 2 | 2 | §6 |
| 9 | `InstitutionStudyAreaController` | 5 | 5 | §7 |
| 10 | `InstitutionStudyPathController` | 5 | 5 | §8 |
| 11 | `InstitutionBienniumController` | 5 | 5 | §9 |
| 12 | `InstitutionBienniumStudyAreaController` | 4 | 4 | §10 |
| 13 | `InstitutionBienniumStudyPathController` | 4 | 4 | §11 |
| 14 | `InstitutionCampusController` | 5 | 5 | §12 |
| 15 | `InstitutionClassController` | 5 | 5 | §13 |
| 16 | `InstitutionClassSubjectController` | 5 | 5 | §14 |
| 17 | `InstitutionCourseEditionController` | 11 | 11 | §15 |
| 18 | `InstitutionEnrollmentController` | 7 | 7 | §16 |
| 19 | `InstitutionStudentController` | 8 | 8 | §17 |
| 20 | `InstitutionStudentTransferController` | 2 | 2 | §18 |
| 21 | `InstitutionTeacherController` | 8 | 8 | §19 |
| 22 | `InstitutionSubjectController` | 8 | 8 | §20 |
| 23 | `InstitutionGradeController` | 9 | 9 | §21 |
| 24 | `InstitutionExamController` | 13 | 13 | §22 |
| 25 | `InstitutionSubmissionController` | 11 | 11 | §23 |
| 26 | `InstitutionCalendarController` | 22 | 22 | §24 |
| 27 | `InstitutionFormativePlanController` | 16 | 16 | §25 |
| 28 | `InstitutionEmailController` | 13 | 13 | §26 |
| 29 | `InstitutionImportController` | 9 | 9 | §27 |
| | **Totale** | **197** | **197** | |

Nel codice di produzione non ci sono endpoint per le **operazioni di piattaforma**: creazione di un istituto, primo amministratore, recovery. Esiste un servizio applicativo (`IInstitutionGovernanceManager` / `PlatformAsync`) ma nessuna route lo espone. Vedi [OPEN_QUESTIONS Q-10](OPEN_QUESTIONS.md#q-10).
