// App.js
// IL CERVELLO DEL FRONTEND. Decide quale schermata mostrare:
//   caricamento → login/registrazione → scelta istituto → app vera (menu + pagine).
// Gestisce anche la sessione: salvataggio, rinnovo del token, logout. (flusso completo: GUIDA §6)

// File che adatta gli avvisi (Alert) al web; viene solo eseguito, non serve importare nulla da lì.
import "./src/utils/webAlert";
// Hook di React (vedi GUIDA §4.3).
import { useCallback, useEffect, useRef, useState } from "react";
// Tessere grafiche di React Native (vedi GUIDA §4.4); Platform dice se siamo su web, Android o iOS.
import { ActivityIndicator, Alert, Platform, Text, View } from "react-native";
// Gestisce le zone sicure dello schermo (notch, barre).
import { SafeAreaProvider } from "react-native-safe-area-context";
// Colori e stili condivisi.
import { C, s } from "./src/constants/theme";
// La "bacheca" che condivide i dati dell'utente con tutte le schermate (vedi useAuth.js).
import { AuthContext } from "./src/hooks/useAuth";
// Funzioni che parlano col backend per login, account, istituti.
import { buildUser, getAccount, getContext, getInstitutions, login, selectInstitution } from "./src/api/authService";
// Il portachiavi con i token (vedi session.js).
import { session } from "./src/api/session";
// Permette di dire al "postino" cosa fare quando arriva un 401 (vedi axiosClient.js).
import { setUnauthorizedHandler } from "./src/api/axiosClient";
// Funzioni per salvare/cancellare dati locali separati per utente.
import { clearUserStorage, purgeLegacyStorage, setStorageUser } from "./src/utils/userStorage";
// Le schermate che App sceglie di mostrare.
import LoginScreen from "./src/screens/LoginScreen";
import RegisterScreen from "./src/screens/RegisterScreen";
import InstitutionPickerScreen from "./src/screens/InstitutionPickerScreen";
import AppNav from "./src/navigation/AppNav";

// La sessione sopravvive al refresh (F5) su web grazie a sessionStorage: senza, ogni reload perdeva lo
// stato React e rimandava al login anche con un token ancora valido. A differenza di localStorage,
// sessionStorage è isolato per scheda: così si può restare loggati con account diversi in schede diverse.
// Si salvano token istituzionale, scadenza, istituto e utente; mai ticket né password.
// sessionStorage = piccolo "cassetto" del browser che si svuota alla chiusura della scheda.
const SESSION_KEY = "authSession2"; // etichetta del cassetto
// Oggetto con tre azioni sul cassetto: leggere (get), scrivere (set), svuotare (clear).
// Funziona solo su web (Platform.OS === "web"); i try/catch evitano crash se il browser lo blocca.
const sessionStore = {
  get() { try { return Platform.OS === "web" ? JSON.parse(window.sessionStorage.getItem(SESSION_KEY)) : null; } catch { return null; } },
  set(v) { try { if (Platform.OS === "web") window.sessionStorage.setItem(SESSION_KEY, JSON.stringify(v)); } catch {} },
  clear() { try { if (Platform.OS === "web") window.sessionStorage.removeItem(SESSION_KEY); } catch {} },
};
// JSON.stringify trasforma un oggetto in testo (per salvarlo); JSON.parse fa il contrario.

// Elimina dati vecchi, salvati da versioni precedenti dell'app, che non servono più.
purgeLegacyStorage();

