# PlatformOperator, MT-5C e MT-5D

La decisione del proprietario del sistema sostituisce il precedente blocco sul
modello dell'autorità. PlatformOperator è control plane, non User.Role, root SQL,
InstitutionAdmin o un JWT didattico. Nessuna route HTTP espone operazioni firmate.

## Registrazione, firma e revoca

Il proprietario installa fuori dal repository la configurazione amministrativa
(esempio senza segreti in tools/PlatformAdmin/admin.example.json). Registro Keys:
OperatorId esplicito, KeyId univoco, PublicKeyPem **solo pubblica RSA >=3072**,
NotBeforeUtc/NotAfterUtc UTC, Revoked e Actions consentite. Enabled è false di
default. Gli utenti applicativi e il service account web non devono poter scrivere
il registro, i suoi genitori o l'adapter. ACL: SYSTEM/amministratori/proprietario
con scrittura; operatori autorizzati con sola lettura. La CLI rifiuta scritture
concesse a Everyone/Users/Authenticated Users/Guests/IIS_IUSRS. Un amministratore OS
o DBA che modifica binari, registro o dati è fuori dal confine dell'autenticazione
applicativa; root SQL non viene per questo accettato come attore di provisioning.

La chiave privata resta nel certificato Windows CurrentUser/My, preferibilmente
CNG non esportabile con ACL dedicate. Creazione e registrazione sono due azioni
esplicite del proprietario; il signer non aggiunge chiavi al registro. La CLI
`sign <thumbprint> <unsigned-private.json> <signed-private.json>` firma tramite il
keystore OS senza esportare la chiave. Esecuzione:

```
dotnet run --project tools/PlatformAdmin -- sign THUMBPRINT unsigned-private.json signed-private.json
dotnet run --project tools/PlatformAdmin -- trusted-admin-config.json signed-private.json
```

Tutti questi file runtime vanno fuori dal repository o in private/ con ACL; il
documento firmato è una capability breve e non va pubblicato. JSON sconosciuto o
con proprietà duplicate è rifiutato dall'esecutore. Configurazione caricata da
percorso esplicito, mai derivata dal payload o auto-registrata dal richiedente.

Firma RSA-PSS/SHA256 con dominio ElectronicRegister.Platform.v1 e serializzazione
tipizzata di operator/key/event/nonce/tempi/action/intent. Nonce casuale 32 byte
esadecimale mai riusato; finestra massima 5 minuti. Registro e firma rivalidati
sotto lock e prima del commit. Nonce consumati persistenti con unicità per operatore,
anche tra chiavi ruotate. Replay esatto negato. Retry idempotente: stesso EventId/
intento/operatore, **nuovo nonce e nuova firma valida**; risultato storico, nessuna
seconda mutazione. EventId riusato con intento diverso è conflitto.

Rotazione: il proprietario registra nuova chiave/KeyId con finestra e diritti
espliciti, distribuisce l'accesso al certificato, poi revoca la vecchia. Revoked o
scadenza negano nuove operazioni, anche retry. Non rimuovere gli eventi/nonces.
La revoca di una chiave non riscrive approvazioni storiche: gli inviti già approvati
richiedono revoca esplicita separata se l'approvazione stessa è contestata.

## Prova amministrativa e destinatario

Provision è l'attestazione firmata dell'operatore della verifica del referente e
dell'identità dichiarata, con riferimento obbligatorio alle evidenze e motivo.
Il sistema non controlla automaticamente registri legali e non deduce autorità dal
codice o dominio email. Nessun documento personale/token è copiato nell'audit:
rimane un riferimento nel sistema documentale amministrativo protetto.

Il recipient UserId è scelto esplicitamente, mai cercato/associato automaticamente
per coincidenza email. Institution resta Provisioning e senza ruoli finché quel
User si autentica con selection ticket autentico e presenta il challenge personale
consegnato sul suo canale. Il challenge è 256 bit, solo hash SHA256 nel server,
TTL 15 minuti, single-use con replay idempotente dello stesso evento. Cambio email,
destinatario diverso, token scaduto/revocato o vecchio challenge dopo reissue negati.
Creazione membership/ruoli, consumo, stato Active e audit condividono la transazione;
generation avanzata esclusivamente dai trigger esistenti.

Il canale è un IInvitationChannel installato dal proprietario, non un bool nel
body. Default ClosedInvitationChannel: senza adapter affidabile operativo tutti i
percorsi che richiedono consegna/prova rimangono chiusi. Un adapter opzionale è
caricato dall'host solo con AssemblyPath, SHA256 e Type amministrativi: i byte
verificati vengono caricati da stream. L'operatore non lo sceglie nella richiesta.
Nessun provider produttivo o destinazione di notifica è configurato da questa fase;
il provider di test vive solo nella fixture e conserva i challenge in memoria.

Consegna prima del commit: se fallisce la transazione, il link consegnato è
inutilizzabile. Nessun token plaintext/outbox persistito. La riemissione autorizzata
genera un challenge nuovo e invalida il precedente; il callback del provider deve
essere cancellabile e non loggare il segreto. Non promettere delivery esattamente
una volta attraverso un servizio esterno.

## Inviti istituzionali e confini

POST api/institution/admin/invitations, POST {id}/revoke e {id}/reissue: solo
InstitutionAccess + InstitutionAdmin attuale rivalidato sotto mutex, gate e flag
MembershipAdminApi/MembershipWriteApi/Onboarding chiusi per default. Nessuna self
invitation, nessun rejoin Left/Pending, nessuna modifica a membership già presenti.
Ruoli esatti InstitutionAdmin/Teacher/Student. Risposta neutra per destinatari
assenti o già membri, nessun lookup pubblico email. Rate limit persistente 5 eventi
per attore/tenant/minuto; challenge blocca dopo 5 prove errate, reissue lo ruota.

POST api/Auth/invitations/{id}/accept richiede il vero selection ticket; non accetta
JWT globale o InstitutionAccess come sostituto. InstitutionId nel body è un locator
non autorevole: lookup invitation owner-scoped e verifica del recipient/proof, mai
attribuzione di privilegi dall'identificativo. Nessun token in risposta o audit.
Le mutazioni umane IAM esistenti restano separate dal ledger tecnico onboarding;
nessun ActorMembershipId fittizio per PlatformOperator o destinatario pre-nomina.
