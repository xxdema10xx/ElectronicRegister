> Aggiornamento MT5B: **PARZIALE, scritture non implementate e chiuse**. Baseline
> nuova 711/711 Passed su MAMP MySQL 5.7.24 isolato 13306; nessun DDL o codice
> applicativo modificato. Restano da definire il contratto attribuito dei writer
> tecnici MT1 e da verificare un contatore monotono compatibile con INSERT SELECT/
> DELETE JOIN esistenti. Nessuna garanzia Last Admin/audit/revoca monotona nuova.
> [Design e blocchi MT5B](../architecture/INSTITUTION_MEMBERSHIP_MUTATIONS_AND_AUDIT.md),
> [report MT5B](../../tests/Integration/MULTITENANCY_MT5B_REPORT.md).
> MT5A resta completata (due711/711); le precedenti descrizioni PARZIALE sotto sono
> storiche. Rollout chiuso, nessuna nuova POST o API audit, nessun MT5C/cutover.

# Contratti directory membership — MT-5A

Implementazione read-only; certificazione MT5A **PARZIALE**, rollout chiuso. Vedere il
[report](../../tests/Integration/MULTITENANCY_MT5A_REPORT.md) e la
[governance](../architecture/INSTITUTION_MEMBERSHIP_GOVERNANCE.md).

Usare il flusso MT2: identity/login -> selection ticket -> select-institution -> JWT
InstitutionAccess. Solo InstitutionAdmin della membership corrente, membership e Institution
Active; ruoli riletti ad ogni richiesta. Teacher/Student e admin legacy non sono autorizzati.
Multitenancy:MembershipAdminApi:Enabled server-side default false e indipendente da SchoolApi.
Non inviare flag o tenant per ottenere autorizzazione; cambiare istituto richiede un nuovo token.

| Metodo | Route | Risposta |
| --- | --- | --- |
| GET | /api/institution/admin/members | 200 InstitutionMemberDto[], anche [] |
| GET | /api/institution/admin/members/{membershipId:guid} | 200 InstitutionMemberDto; 404 assente/fuori tenant |

Query della lista:

| Parametro | Regola |
| --- | --- |
| pageNumber | intero >=1, default1; offset deve rientrare in Int32 |
| pageSize | intero 1..100, default20 |
| status | opzionale, esattamente pending / active / suspended / left |
| role | opzionale, esattamente InstitutionAdmin / Teacher / Student |
| search | opzionale, sottostringa letterale dell'Email locale, trim, 1..100 caratteri prima del trim |

Filtri combinati con AND. Filtri presenti ma vuoti/bianchi sono 400. Nessun count/total/header
di conteggio; pagina corta indica fine alla lettura corrente. Ordine JoinedAt, MembershipId,
deterministico a dati invariati; nessuno snapshot garantito fra pagine durante modifiche concorrenti.
La collation DB governa maiuscole/accenti della ricerca. Non esiste ricerca globale per email.

Esempio di elemento (JoinedAt e il timestamp persistito, non data di invito):

```json
{
  "membershipId": "11111111-1111-4111-8111-111111111111",
  "userId": "22222222-2222-4222-8222-222222222222",
  "email": "membro@example.test",
  "status": "active",
  "roles": ["InstitutionAdmin", "Teacher"],
  "joinedAt": "2026-01-01T00:00:00Z"
}
```

SuspendedAt presente come UTC quando disponibile, omesso quando null (configurazione JSON
esistente). Roles e il set corrente ordinato ordinalmente; nessun ruolo implica automaticamente
un profilo scolastico. UserId puo essere uguale in altre fondazioni, MembershipId rimane distinto.
Nome visualizzato non disponibile su User, quindi omesso. Nessun profilo, hash password, ruolo
legacy, token o segreto. Email visibile solo per identita gia associate al tenant selezionato.

200 letture valide; 400 filtri/binding invalidi; 401 assenza/token non valido, legacy, selection,
scaduto; 403 ruolo/stato/revoca/rollout; 404 GUID valido assente o esterno con stesso corpo pubblico.
Un segmento non GUID non corrisponde alla route dettaglio (404). Errori sanitizzati, nessun SQL.
Un ruolo aggiunto dopo emissione invalida il vecchio JWT quanto un ruolo rimosso: ripetere selezione
istituto soltanto se ancora autorizzati, senza tentare fallback al login legacy.

Non memorizzare la directory in cache condivisa fra tenant; svuotare UI/dati al cambio istituto
o 401/403. I campi di identita non sono una schermata modifica account. Nessun POST/PUT/PATCH/
DELETE, invito, provisioning, revoca o trasferimento introdotto. Non usare /api/Users legacy come
alternativa. Nessuna modifica frontend inclusa nel checkpoint.
