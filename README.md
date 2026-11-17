# ElectronicRegister

Registro elettronico: backend .NET in `Backend/`, app React Native/Expo in `Frontend/ElectronicRegisterReact/`.

## Backend

.NET 10 / ASP.NET Core / EF Core DB-first, MySQL 5.7.24.

Per un nuovo computer partire da [LOCAL_DATABASE_SETUP](docs/development/LOCAL_DATABASE_SETUP.md):
bootstrap versionato solo su istanza di test separata, senza dump o file privati del
vecchio PC. Non rieseguire i runner storici sullo schema consolidato e non usare la 3306.

Il [PlatformOperator](docs/architecture/PLATFORM_OPERATOR_AND_ONBOARDING.md) usa
operazioni firmate via CLI e non ottiene privilegi didattici. Rollout chiuso per
default; chiavi reali e provider di challenge sono configurazione del proprietario.

Governance, audit in lettura e recupero con doppia approvazione: [contratto MT-5D](docs/architecture/INSTITUTION_GOVERNANCE_AND_RECOVERY.md).

Organizzazione del backend e controlli Roslyn: [guida](tools/CodeOrganization/README.md), [report del refactoring](docs/refactoring/CODE_ORGANIZATION_REPORT.md).

Cutover istituzionale completato: [contratti API correnti](docs/refactoring/INSTITUTIONAL_API_AND_LEGACY_CUTOVER.md), [report conclusivo](docs/refactoring/RESPONSIBILITY_REFACTORING_REPORT.md) e [regressione finale](docs/refactoring/validation/current-cutover/final/README.md). Le API globali dei vecchi frontend sono ritirate; i report delle fasi MT/PHASE precedenti restano evidenze storiche.

## Frontend

App Expo (web, Android, iOS) in [`Frontend/ElectronicRegisterReact/`](Frontend/ElectronicRegisterReact/README.md).

```bash
cd Frontend/ElectronicRegisterReact
npm ci
npm test        # test Jest
npm run test:coverage   # test Jest con report di copertura
npm run web     # avvio in browser (expo start --web)
```

Configurazione dell'indirizzo API e del proxy di sviluppo: `.env.example` e il [README del frontend](Frontend/ElectronicRegisterReact/README.md), che spiega anche come pubblicare la build web (ogni percorso rimandato a `index.html`).

## Documentazione

- Contratti API correnti (fonte autorevole): [`docs/api/`](docs/api/)
- Architettura, sviluppo, operazioni, qualità, refactoring: [`docs/architecture/`](docs/architecture/), [`docs/development/`](docs/development/), [`docs/operations/`](docs/operations/), [`docs/quality/`](docs/quality/), [`docs/refactoring/`](docs/refactoring/)
- Guida al backend per il frontend (snapshot precedente): [`docs/README.md`](docs/README.md), con [API](docs/API.md), [modelli](docs/MODELS.md), [autenticazione](docs/AUTH.md), [convenzioni](docs/CONVENTIONS.md), [flussi](docs/FLOWS.md), [checklist frontend](docs/FRONTEND_CHECKLIST.md) e [punti aperti](docs/OPEN_QUESTIONS.md)
