// App.js
import "./src/utils/webAlert";
import { useCallback, useEffect, useRef, useState } from "react";
import { ActivityIndicator, Alert, Platform, Text, View } from "react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { C, s } from "./src/constants/theme";
import { AuthContext } from "./src/hooks/useAuth";
import { buildUser, getAccount, getContext, getInstitutions, login, selectInstitution } from "./src/api/authService";
import { session } from "./src/api/session";
import { setUnauthorizedHandler } from "./src/api/axiosClient";
import LoginScreen from "./src/screens/LoginScreen";
import RegisterScreen from "./src/screens/RegisterScreen";
import InstitutionPickerScreen from "./src/screens/InstitutionPickerScreen";
import AppNav from "./src/navigation/AppNav";

// La sessione sopravvive al refresh (F5) su web grazie a sessionStorage: senza, ogni reload perdeva lo
// stato React e rimandava al login anche con un token ancora valido. A differenza di localStorage,
// sessionStorage è isolato per scheda: così si può restare loggati con account diversi in schede diverse.
// Si salvano token istituzionale, scadenza, istituto e utente; mai ticket né password.
const SESSION_KEY = "authSession2";
const sessionStore = {
  get() { try { return Platform.OS === "web" ? JSON.parse(window.sessionStorage.getItem(SESSION_KEY)) : null; } catch { return null; } },
  set(v) { try { if (Platform.OS === "web") window.sessionStorage.setItem(SESSION_KEY, JSON.stringify(v)); } catch {} },
  clear() { try { if (Platform.OS === "web") window.sessionStorage.removeItem(SESSION_KEY); } catch {} },
};

export default function App() {
  const [token, setToken] = useState(null);
  const [user, setUser] = useState(null);
  const [authScreen, setAuthScreen] = useState("login");
  const [booting, setBooting] = useState(true);
  const [choices, setChoices] = useState(null); // istituti tra cui scegliere dopo il login
  const pendingAccount = useRef(null);

  const persist = (u) => sessionStore.set({
    accessToken: session.accessToken, accessExpiresAt: session.accessExpiresAt, institution: session.institution, user: u,
  });

  const logout = useCallback((message) => {
    session.clear();
    sessionStore.clear();
    setToken(null); setUser(null); setChoices(null); setAuthScreen("login");
    if (message) Alert.alert("Sessione terminata", message);
  }, []);

  // All'avvio: se c'è una sessione salvata e non scaduta, si riprende invece di tornare al login.
  useEffect(() => {
    const stored = sessionStore.get();
    if (!stored?.accessToken || stored.accessExpiresAt <= Date.now()) { sessionStore.clear(); setBooting(false); return; }
    session.restore(stored);
    getContext(stored.accessToken)
      .then(() => { setToken(stored.accessToken); setUser(stored.user); })
      .catch(() => { session.clear(); sessionStore.clear(); })
      .finally(() => setBooting(false));
  }, []);

  useEffect(() => { setUnauthorizedHandler(() => logout("La sessione è scaduta o è stata revocata: accedi di nuovo.")); }, [logout]);

  // Il token istituzionale dura 15 minuti e non esiste un refresh: poco prima della scadenza si richiede
  // con il ticket (se ancora valido) o rifacendo il login con le credenziali tenute in memoria.
  useEffect(() => {
    if (!token) return;
    let timer;
    const schedule = () => { timer = setTimeout(renew, session.msUntilRenew()); };
    async function renew() {
      try {
        const ticket = session.ticket || (session.credentials
          ? (await login(session.credentials.email, session.credentials.password)).ticket : null);
        if (!ticket) return logout("La sessione è scaduta: accedi di nuovo.");
        await selectInstitution(ticket, session.institution);
        persist(user);
        schedule();
      } catch { logout("Non è stato possibile rinnovare la sessione: accedi di nuovo."); }
    }
    schedule();
    return () => clearTimeout(timer);
  }, [token, user, logout]);

  async function enter(choice) {
    setBooting(true);
    try {
      const { token: access } = await selectInstitution(session.ticket, choice);
      const u = await buildUser(access, pendingAccount.current);
      persist(u);
      setChoices(null);
      setUser(u);
      setToken(access);
    } catch (e) { Alert.alert("Accesso non riuscito", e.message); }
    finally { setBooting(false); }
  }

  // Dopo il login (password o Microsoft) si ha solo il ticket: si cercano gli istituti dell'utente.
  async function handleLogin({ ticket, credentials }) {
    session.setCredentials(credentials || null);
    setBooting(true);
    try {
      pendingAccount.current = await getAccount(ticket).catch(() => null);
      const list = await getInstitutions(ticket);
      if (list.length === 0) {
        session.clear();
        Alert.alert("Nessun istituto", "Il tuo account non è ancora associato a nessun istituto. Contatta la segreteria.");
      } else if (list.length === 1) {
        await enter(list[0]);
        return;
      } else setChoices(list);
    } catch (e) { session.clear(); Alert.alert("Errore", e.message); }
    finally { setBooting(false); }
  }

  if (booting) {
    return (
      <View style={[s.loaderWrap, { flex: 1 }]}>
        <ActivityIndicator size="large" color={C.primary} />
        <Text style={{ color: C.textMuted, marginTop: 12 }}>Caricamento…</Text>
      </View>
    );
  }

  if (!token) {
    if (choices) return <InstitutionPickerScreen institutions={choices} onSelect={enter} onBack={() => logout()} />;
    return authScreen === "register"
      ? <RegisterScreen onBack={() => setAuthScreen("login")} />
      : <LoginScreen onLogin={handleLogin} goRegister={() => setAuthScreen("register")} />;
  }

  return (
    <SafeAreaProvider>
      <AuthContext.Provider value={{ token, user, logout }}>
        <AppNav user={user} onLogout={() => logout()} />
      </AuthContext.Provider>
    </SafeAreaProvider>
  );
}