// Il componente principale dell'app.
export default function App() {
  // Le "memorie" di App (useState, vedi GUIDA §4.3):
  const [token, setToken] = useState(null);            // token istituzionale (null = non loggato)
  const [user, setUser] = useState(null);              // dati dell'utente loggato
  const [authScreen, setAuthScreen] = useState("login"); // "login" oppure "register"
  const [booting, setBooting] = useState(true);        // true = sto caricando, mostra la rotellina
  const [choices, setChoices] = useState(null); // istituti tra cui scegliere dopo il login
  // useRef: scatola che ricorda i dati dell'account senza ridisegnare la schermata.
  const pendingAccount = useRef(null);

  // Durante il render e non in un effetto: le schermate leggono i dati dell'utente già al primo render
  // Dice al sistema di salvataggio locale "ora l'utente è questo", per tenere i dati separati per persona.
  setStorageUser(user?.id);

  // Salva nel cassetto del browser i dati della sessione corrente (u = utente).
  const persist = (u) => sessionStore.set({
    accessToken: session.accessToken, accessExpiresAt: session.accessExpiresAt, institution: session.institution, user: u,
  });

  // Aggiorna solo alcuni campi dell'utente (patch): copia l'utente, sovrascrive i campi nuovi (spread, GUIDA §4.1),
  // li salva e aggiorna lo stato.
  const updateUser = (patch) => { const next = { ...user, ...patch }; persist(next); setUser(next); };

  // Con "message" la sessione è scaduta da sola: i dati restano, separati per utente, così chi rientra ritrova
  // la propria bozza non salvata. Con l'uscita volontaria si cancellano.
  // useCallback: ricorda la funzione, per non ricrearla a ogni ridisegno (GUIDA §4.3).
  const logout = useCallback((message) => {
    if (!message) clearUserStorage();   // uscita volontaria: cancella i dati locali dell'utente
    session.clear();                    // dimentica i token
    sessionStore.clear();               // svuota il cassetto del browser
    setToken(null); setUser(null); setChoices(null); setAuthScreen("login"); // torna allo stato "non loggato"
    if (message) Alert.alert("Sessione terminata", message); // se c'è un motivo, lo mostra
  }, []);

  // All'avvio: se c'è una sessione salvata e non scaduta, si riprende invece di tornare al login.
  // [] = l'effetto gira una sola volta, all'apertura dell'app.
  useEffect(() => {
    const stored = sessionStore.get();
    // Nessun token, o già scaduto: si pulisce e si smette di "caricare" (si andrà al login).
    if (!stored?.accessToken || stored.accessExpiresAt <= Date.now()) { sessionStore.clear(); setBooting(false); return; }
    session.restore(stored); // si rimettono i token nel portachiavi
    // Si chiede al backend se il token è ancora valido (getContext).
    getContext(stored.accessToken)
      .then(() => { setToken(stored.accessToken); setUser(stored.user); }) // valido: si entra
      .catch(() => { session.clear(); sessionStore.clear(); })              // non valido: si pulisce
      .finally(() => setBooting(false));                                    // in ogni caso: fine caricamento
  }, []);

  // Registra cosa fare se il backend risponde 401: mostrare il logout con un messaggio.
  useEffect(() => { setUnauthorizedHandler(() => logout("La sessione è scaduta o è stata revocata: accedi di nuovo.")); }, [logout]);

  // Il token istituzionale dura 15 minuti e non esiste un refresh: poco prima della scadenza si richiede
  // con il ticket (se ancora valido) o rifacendo il login con le credenziali tenute in memoria.
  useEffect(() => {
    if (!token) return; // se non sono loggato non c'è nulla da rinnovare
    let timer;
    // Programma il rinnovo tra "msUntilRenew" millisecondi (setTimeout = "esegui tra X tempo").
    const schedule = () => { timer = setTimeout(renew, session.msUntilRenew()); };
    async function renew() {
      try {
        // Usa il ticket se valido; altrimenti rifà il login con le credenziali in memoria e prende il nuovo ticket.
        const ticket = session.ticket || (session.credentials
          ? (await login(session.credentials.email, session.credentials.password)).ticket : null);
        // Nessun modo di rinnovare: logout.
        if (!ticket) return logout("La sessione è scaduta: accedi di nuovo.");
        // Chiede un nuovo token per lo stesso istituto, salva e programma il prossimo rinnovo.
        await selectInstitution(ticket, session.institution);
        persist(user);
        schedule();
      } catch { logout("Non è stato possibile rinnovare la sessione: accedi di nuovo."); }
    }
    schedule();
    // Pulizia: se l'effetto riparte o l'app si chiude, il timer in attesa viene annullato.
    return () => clearTimeout(timer);
  }, [token, user, logout]);

  // Entra nell'istituto scelto ("choice") e apre l'app vera.
  async function enter(choice) {
    setBooting(true);
    try {
      // Con il ticket si ottiene il token istituzionale per l'istituto scelto.
      const { token: access } = await selectInstitution(session.ticket, choice);
      // Si costruisce l'oggetto utente con i dati dal backend.
      const u = await buildUser(access, pendingAccount.current);
      persist(u);
      setChoices(null);
      setUser(u);
      setToken(access); // cambiando token, App ridisegna e mostra l'app vera
    } catch (e) { Alert.alert("Accesso non riuscito", e.message); }
    finally { setBooting(false); }
  }

  // Dopo il login (password o Microsoft) si ha solo il ticket: si cercano gli istituti dell'utente.
  async function handleLogin({ ticket, credentials }) {
    session.setCredentials(credentials || null); // credentials vuote → null (login Microsoft non ne ha)
    setBooting(true);
    try {
      // Dati dell'account; se falliscono si prosegue comunque (.catch restituisce null).
      pendingAccount.current = await getAccount(ticket).catch(() => null);
      const list = await getInstitutions(ticket);
      if (list.length === 0) {
        // Nessun istituto associato: non si può proseguire.
        session.clear();
        Alert.alert("Nessun istituto", "Il tuo account non è ancora associato a nessun istituto. Contatta la segreteria.");
      } else if (list.length === 1) {
        // Un solo istituto: si entra direttamente.
        await enter(list[0]);
        return;
      } else setChoices(list); // più istituti: si mostra la schermata di scelta
    } catch (e) { session.clear(); Alert.alert("Errore", e.message); }
    finally { setBooting(false); }
  }

  // Da qui in giù si decide COSA mostrare, in ordine di priorità:

  // 1) Sto caricando: rotellina e scritta.
  if (booting) {
    return (
      <View style={[s.loaderWrap, { flex: 1 }]}>
        <ActivityIndicator size="large" color={C.primary} />
        <Text style={{ color: C.textMuted, marginTop: 12 }}>Caricamento…</Text>
      </View>
    );
  }

  // 2) Non sono loggato: scelta istituto, registrazione o login.
  if (!token) {
    if (choices) return <InstitutionPickerScreen institutions={choices} onSelect={enter} onBack={() => logout()} />;
    return authScreen === "register"
      ? <RegisterScreen onBack={() => setAuthScreen("login")} />
      : <LoginScreen onLogin={handleLogin} goRegister={() => setAuthScreen("register")} />;
  }

  // 3) Sono loggato: l'app vera. AuthContext.Provider pubblica token, user, logout, updateUser
  // a TUTTE le schermate figlie (si leggono con useAuth(), vedi useAuth.js).
  return (
    <SafeAreaProvider>
      <AuthContext.Provider value={{ token, user, logout, updateUser }}>
        <AppNav user={user} onLogout={() => logout()} />
      </AuthContext.Provider>
    </SafeAreaProvider>
  );
}
