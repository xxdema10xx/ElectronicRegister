// src/screens/FirstPasswordScreen.js
// Primo accesso con la password temporanea data dall'istituto: il server accetta il ticket solo per cambiarla
// (ogni altra chiamata risponde 401), quindi si sceglie qui la nuova password prima di entrare.
import { useState } from "react";
import { Image, Platform, SafeAreaView, ScrollView, StatusBar, Text, TouchableOpacity, View } from "react-native";
import { C, s } from "../constants/theme";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import { PASSWORD_SPECIALS, getPasswordErrors } from "../utils/passwordValidation";
import { useLayout } from "../hooks/useLayout";

// onSubmit(newPassword) cambia la password ed entra; lancia un errore con il messaggio da mostrare
export default function FirstPasswordScreen({ email, temporaryPassword, onSubmit, onBack }) {
  const [form, setForm] = useState({ password: "", confirm: "" });
  const [errors, setErrors] = useState({});
  const [failure, setFailure] = useState(null);
  const [saving, setSaving] = useState(false);
  const isDesktop = useLayout().isDesktop || Platform.OS === "web";

  function set(k, v) {
    setForm(f => {
      const next = { ...f, [k]: v };
      setErrors(getPasswordErrors(next.password, next.confirm, temporaryPassword));
      return next;
    });
  }

  async function submit() {
    const e = getPasswordErrors(form.password, form.confirm, temporaryPassword);
    if (!form.password || !form.confirm) e.confirmPassword = e.confirmPassword || "Ripeti la nuova password.";
    setErrors(e);
    if (Object.keys(e).length > 0) return;
    setSaving(true);
    setFailure(null);
    try { await onSubmit(form.password); }
    catch (err) { setFailure(err.message); setSaving(false); }
  }

  return (
    <SafeAreaView style={s.authBg}>
      <StatusBar barStyle="light-content" backgroundColor={C.primary} />
      <ScrollView contentContainerStyle={[{ flexGrow: 1 }, isDesktop && { justifyContent: "center", alignItems: "center", padding: 24 }]}
        keyboardShouldPersistTaps="handled">
        <View style={[s.authHeader, isDesktop && s.authHeaderDesktop]}>
          <Image source={require("../../assets/images/logoits.png")} style={isDesktop ? { width: 100, height: 66 } : { width: 150, height: 100 }} />
          <Text style={[s.authTitle, isDesktop && { fontSize: 20 }]}>Electronic Register</Text>
        </View>
        <View style={[s.authBody, isDesktop && s.authBodyDesktop]}>
          <Card>
            <Text style={s.cardTitle}>Scegli la tua password</Text>
            <Text style={[s.itemSub, { marginBottom: 14 }]}>
              Stai usando la password temporanea ricevuta dall'istituto per {email}. Prima di entrare scegline una tua:
              almeno 8 caratteri, con almeno un carattere speciale ({PASSWORD_SPECIALS}).
            </Text>
            <Input label="Nuova password" value={form.password} onChangeText={v => set("password", v)} secureTextEntry
              placeholder="••••••••" error={errors.newPassword} />
            <Input label="Conferma nuova password" value={form.confirm} onChangeText={v => set("confirm", v)} secureTextEntry
              placeholder="••••••••" error={errors.confirmPassword} />
            {!!failure && <Text style={[s.errorText, { marginBottom: 10 }]}>{failure}</Text>}
            <Btn label="Salva e accedi" onPress={submit} loading={saving} />
            <TouchableOpacity onPress={onBack} style={{ marginTop: 14, alignItems: "center" }}>
              <Text style={{ color: C.primary, fontWeight: "600" }}>Torna al login</Text>
            </TouchableOpacity>
          </Card>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
