# MT-5C: precedente blocco di autorità — decisione sostituita

La decisione esplicita del proprietario approva ora il PlatformOperator separato,
operazioni firmate via CLI e provider di challenge affidabile. Il blocco storico
sotto non descrive più il modello corrente: vedere
[PLATFORM_OPERATOR_AND_ONBOARDING](PLATFORM_OPERATOR_AND_ONBOARDING.md).
Il provider produttivo e le chiavi reali restano azioni amministrative esterne;
il loro mancato setup mantiene il percorso operativo chiuso per default.

L'implementazione privilegiata di onboarding è bloccata da una decisione di
sicurezza esplicita, non da un problema ordinario di codice.

Fonti attuali:

- `INSTITUTION_MEMBERSHIP_GOVERNANCE.md`, «Audit» e «Onboarding e inviti proposti»:
  «scelta del modello di attore tecnico ancora da approvare»; «Decisioni ancora
  necessarie: autorita legale di provisioning e primo admin, autenticazione forte
  e recupero», ruoli concedibili, doppio controllo, durata/challenge/riemissione.
- `MULTITENANCY_DESIGN.md`, «Onboarding e separazione dei poteri»: PlatformOperator
  come control plane distinto, nessun diritto didattico e nessun self-service admin.
- `INSTITUTION_MEMBERSHIP_MUTATIONS_AND_AUDIT.md`, «Decisione necessaria»:
  il writer tecnico non è attribuibile a root, primo admin, target o User.Role;
  il suo attore deve essere distinto e verificato, non una membership fittizia.

Il codice non contiene principal/policy/verificatore PlatformOperator o attestazione
di verifica del referente, né un servizio di prova possesso email riusabile per
accettare la prima nomina. InstitutionRepository.Add rimane Provisioning-only;
i writer tecnici non diventano writer amministrativi. Il ledger IAM D richiede
attore umano con membership: non può ospitare una nomina di bootstrap fingendo
che l'attore sia già membro. Nessun percorso privilegiato introdotto in questa fase.

Decisione necessaria per proseguire in modo implementabile:

1. Chi autorizza provisioning/primo admin; identità e trust anchor del control plane,
   autenticazione forte, revoca/recupero e verifica server-side non falsificabile.
2. Quali evidenze attestano Fondazione/referente/delega e chi le approva; se il doppio
   controllo proposto è obbligatorio; riferimento verificabile e retention delle prove.
3. Contratto dell'attore e audit tecnico separato: identificatore, responsabilità,
   scope, idempotenza e correlazione; nessun ActorMembershipId inventato.
4. Verifica del destinatario e sfida invito: canale affidabile, TTL/rate limit,
   riemissione/revoca, email cambiata e ruoli invitabili, in particolare InstitutionAdmin.

La prima membership/admin/attivazione dovrà essere atomica con quelle verifiche e
audit appropriato; non è sicuro inventare un `approved=true`, una nuova chiave locale
o attribuire il potere al JWT globale. Gli inviti non sono implementati tramite il
writer tecnico come scorciatoia. LegacyEndpointGate e rollout restano chiusi;
nessuna nuova route, ruolo globale o modifica SQL MT-5C è stata introdotta.
