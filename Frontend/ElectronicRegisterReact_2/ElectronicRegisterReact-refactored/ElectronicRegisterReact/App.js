// App.js
import { useState } from "react";
import { ActivityIndicator, Alert, Text, View } from "react-native";
import { SafeAreaProvider } from "react-native-safe-area-context";
import { C, s } from "./src/constants/theme";
import { AuthContext } from "./src/hooks/useAuth";
import { getMe } from "./src/api/authService";
import LoginScreen from "./src/screens/LoginScreen";
import RegisterScreen from "./src/screens/RegisterScreen";
import AppNav from "./src/navigation/AppNav";

export default function App() {
  const [token, setToken] = useState(null);
  const [user, setUser] = useState(null);
  const [authScreen, setAuthScreen] = useState("login");
  const [booting, setBooting] = useState(false);

  async function handleLogin(t) {
    setToken(t);
    setBooting(true);
    try {
      const me = await getMe(t);
      setUser(me);
    } catch (e) { Alert.alert("Errore", e.message); setToken(null); }
    finally { setBooting(false); }
  }

  function handleLogout() { setToken(null); setUser(null); setAuthScreen("login"); }

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
