// App.js
import "./src/utils/webAlert";
import { useEffect, useState } from "react";
import { ActivityIndicator, Alert, Platform, Text, View } from "react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { C, s } from "./src/constants/theme";
import { AuthContext } from "./src/hooks/useAuth";
import { getMe } from "./src/api/authService";
import LoginScreen from "./src/screens/LoginScreen";
import RegisterScreen from "./src/screens/RegisterScreen";
import AppNav from "./src/navigation/AppNav";

// Il token sopravvive al refresh (F5) su web grazie a sessionStorage: senza, ogni reload perdeva lo
// stato React e rimandava al login anche con un token ancora valido. A differenza di localStorage,
// sessionStorage è isolato per scheda: così si può restare loggati con account diversi in schede diverse.
const TOKEN_KEY = "authToken";
const tokenStore = {
  get() { try { return Platform.OS === "web" ? window.sessionStorage.getItem(TOKEN_KEY) : null; } catch { return null; } },
  set(t) { try { if (Platform.OS === "web") window.sessionStorage.setItem(TOKEN_KEY, t); } catch {} },
  clear() { try { if (Platform.OS === "web") window.sessionStorage.removeItem(TOKEN_KEY); } catch {} },
};

export default function App() {
  const [token, setToken] = useState(null);
  const [user, setUser] = useState(null);
  const [authScreen, setAuthScreen] = useState("login");
  const [booting, setBooting] = useState(true);

  // All'avvio: se c'è un token salvato, si riprende la sessione invece di tornare al login.
  useEffect(() => {
    const stored = tokenStore.get();
    if (!stored) { setBooting(false); return; }
    getMe(stored)
      .then(me => { setToken(stored); setUser(me); })
      .catch(() => tokenStore.clear())
      .finally(() => setBooting(false));
  }, []);

  async function handleLogin(t) {
    setToken(t);
    setBooting(true);
    try {
      const me = await getMe(t);
      setUser(me);
      tokenStore.set(t);
    } catch (e) { Alert.alert("Errore", e.message); setToken(null); }
    finally { setBooting(false); }
  }

  function handleLogout() { setToken(null); setUser(null); setAuthScreen("login"); tokenStore.clear(); }

  if (booting) {
    return (
      <View style={[s.loaderWrap, { flex: 1 }]}>
        <ActivityIndicator size="large" color={C.primary} />
        <Text style={{ color: C.textMuted, marginTop: 12 }}>Caricamento…</Text>
      </View>
    );
  }

  if (!token) {
    return authScreen === "register"
      ? <RegisterScreen onBack={() => setAuthScreen("login")} />
      : <LoginScreen onLogin={handleLogin} goRegister={() => setAuthScreen("register")} />;
  }

  return (
    <SafeAreaProvider>
      <AuthContext.Provider value={{ token, user }}>
        <AppNav user={user} onLogout={handleLogout} />
      </AuthContext.Provider>
    </SafeAreaProvider>
  );
}
