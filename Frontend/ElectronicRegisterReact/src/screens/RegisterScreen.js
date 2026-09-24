// src/screens/RegisterScreen.js
import { useState } from "react";
import { Alert, Image, SafeAreaView, ScrollView, StatusBar, Text, TouchableOpacity, View } from "react-native";
import { C, s } from "../constants/theme";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import { register } from "../api/authService";

export default function RegisterScreen({ onBack }) {
  const [form, setForm] = useState({ email: "", password: "", firstName: "", lastName: "" });
  const [loading, setLoading] = useState(false);

  function set(k, v) { setForm(f => ({ ...f, [k]: v })); }

  async function submit() {
    setLoading(true);
    try {
      await register(form);
      Alert.alert("Successo", "Registrazione completata!", [{ text: "OK", onPress: onBack }]);
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setLoading(false); }
  }

  return (
    <SafeAreaView style={s.authBg}>
      <StatusBar barStyle="light-content" backgroundColor={C.primary} />
      <View style={s.authHeader}>
        <Image source={require("../../assets/images/logoits.png")} style={{ width: 150, height: 100 }} />
        <Text style={s.authTitle}>Electronic Register</Text>
      </View>
      <ScrollView contentContainerStyle={s.authBody}>
        <Card>
          <Text style={s.cardTitle}>Crea account</Text>
          <Input label="Nome" value={form.firstName} onChangeText={v => set("firstName", v)} placeholder="Mario" />
          <Input label="Cognome" value={form.lastName} onChangeText={v => set("lastName", v)} placeholder="Rossi" />
          <Input label="Email" value={form.email} onChangeText={v => set("email", v)}
            keyboardType="email-address" autoCapitalize="none" placeholder="allievo_mario@itsumbria.it" />
          <Input label="Password (min 8 car. + simbolo)" value={form.password} onChangeText={v => set("password", v)}
            secureTextEntry placeholder="••••••••" />
          <Btn label="Registrati" onPress={submit} loading={loading} />
          <TouchableOpacity onPress={onBack} style={{ marginTop: 14, alignItems: "center" }}>
            <Text style={{ color: C.primary, fontWeight: "600" }}>Hai già un account? Accedi</Text>
          </TouchableOpacity>
        </Card>
      </ScrollView>
    </SafeAreaView>
  );
}
