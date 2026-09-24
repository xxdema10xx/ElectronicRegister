// src/screens/LoginScreen.js
import { useEffect, useState } from "react";
import { Alert, Image, SafeAreaView, StatusBar, Text, TouchableOpacity, View } from "react-native";
import * as WebBrowser from "expo-web-browser";
import { s } from "../constants/theme";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import { login, loginWithMicrosoft } from "../api/authService";
import { useMicrosoftLogin } from "../hooks/useMicrosoftLogin";

WebBrowser.maybeCompleteAuthSession();

export default function LoginScreen({ onLogin, goRegister }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [msLoading, setMsLoading] = useState(false);

  const { request, response, promptAsync, exchangeCode } = useMicrosoftLogin();

  useEffect(() => {
    if (response?.type === "success") {
      const { code } = response.params;
      setMsLoading(true);
      (async () => {
        try {
          const tokenResponse = await exchangeCode(code);
          await submitMicrosoftToken(tokenResponse.accessToken);
        } catch (err) {
          Alert.alert("Errore login Microsoft", err?.message || JSON.stringify(err));
          setMsLoading(false);
        }
      })();
    } else if (response?.type === "error") {
      Alert.alert("Errore login Microsoft", response.error?.message || "Login annullato");
      setMsLoading(false);
    }
  }, [response]);

  async function submitMicrosoftToken(msAccessToken) {
    try {
      const data = await loginWithMicrosoft(msAccessToken);
      onLogin(data.token);
    } catch (e) {
      Alert.alert("Errore login Microsoft", String(e.message));
    } finally {
      setMsLoading(false);
    }
  }

  async function submit() {
    if (!email || !password) { Alert.alert("Attenzione", "Compila tutti i campi"); return; }
    setLoading(true);
    try {
      const data = await login(email, password);
      onLogin(data.token);
    } catch (e) { Alert.alert("Errore login", e.message); }
    finally { setLoading(false); }
  }

  return (
    <SafeAreaView style={s.authBg}>
      <StatusBar barStyle="light-content" backgroundColor={s.authBg.backgroundColor} />
      <View style={s.authHeader}>
        <Image source={require("../../assets/images/logoits.png")} style={{ width: 150, height: 100 }} />
        <Text style={s.authTitle}>Electronic Register</Text>
        <Text style={s.authSubtitle}>ITS Umbria</Text>
      </View>
      <View style={s.authBody}>
        <Card>
          <Text style={s.cardTitle}>Accedi</Text>
          <Input label="Email" value={email} onChangeText={setEmail} keyboardType="email-address"
            autoCapitalize="none" placeholder="allievo_nome@itsumbria.it" />
          <Input label="Password" value={password} onChangeText={setPassword} secureTextEntry placeholder="••••••••" />
          <Btn label="Entra" onPress={submit} loading={loading} style={{ marginTop: 4 }} />

          <Btn
            label="Accedi con Microsoft"
            onPress={() => promptAsync()}
            loading={msLoading}
            disabled={!request}
            style={{ marginTop: 10, backgroundColor: "#2F2F2F" }}
          />

          <TouchableOpacity onPress={goRegister} style={{ marginTop: 14, alignItems: "center" }}>
            <Text style={{ color: s.authBg.backgroundColor, fontWeight: "600" }}>Non hai un account? Registrati</Text>
          </TouchableOpacity>
        </Card>
      </View>
    </SafeAreaView>
  );
}
