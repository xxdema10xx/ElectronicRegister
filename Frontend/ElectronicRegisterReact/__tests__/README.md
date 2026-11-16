# Test del frontend

Jest (`jest-expo`) e React Native Testing Library. Si lanciano con `npm test`. Non serve il backend: ogni test simula le risposte del server.

## Cartelle

| Cartella | Cosa verifica |
|---|---|
| `utils/` | Date, password, ricerca, messaggi d'errore degli esami, posizione dei tooltip, nome dell'utente, archivio locale per utente (app e web) |
| `api/` | Sessione e token, errori HTTP tradotti in messaggi, login e scelta dell'istituto, anagrafiche, voti, membri, classi, orario, esami (contratto, API, "chi deve recuperare", libretto), import del calendario |
| `components/` | Dialog dell'app, campi e liste, editor degli esami (regole e interazione), svolgimento dell'esame, consegne e correzione, voti di uno studente |
| `screens/` | Le schermate per ruolo: accesso, anagrafiche, membri, profilo, voti, classi, home, calendario, esami del docente e dello studente |
| `navigation/` | Sezioni per ruolo, guardia sulle modifiche non salvate, rotte degli esami; su web URL, indietro/avanti del browser e menu su finestra stretta |
| `app/` | Flusso completo: login, scelta dell'istituto, uscita, sessione scaduta, rinnovo del token |

## Convenzioni

- **Ambiente**: i test girano come app nativa (iOS). Le parti solo web si provano impostando `Platform.OS = "web"` prima di caricare il modulo (vedi `navigation/webNavigation.test.js` e `utils/userStorage.test.js`).
- **Fuso orario**: `Europe/Rome`, fissato in `jest.globalSetup.js`, così le conversioni tra UTC (server) e ora locale (interfaccia) sono uguali su ogni macchina.
- **Server**: nei test delle API si sostituiscono i metodi di `axiosClient` (`jest.spyOn`); nei test delle schermate si simula il servizio intero (`jest.mock("../../src/api/…")`).
- **Helper** (`helpers/render.js`):
  - `renderAs(ruolo, ui)` rende con un utente collegato (`admin`, `staff`, `teacher`, `student`) e con il contenitore dei dialog;
  - `pressDialog(testo)` preme un pulsante del dialog dell'app;
  - `pressAlertButton(testo)` preme un pulsante dell'`Alert` di sistema;
  - `flush()` aspetta i caricamenti iniziali.
- **Mock comuni** (`jest.setup.js`): icone, `expo-crypto`, selettore di file, login Microsoft, margini dello schermo, `Alert.alert`.
