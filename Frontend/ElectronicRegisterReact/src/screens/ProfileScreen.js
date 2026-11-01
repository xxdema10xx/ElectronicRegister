// src/screens/ProfileScreen.js
// Account dell'utente (docs/AUTH.md §2.8): nome visualizzato e cambio password. Entrambe le operazioni
// richiedono il ticket di accesso: per la password se ne ottiene uno nuovo con la password attuale.
import { useState } from "react";
import { Alert, ScrollView, Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import Badge from "../components/Badge";
import FormModal from "../components/FormModal";
import { getPasswordErrors } from "../utils/passwordValidation";
import { changePassword, freshTicket, login, updateDisplayName } from "../api/authService";

const ROLE_LABEL = { InstitutionAdmin: "Amministratore", Staff: "Segreteria", Teacher: "Docente", Student: "Studente" };

export default function ProfileScreen({ onLogout }) {
  const { user, logout } = useAuth();
  const [modal, setModal] = useState(null); // "name" | "password"
  const [displayName, setDisplayName] = useState(user?.displayName || "");
  const [shownName, setShownName] = useState(user?.displayName || "");
  const [pwdForm, setPwdForm] = useState({ oldPassword: "", newPassword: "", confirmPassword: "" });
  const [pwdErrors, setPwdErrors] = useState({});
  const [saving, setSaving] = useState(false);

  const role = user?.role?.toLowerCase();
  const personName = `${user?.studentFirstName || user?.teacherFirstName || ""} ${user?.studentLastName || user?.teacherLastName || ""}`.trim();
  const name = personName || shownName;

  function close() {
    setModal(null);
    setPwdForm({ oldPassword: "", newPassword: "", confirmPassword: "" });
    setPwdErrors({});
  }

  function updatePwdField(field, value) {
    setPwdForm(f => {
      const next = { ...f, [field]: value };
      setPwdErrors(getPasswordErrors(next.newPassword, next.confirmPassword, next.oldPassword));
      return next;
    });
  }

  async function saveName() {
    if (!displayName.trim()) { Alert.alert("Attenzione", "Inserisci un nome"); return; }
    setSaving(true);
    try {
      await updateDisplayName(await freshTicket(), displayName);
      setShownName(displayName.trim());
      close();
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setSaving(false); }
  }

  async function savePassword() {
    if (!pwdForm.oldPassword || !pwdForm.newPassword || !pwdForm.confirmPassword) {
      Alert.alert("Attenzione", "Inserisci vecchia password, nuova password e conferma");
      return;
    }
    const errors = getPasswordErrors(pwdForm.newPassword, pwdForm.confirmPassword, pwdForm.oldPassword);
    if (Object.keys(errors).length > 0) { setPwdErrors(errors); return; }
    setSaving(true);
    try {
      // La password attuale serve anche a ottenere un ticket fresco (quello del login dura 5 minuti)
      const { ticket } = await login(user.email, pwdForm.oldPassword).catch(() => { throw new Error("La password attuale non è corretta."); });
      await changePassword(ticket, pwdForm.oldPassword, pwdForm.newPassword);
      close();
      // Il server revoca tutte le sessioni dopo il cambio password
      logout("Password aggiornata: accedi di nuovo con la nuova password.");
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setSaving(false); }
  }

  return (
    <ScrollView style={{ flex: 1, backgroundColor: C.bg }} contentContainerStyle={{ padding: 16 }}>
      <View style={s.profileHeader}>
        <View style={s.profileAvatar}>
          <Text style={s.profileAvatarText}>{(user?.email || "?")[0].toUpperCase()}</Text>
        </View>
        {!!name && <Text style={s.profileName}>{name}</Text>}
        <Text style={s.profileEmail}>{user?.email}</Text>
        <Badge text={role} role={role} />
      </View>

      <Card style={{ marginTop: 16 }}>
        <Text style={[s.cardTitle, { fontSize: 14 }]}>Dettagli account</Text>
        <View style={s.profileRow}><Text style={s.profileKey}>Email</Text><Text style={s.profileVal}>{user?.email}</Text></View>
        {!!shownName && <View style={s.profileRow}><Text style={s.profileKey}>Nome visualizzato</Text><Text style={s.profileVal}>{shownName}</Text></View>}
        {!!user?.institutionName && <View style={s.profileRow}><Text style={s.profileKey}>Istituto</Text><Text style={s.profileVal}>{user.institutionName}</Text></View>}
        <View style={s.profileRow}><Text style={s.profileKey}>Ruoli</Text><Text style={s.profileVal}>{(user?.roles || []).map(r => ROLE_LABEL[r] || r).join(", ")}</Text></View>
        {user?.studentId && <View style={s.profileRow}><Text style={s.profileKey}>ID Studente</Text><Text style={s.profileVal}>{user.studentId.slice(0, 16)}…</Text></View>}
        {user?.teacherId && <View style={s.profileRow}><Text style={s.profileKey}>ID Docente</Text><Text style={s.profileVal}>{user.teacherId.slice(0, 16)}…</Text></View>}
      </Card>

      <Btn label="Modifica nome visualizzato" onPress={() => { setDisplayName(shownName); setModal("name"); }} style={{ marginTop: 16 }} variant="primary" />
      <Btn label="Cambia password" onPress={() => setModal("password")} style={{ marginTop: 10 }} variant="primary" />
      <Btn label="Logout" onPress={onLogout} style={{ marginTop: 10 }} variant="danger" />

      <FormModal visible={modal === "name"} title="Nome visualizzato" onClose={close}>
        <Input label="Nome" value={displayName} onChangeText={setDisplayName} placeholder="Mario Rossi" />
        <Btn label="Salva" onPress={saveName} loading={saving} />
      </FormModal>

      <FormModal visible={modal === "password"} title="Cambia password" onClose={close}>
        <Input label="Vecchia password" value={pwdForm.oldPassword} onChangeText={v => updatePwdField("oldPassword", v)} secureTextEntry />
        <Input label="Nuova password" value={pwdForm.newPassword} onChangeText={v => updatePwdField("newPassword", v)} secureTextEntry error={pwdErrors.newPassword} />
        <Input label="Conferma nuova password" value={pwdForm.confirmPassword} onChangeText={v => updatePwdField("confirmPassword", v)} secureTextEntry error={pwdErrors.confirmPassword} />
        <Btn label="Salva" onPress={savePassword} loading={saving} />
      </FormModal>
    </ScrollView>
  );
}
