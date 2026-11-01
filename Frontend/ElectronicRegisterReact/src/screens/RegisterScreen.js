// src/screens/RegisterScreen.js
// Registrazione dell'account (solo email e password, POST /identity/accounts). Nome, cognome e ruolo
// non fanno parte dell'account: li assegna l'istituto quando l'utente ne diventa membro.
import { useState } from "react";
import { Alert, Image, SafeAreaView, ScrollView, StatusBar, Text, TouchableOpacity, View } from "react-native";
import { C, s } from "../constants/theme";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import { register } from "../api/authService";
import { getPasswordErrors } from "../utils/passwordValidation";
import { useLayout } from "../hooks/useLayout";

export default function RegisterScreen({ onBack }) {
  const [form, setForm] = useState({ email: "", password: "", confirmPassword: "" });
  const [errors, setErrors] = useState({});
  const [loading, setLoading] = useState(false);
  const { isDesktop } = useLayout();

  function set(k, v) {
    setForm(f => {
      const next = { ...f, [k]: v };
      setErrors(getPasswordErrors(next.password, next.confirmPassword));
      return next;
    });
  }

  async function submit() {
    if (!form.email.trim() || !form.password || !form.confirmPassword) { Alert.alert("Attenzione", "Compila tutti i campi"); return; }
    const e = getPasswordErrors(form.password, form.confirmPassword);
    if (Object.keys(e).length > 0) { setErrors(e); return; }
    setLoading(true);
    try {
      await register(form);
      Alert.alert("Account creato", "Ora puoi accedere. Per usare il registro, la segreteria deve associare il tuo account all'istituto.",
        [{ text: "OK", onPress: onBack }]);
    } catch (err) {
      Alert.alert("Errore", err.status === 409 ? "Esiste già un account con questa email." : err.message);
    } finally { setLoading(false); }
  }

  return (
    <SafeAreaView style={s.authBg}>
      <StatusBar barStyle="light-content" backgroundColor={C.primary} />
      <View style={[s.authHeader, isDesktop && s.authHeaderDesktop]}>
        <Image source={require("../../assets/images/logoits.png")} style={isDesktop ? { width: 100, height: 66 } : { width: 150, height: 100 }} />
        <Text style={[s.authTitle, isDesktop && { fontSize: 20 }]}>Electronic Register</Text>
      </View>
      <ScrollView contentContainerStyle={[{ flexGrow: 1 }, isDesktop && { justifyContent: "center", alignItems: "center", padding: 24 }]}
        keyboardShouldPersistTaps="handled">
        <View style={[s.authBody, isDesktop && s.authBodyDesktop]}>
        <Card>
          <Text style={s.cardTitle}>Crea account</Text>
          <Input label="Email" value={form.email} onChangeText={v => set("email", v)}
            keyboardType="email-address" autoCapitalize="none" placeholder="allievo_mario@itsumbria.it" />
          <Input label="Password (min 8 car. + simbolo)" value={form.password} onChangeText={v => set("password", v)}
            secureTextEntry placeholder="••••••••" error={errors.newPassword} />
          <Input label="Conferma password" value={form.confirmPassword} onChangeText={v => set("confirmPassword", v)}
            secureTextEntry placeholder="••••••••" error={errors.confirmPassword} />
          <Btn label="Registrati" onPress={submit} loading={loading} />
          <TouchableOpacity onPress={onBack} style={{ marginTop: 14, alignItems: "center" }}>
            <Text style={{ color: C.primary, fontWeight: "600" }}>Hai già un account? Accedi</Text>
          </TouchableOpacity>
        </Card>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
