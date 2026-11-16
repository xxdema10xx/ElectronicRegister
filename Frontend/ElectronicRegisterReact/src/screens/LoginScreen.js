// src/screens/LoginScreen.js
// La schermata di ACCESSO: email + password, oppure "Accedi con Microsoft".
// Non decide cosa succede dopo il login: avvisa App.js chiamando onLogin (vedi App.js, handleLogin).

// Hook di React: useState = memoria, useEffect = "fai qualcosa quando succede X". (vedi GUIDA §4.3)
import { useEffect, useState } from "react";
// Tessere grafiche di React Native (vedi GUIDA §4.4).
import { Alert, Image, SafeAreaView, ScrollView, StatusBar, Text, TouchableOpacity, View } from "react-native";
// Strumento per aprire il browser di sistema (serve per il login Microsoft).
import * as WebBrowser from "expo-web-browser";
// Stili condivisi.
import { s } from "../constants/theme";
// Componenti riusabili dell'app: carta, campo di testo, bottone.
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
// Funzioni che parlano col backend: login classico e login Microsoft.
import { login, loginWithMicrosoft } from "../api/authService";
// Hook personalizzati: login Microsoft e misure dello schermo.
import { useMicrosoftLogin } from "../hooks/useMicrosoftLogin";
import { useLayout } from "../hooks/useLayout";

// Su web chiude correttamente la finestra di login Microsoft quando torna all'app.
WebBrowser.maybeCompleteAuthSession();

// Componente della schermata. Riceve due props (vedi GUIDA §4.3):
//  - onLogin: funzione da chiamare quando il login riesce
//  - goRegister: funzione da chiamare per andare alla pagina "Registrati"
export default function LoginScreen({ onLogin, goRegister }) {
  // Quattro "memorie" della schermata (useState, vedi GUIDA §4.3):
  const [email, setEmail] = useState("");          // testo scritto nel campo email
  const [password, setPassword] = useState("");    // testo scritto nel campo password
  const [loading, setLoading] = useState(false);   // true mentre il login normale è in corso (rotellina)
  const [msLoading, setMsLoading] = useState(false); // true mentre il login Microsoft è in corso

  // isDesktop = true se lo schermo è largo (computer): serve per adattare il disegno.
  const { isDesktop } = useLayout();
  // Strumenti del login Microsoft: request (pronto?), response (esito), promptAsync (apri la finestra), exchangeCode (scambia il codice con un token).
  const { request, response, promptAsync, exchangeCode } = useMicrosoftLogin();

  // Si attiva ogni volta che cambia "response", cioè quando Microsoft risponde.
  useEffect(() => {
    if (response?.type === "success") {
      // Login Microsoft riuscito: Microsoft ha dato un "code" temporaneo.
      const { code } = response.params;
      setMsLoading(true);
      // Funzione anonima async lanciata subito: le parentesi finali () la eseguono.
      (async () => {
        try {
          // Si scambia il code con un token Microsoft, poi lo si manda al nostro backend.
          const tokenResponse = await exchangeCode(code);
          await submitMicrosoftToken(tokenResponse.accessToken);
        } catch (err) {
          Alert.alert("Errore login Microsoft", err?.message || JSON.stringify(err));
          setMsLoading(false);
        }
      })();
    } else if (response?.type === "error") {
      // Microsoft ha risposto con un errore o l'utente ha annullato.
      Alert.alert("Errore login Microsoft", response.error?.message || "Login annullato");
      setMsLoading(false);
    }
  }, [response]); // la lista: l'effetto riparte solo quando "response" cambia

  // Invia al nostro backend il token Microsoft e, se va bene, avvisa App.js.
  async function submitMicrosoftToken(msAccessToken) {
    try {
      const data = await loginWithMicrosoft(msAccessToken); // il backend risponde con un ticket (GUIDA §6)
      await onLogin({ ticket: data.ticket });
    } catch (e) {
      Alert.alert("Errore login Microsoft", String(e.message));
    } finally {
      setMsLoading(false); // in ogni caso si toglie la rotellina
    }
  }

  // Login con email e password, eseguito quando si preme "Entra".
  async function submit() {
    // Controllo veloce: se un campo è vuoto si avvisa e ci si ferma (return).
    if (!email || !password) { Alert.alert("Attenzione", "Compila tutti i campi"); return; }
    setLoading(true);
    try {
      // trim() toglie gli spazi all'inizio/fine dell'email.
      const data = await login(email.trim(), password);
      // Si passano anche le credenziali: App.js le tiene in memoria per rinnovare la sessione (vedi session.js).
      await onLogin({ ticket: data.ticket, credentials: { email: email.trim(), password } });
    } catch (e) { Alert.alert("Errore login", e.message); }
    finally { setLoading(false); }
  }

  // Ciò che si vede a schermo (JSX, vedi GUIDA §4.3).
  return (
    // Area sicura: evita barre/notch del telefono.
    <SafeAreaView style={s.authBg}>
      {/* Colore della barra di stato in alto (ora, batteria). */}
      <StatusBar barStyle="light-content" backgroundColor={s.authBg.backgroundColor} />
      {/* Area scorrevole. Su computer il contenuto si centra, su telefono occupa tutta l'altezza. */}
      <ScrollView contentContainerStyle={[{ flexGrow: 1 }, isDesktop && { justifyContent: "center", alignItems: "center", padding: 24 }]}
        keyboardShouldPersistTaps="handled">
        {/* Intestazione con logo e titoli. */}
        <View style={[s.authHeader, isDesktop && s.authHeaderDesktop]}>
          {/* Il logo, più piccolo su computer. */}
          <Image source={require("../../assets/images/logoits.png")} style={isDesktop ? { width: 100, height: 66 } : { width: 150, height: 100 }} />
          <Text style={[s.authTitle, isDesktop && { fontSize: 20 }]}>Electronic Register</Text>
          <Text style={s.authSubtitle}>ITS Umbria</Text>
        </View>
        {/* Corpo con il modulo di accesso. */}
        <View style={[s.authBody, isDesktop && s.authBodyDesktop]}>
          <Card>
            <Text style={s.cardTitle}>Accedi</Text>
            {/* Campo email: value = cosa mostra; onChangeText = cosa fare a ogni lettera digitata (aggiorna lo state). */}
            <Input label="Email" value={email} onChangeText={setEmail} keyboardType="email-address"
              autoCapitalize="none" placeholder="allievo_nome@itsumbria.it" />
            {/* Campo password: secureTextEntry nasconde i caratteri con dei pallini. */}
            <Input label="Password" value={password} onChangeText={setPassword} secureTextEntry placeholder="••••••••" />
            {/* Bottone di invio: mostra la rotellina quando loading è true. */}
            <Btn label="Entra" onPress={submit} loading={loading} style={{ marginTop: 4 }} />

            {/* Bottone Microsoft: disabilitato finché "request" non è pronta. */}
            <Btn
              label="Accedi con Microsoft"
              onPress={() => promptAsync()}
              loading={msLoading}
              disabled={!request}
              style={{ marginTop: 10, backgroundColor: "#2F2F2F" }}
            />

            {/* Link testuale per andare alla registrazione. */}
            <TouchableOpacity onPress={goRegister} style={{ marginTop: 14, alignItems: "center" }}>
              <Text style={{ color: s.authBg.backgroundColor, fontWeight: "600" }}>Non hai un account? Registrati</Text>
            </TouchableOpacity>
          </Card>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
