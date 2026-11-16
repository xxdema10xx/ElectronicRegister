# Guida al codice di ElectronicRegister (per chi non ha mai programmato)

Questa guida spiega **cosa fa il progetto**, **come è organizzato** e **cosa significano le parole strane** che vedi nel codice.
Nei file commentati trovi rimandi tipo `(vedi GUIDA §4.2)`: significa "la spiegazione generale è qui, non la ripeto ogni volta".

---

## 1. Cosa fa il progetto

È un **registro elettronico** per un istituto (ITS Umbria): docenti, studenti e segreteria accedono per gestire classi, materie,
lezioni, voti ed esami.

Il progetto è fatto di **due metà che parlano tra loro**, come un ristorante:

| Metà | Cartella | Paragone | Linguaggio |
|---|---|---|---|
| **Frontend** (quello che l'utente vede: schermate, pulsanti) | `Frontend/ElectronicRegisterReact` | la **sala** con i camerieri | JavaScript (React Native / Expo) |
| **Backend** (la logica e il database) | `Backend/ElectronicRegisterAPI` | la **cucina** | C# (.NET 10) |

Il frontend non tocca mai il database: **chiede** al backend ("dammi i voti di Mario") e il backend risponde.
Queste richieste si chiamano **richieste HTTP** (le stesse che fa il browser quando apri un sito) e le "porte" a cui bussare si
chiamano **API** o **endpoint**, per esempio `GET /api/institution/grades`.

---

## 2. Mappa delle cartelle

```
ElectronicRegister/
├── Frontend/
│   ├── ElectronicRegisterReact/   App vera (web + telefono). È quella "viva".
│   │   ├── index.js               Punto di partenza: avvia l'app
│   │   ├── App.js                 Cervello: gestisce login, sessione, quale schermata mostrare
│   │   └── src/
│   │       ├── api/               Funzioni che parlano col backend (login, voti, esami…)
│   │       ├── screens/           Le "pagine" intere (Login, Voti, Classi, Esami…)
│   │       ├── components/        Pezzi riusabili (bottone, carta, campo di testo…)
│   │       ├── hooks/             Piccole utilità condivise (es. "chi è l'utente loggato?")
│   │       ├── navigation/        Il menu e il passaggio da una pagina all'altra
│   │       ├── constants/         Colori e stili (theme.js)
│   │       └── utils/             Funzioni di supporto
│   └── ElectronicRegisterWeb/     Vecchio frontend web (HTML/JS semplice)
├── Backend/ElectronicRegisterAPI/src/
│   ├── Api/            "Reception": riceve le richieste HTTP (Controllers) e controlla chi sei
│   ├── Application/    "Direttori di reparto" (Managers): coordinano un'operazione intera
│   ├── Business/       "Regole del mestiere" (Services): es. "un voto è tra 1 e 10"
│   ├── Domain/         Il vocabolario: modelli, DTO, regole, permessi, interfacce
│   └── Infrastructure/ Parla col database (Repositories), emette i token di accesso
├── tests/              Programmi che controllano che il codice funzioni (Unit, Integration)
├── migrations/         Script SQL che creano/modificano le tabelle del database
├── docs/               Documentazione tecnica già scritta dal team (AUTH.md, API.md, FLOWS.md…)
├── tools/              Script di servizio (migrazioni, controlli di qualità, ecc.)
├── infra/              Configurazione per mettere online il server (Docker, nginx)
└── .github/workflows/  Automazioni: a ogni modifica GitHub esegue test e controlli
```

### Il percorso di una richiesta nel backend (a strati)

Esempio: "il docente salva un voto".

```
Schermata  →  api/gradeService.js  →  (Internet)  →  Controller  →  Manager  →  Service  →  Repository  →  Database
 (React)       (frontend)                            (Api)        (Application) (Business)  (Infrastructure)  (MySQL)
```

1. **Controller** (`Api/Controllers`): riceve la richiesta, verifica che tu abbia il ruolo giusto, passa il lavoro avanti.
2. **Manager** (`Application/Managers`): orchestra: "prima controlla questo, poi salva quello".
3. **Service** (`Business/Services`): applica le regole ("il voto deve essere tra 1 e 10", "il docente insegna davvero quella materia").
4. **Repository** (`Infrastructure/Repositories`): l'unico che scrive/legge il database.
5. **Domain**: contiene i "mattoncini" usati da tutti gli strati (DTO, regole, permessi).

Ogni strato fa **una cosa sola**: così, se cambia una regola, si tocca un solo posto.

### Chi è "l'istituto" (multi-tenancy)
Il sistema può servire **più scuole**. Ogni dato appartiene a un istituto e un utente vede solo i dati del **suo** istituto.
Per questo dopo il login si sceglie l'istituto (vedi §6).

---

## 3. Concetti comuni a tutti i linguaggi

- **Variabile**: una scatola con un nome che contiene un valore. `const nome = "Mario"` → la scatola `nome` contiene "Mario".
- **Funzione**: una ricetta con un nome. La definisci una volta e la "chiami" quando serve: `login(email, password)`.
- **Parametri**: gli ingredienti che passi alla funzione (`email`, `password`).
- **`return`**: il piatto finito che la funzione restituisce.
- **`if / else`**: "se succede X fai A, altrimenti fai B".
- **Oggetto**: un gruppo di dati con etichette: `{ nome: "Mario", eta: 20 }`. Si legge con il punto: `utente.nome`.
- **Array / Lista**: elenco ordinato: `[1, 2, 3]`.
- **Commento**: testo ignorato dal computer, scritto per gli umani. In JS e C# inizia con `//`.
- **`;` (punto e virgola)**: la "fine frase" del codice.
- **`{ }` (graffe)**: racchiudono un blocco di istruzioni che stanno insieme.
- **Bug**: errore nel codice. **Debug**: cercarlo e correggerlo.

---

## 4. JavaScript / React: le parole che incontrerai nel frontend

### 4.1 Le basi del linguaggio

| Scrivi | Significa |
|---|---|
| `import X from "./file"` | "Porta qui X, che è scritto in un altro file". Serve a non scrivere tutto in un unico file gigante. |
| `import { A, B } from "…"` | Come sopra, ma prendi solo i pezzi A e B dal file. |
| `export default function X` | "Questo file mette a disposizione X agli altri file" (uno solo per file, il principale). |
| `export function Y` / `export const Z` | Come sopra, ma se ne possono esportare quanti si vuole; chi importa deve usare le graffe `{ Y }`. |
| `const x = …` | Crea una variabile che **non può essere riassegnata**. |
| `let x = …` | Crea una variabile che **può cambiare** valore. |
| `function f() { … }` | Definisce una funzione. |
| `(a) => a + 1` | **Funzione freccia**: scrittura corta di una funzione. "Dato a, restituisci a+1". |
| `` `Ciao ${nome}` `` | **Template string**: testo con dentro un valore. Se nome="Mario" → "Ciao Mario". |
| `a ?? b` | "usa `a`; se è vuoto (null/undefined) usa `b`". |
| `a \|\| b` | "usa `a`; se è vuoto/falso usa `b`". |
| `a && b` | "se `a` è vero allora fai/mostra `b`, altrimenti niente". |
| `cond ? x : y` | **Operatore ternario**: "se cond è vera vale x, altrimenti y". |
| `user?.id` | **Optional chaining**: "se `user` esiste dammi `id`, altrimenti non andare in errore: dammi `undefined`". |
| `const { a, b } = oggetto` | **Destructuring**: estrae `a` e `b` dall'oggetto in due variabili. |
| `{ ...utente, nome: "X" }` | **Spread**: copia tutto `utente` e poi cambia solo `nome`. |
| `===` / `!==` | "è esattamente uguale / diverso". |
| `!x` | "NON x" (nega). `!!x` lo trasforma in vero/falso. |

### 4.2 Codice asincrono: `async`, `await`, `Promise`
Chiedere dati al backend **richiede tempo** (come ordinare al ristorante). Il programma non deve restare bloccato ad aspettare.

- **`Promise`** (promessa): "ti darò un risultato più tardi". Può finire bene o male.
- **`async function`**: funzione che può "aspettare" cose lente.
- **`await`**: "fermati **in questa funzione** finché la risposta non arriva". Il resto dell'app resta libero.
- **`.then(…)`**: "quando va bene, fai questo".
- **`.catch(…)`**: "se va male, fai questo".
- **`.finally(…)`**: "in ogni caso, alla fine, fai questo" (es. togliere la rotellina di caricamento).
- **`try { … } catch (e) { … } finally { … }`**: stessa idea scritta in blocchi: *prova* → *se errore, gestiscilo* → *alla fine pulisci*.

### 4.3 React: costruire schermate a pezzi

- **Componente**: una funzione che **restituisce un pezzo di schermata** (es. `Btn` è un bottone). Il nome inizia **sempre con la maiuscola**.
- **JSX**: la scrittura che sembra HTML dentro il JavaScript: `<Text>Ciao</Text>`. Le graffe `{ }` dentro il JSX inseriscono codice JS: `<Text>{nome}</Text>`.
- **Props**: i "parametri" di un componente, passati come attributi: `<Btn label="Entra" onPress={submit} />`.
  `label` e `onPress` sono props. Servono a rendere un componente riusabile con testi/azioni diversi.
- **`style`**: l'aspetto (colori, margini…). Come CSS ma scritto come oggetto.
- **Hook** (`useQualcosa`): strumenti speciali di React. Regola: si chiamano solo all'inizio di un componente, mai dentro `if`.
  - **`useState`**: la **memoria** del componente. `const [email, setEmail] = useState("")` crea una variabile `email` (valore attuale,
    inizialmente testo vuoto) e una funzione `setEmail` per cambiarla. **Quando la cambi con `setEmail`, React ridisegna la schermata.**
    Cambiare `email = "x"` direttamente NON funzionerebbe.
  - **`useEffect(() => {…}, [dipendenze])`**: "esegui questo codice **dopo** che la schermata è comparsa, e ogni volta che cambia
    qualcosa nella lista `[dipendenze]`". Con lista vuota `[]` → una volta sola all'avvio. La funzione che restituisce (`return () => …`)
    è la "pulizia" eseguita quando il componente sparisce.
  - **`useRef`**: una scatola che **ricorda un valore ma non ridisegna** la schermata quando cambia.
  - **`useCallback`**: ricorda una funzione per non ricrearla a ogni disegno (ottimizzazione; non serve capirla a fondo).
  - **`useContext` / `createContext`**: una "bacheca" da cui qualunque componente può leggere dati comuni (es. l'utente loggato)
    senza passarli a mano di componente in componente.
- **Render / ridisegno**: React richiama la funzione del componente ogni volta che cambia uno stato, e aggiorna ciò che si vede.

### 4.4 Pezzi di React Native (le "tessere" grafiche)
`View` = scatola/contenitore · `Text` = testo · `Image` = immagine · `ScrollView` = area scorrevole ·
`TouchableOpacity` = qualcosa su cui si tocca/clicca (usato per i bottoni) · `ActivityIndicator` = rotellina di caricamento ·
`Alert.alert(titolo, messaggio)` = finestrella di avviso · `SafeAreaView` = evita che il contenuto finisca sotto il notch/barre del telefono.

### 4.5 axios
**axios** è la libreria che invia le richieste HTTP al backend. `axiosClient.get(url)` = "chiedi", `.post(url, dati)` = "invia/crea",
`.put` = "modifica", `.delete` = "elimina".
Gli **interceptors** sono "controllori alla porta" che vedono ogni risposta prima delle schermate.

---

## 5. C#: le parole che incontrerai nel backend

| Scrivi | Significa |
|---|---|
| `using X;` | Come `import`: "uso gli strumenti definiti in X". |
| `namespace A.B;` | L'"indirizzo" del file, per evitare confusioni tra nomi uguali. |
| `class Nome { … }` | Un **modello/progetto** di oggetto, con dati e azioni. |
| `public` / `private` | Visibile a tutti / solo dentro questa classe. |
| `static` | Appartiene alla classe, non serve crearne un esemplare (`GradeRules.EnsureValidValue(…)`). |
| `sealed` | "Nessuno può derivare da questa classe". |
| `{ get; set; }` | **Proprietà**: un dato che si può leggere (`get`) e scrivere (`set`). |
| `Guid` | Identificatore unico lungo e casuale (es. `3f2504e0-…`): è l'"id" di ogni cosa. |
| `decimal` / `int` / `string` / `bool` | Numero con virgola / intero / testo / vero-falso. |
| `DateOnly` | Una data senza orario. |
| `Guid?` | Il `?` significa "può anche essere vuoto (null)". |
| `Task<T>` + `async` + `await` | Come in JavaScript (§4.2): operazione lenta che alla fine dà un `T`. |
| `var x = …` | Il computer deduce il tipo da solo. |
| `x is { } y` | "se `x` non è vuoto, mettilo in `y` e prosegui". |
| `=>` | Scrittura corta: "fa/restituisce". `Metodo() => Ok(x);` equivale a `{ return Ok(x); }`. |
| `throw new …Exception(…)` | **Lancia un errore** interrompendo l'operazione. |
| `interface IQualcosa` | Un **contratto**: elenca cosa una classe deve saper fare, senza dire come. |
| `[Qualcosa]` tra quadre | **Attributo**: un'etichetta sopra una classe/metodo che dà istruzioni al sistema (es. `[HttpGet]` = "questo metodo risponde a GET"). |
| `/// <summary>` | Commento di documentazione. |

### 5.1 Dependency Injection ("iniezione delle dipendenze")
Un controller ha bisogno di un manager; un manager di un repository. Invece di crearseli da solo, **li chiede nel costruttore** e il
sistema glieli consegna già pronti:

```csharp
public sealed class InstitutionGradeController(IInstitutionExamManager manager)
```
Qui il controller dice "per lavorare mi serve un `IInstitutionExamManager`" e il framework glielo passa.
Dove si decide *quale* classe consegnare? In file `ServiceCollectionExtensions.cs` (cerca `AddScoped`/`AddSingleton`).
Vantaggio: nei test si possono consegnare versioni finte.

### 5.2 Controller e route
- `[Route("api/institution/grades")]` = indirizzo base del controller.
- `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]` = tipo di richiesta (leggi, crea, modifica, elimina).
- `[Authorize(Roles = "…")]` = "solo chi ha uno di questi ruoli entra". Ruoli: `InstitutionAdmin`, `Staff`, `Teacher`, `Student`.
- `[InstitutionPermission(…)]` = controllo più fine su un permesso specifico (es. `GradeCreate`).
- `ActionResult` / `Ok(...)` (200) / `NotFound()` (404) / `NoContent()` (204) / `CreatedAtAction(...)` (201): le risposte standard.

### 5.3 DTO
**DTO** = *Data Transfer Object*: una "scheda" con solo i campi che viaggiano tra frontend e backend (es. `CreateGradeDto`).
Non coincide con la tabella del database: così non si espone più del necessario.

### 5.4 Codici di stato HTTP (le risposte del backend)
200 OK · 201 creato · 204 ok senza contenuto · 400 richiesta sbagliata · 401 non sei autenticato · 403 non hai il permesso ·
404 non trovato · 409 conflitto/stato non valido · 429 troppe richieste · 500 errore del server.

---

## 6. Come funziona il login (flusso a due passi)

Il sistema usa **due gettoni** (vedi anche `docs/AUTH.md`):

1. **Login** (email+password, o Microsoft) → il backend restituisce un **ticket** (dura 5 minuti). Prova che sei tu, ma **non** ti fa ancora vedere dati.
2. **Scelta istituto** → con il ticket si dice "entro nell'istituto X" → il backend restituisce il **token istituzionale** (dura 15 minuti).
   Questo token viene allegato a ogni richiesta successiva nell'intestazione `Authorization: Bearer <token>`.
3. Poco prima della scadenza il frontend **rinnova** il token in automatico (così non ti butta fuori durante un esame).
4. Se il backend risponde **401**, il frontend ti riporta al login.

Dove sta nel codice:
`LoginScreen.js` (form) → `authService.js` (chiamate) → `App.js` (decide cosa mostrare) → `session.js` (tiene i token in memoria)
→ `axiosClient.js` (allega il token a ogni richiesta e traduce gli errori).

---

## 7. Come leggere un file commentato
- Ogni blocco ha sopra un commento in italiano che spiega **cosa** fa e **perché**.
- Concetti già spiegati qui sono richiamati con `(vedi GUIDA §…)`.
- Il codice **non è stato modificato**: sono stati aggiunti solo commenti, quindi l'app si comporta esattamente come prima.

## 8. File già commentati
**Frontend**: `index.js`, `App.js`, `src/api/session.js`, `src/api/axiosClient.js`, `src/hooks/useAuth.js`,
`src/components/Btn.js`, `src/screens/LoginScreen.js`.
**Backend**: `Program.cs`, `Controllers/InstitutionGradeController.cs`, `Services/GradeService.cs`, `DTOs/CreateGradeDto.cs`.

Questi sette+quattro file mostrano l'intero "viaggio" di un dato: dalla schermata al servizio che applica le regole.
Gli altri file seguono gli **stessi schemi**: una volta capiti questi, il resto si legge allo stesso modo.
