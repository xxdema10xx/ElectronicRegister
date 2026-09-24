// src/screens/ProfileScreen.js
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
import { updateUser, updateUserPassword } from "../api/userService";

export default function ProfileScreen({ onLogout }) {
  const { user, token } = useAuth();
  const [showEdit, setShowEdit] = useState(false);

  // Form per l'admin: email/nome/cognome (via update) + cambio password (via updatepassword)
  const [form, setForm] = useState({ email: "", firstName: "", lastName: "", oldPassword: "", newPassword: "", confirmPassword: "" });
  const [formPwdErrors, setFormPwdErrors] = useState({});

  // Form per studente/docente (solo cambio password)
  const [pwdForm, setPwdForm] = useState({ oldPassword: "", newPassword: "", confirmPassword: "" });
  const [pwdErrors, setPwdErrors] = useState({});

  const role = user?.role?.toLowerCase();
  const isAdmin = role === "admin";

  const name = `${user?.studentFirstName || user?.teacherFirstName || ""} ${user?.studentLastName || user?.teacherLastName || ""}`.trim();

  function resetForms() {
    setForm({ email: "", firstName: "", lastName: "", oldPassword: "", newPassword: "", confirmPassword: "" });
    setPwdForm({ oldPassword: "", newPassword: "", confirmPassword: "" });
    setFormPwdErrors({});
    setPwdErrors({});
  }

  function updateFormField(field, value) {
    setForm(f => {
      const next = { ...f, [field]: value };
      setFormPwdErrors(getPasswordErrors(next.newPassword, next.confirmPassword, next.oldPassword));
      return next;
    });
  }

  function updatePwdField(field, value) {
    setPwdForm(f => {
      const next = { ...f, [field]: value };
      setPwdErrors(getPasswordErrors(next.newPassword, next.confirmPassword, next.oldPassword));
      return next;
    });
  }

  async function save() {
    // Admin: email/firstName/lastName tramite update, password tramite updatepassword
    const body = {};
    if (form.email) body.email = form.email;
    if (form.firstName) body.firstName = form.firstName;
    if (form.lastName) body.lastName = form.lastName;

    const wantsPasswordChange = form.oldPassword || form.newPassword || form.confirmPassword;

    if (Object.keys(body).length === 0 && !wantsPasswordChange) {
      Alert.alert("Attenzione", "Inserisci almeno un campo da modificare");
      return;
    }

    if (wantsPasswordChange) {
      if (!form.oldPassword || !form.newPassword || !form.confirmPassword) {
        Alert.alert("Attenzione", "Per cambiare la password inserisci vecchia password, nuova password e conferma");
        return;
      }
      const errors = getPasswordErrors(form.newPassword, form.confirmPassword, form.oldPassword);
      if (Object.keys(errors).length > 0) { setFormPwdErrors(errors); return; }
    }

    try {
      if (Object.keys(body).length > 0) {
        await updateUser(user.id, body, token);
      }
      if (wantsPasswordChange) {
        await updateUserPassword(user.id, { oldPassword: form.oldPassword, newPassword: form.newPassword }, token);
      }
      Alert.alert("Successo", "Profilo aggiornato!");
      setShowEdit(false);
      resetForms();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function savePassword() {
    // Studente/Docente: solo password tramite updatepassword
    if (!pwdForm.oldPassword || !pwdForm.newPassword || !pwdForm.confirmPassword) {
      Alert.alert("Attenzione", "Inserisci vecchia password, nuova password e conferma");
      return;
    }
    const errors = getPasswordErrors(pwdForm.newPassword, pwdForm.confirmPassword, pwdForm.oldPassword);
    if (Object.keys(errors).length > 0) { setPwdErrors(errors); return; }

    try {
      await updateUserPassword(user.id, { oldPassword: pwdForm.oldPassword, newPassword: pwdForm.newPassword }, token);
      Alert.alert("Successo", "Password aggiornata!");
      setShowEdit(false);
      resetForms();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  return (
    <ScrollView style={{ flex: 1, backgroundColor: C.bg }} contentContainerStyle={{ padding: 16 }}>
      <View style={s.profileHeader}>
        <View style={s.profileAvatar}>
          <Text style={s.profileAvatarText}>{(user?.email || "?")[0].toUpperCase()}</Text>
        </View>
        {name && <Text style={s.profileName}>{name}</Text>}
        <Text style={s.profileEmail}>{user?.email}</Text>
        <Badge text={role} role={role} />
      </View>

      <Card style={{ marginTop: 16 }}>
        <Text style={[s.cardTitle, { fontSize: 14 }]}>Dettagli account</Text>
        <View style={s.profileRow}><Text style={s.profileKey}>Email</Text><Text style={s.profileVal}>{user?.email}</Text></View>
        <View style={s.profileRow}><Text style={s.profileKey}>Ruolo</Text><Text style={s.profileVal}>{role}</Text></View>
        {user?.studentId && <View style={s.profileRow}><Text style={s.profileKey}>ID Studente</Text><Text style={s.profileVal}>{user.studentId.slice(0, 16)}…</Text></View>}
        {user?.teacherId && <View style={s.profileRow}><Text style={s.profileKey}>ID Docente</Text><Text style={s.profileVal}>{user.teacherId.slice(0, 16)}…</Text></View>}
      </Card>

      <Btn
        label={isAdmin ? "Modifica profilo" : "Cambia password"}
        onPress={() => setShowEdit(true)}
        style={{ marginTop: 16 }}
        variant="primary"
      />
      <Btn label="Logout" onPress={onLogout} style={{ marginTop: 10 }} variant="danger" />

      {isAdmin ? (
        <FormModal visible={showEdit} title="Modifica profilo" onClose={() => { setShowEdit(false); resetForms(); }}>
          <Input label="Nuova email (opzionale)" value={form.email} onChangeText={v => setForm(f => ({ ...f, email: v }))} keyboardType="email-address" autoCapitalize="none" />
          <Input label="Vecchia password (opzionale)" value={form.oldPassword} onChangeText={v => updateFormField("oldPassword", v)} secureTextEntry />
          <Input label="Nuova password (opzionale)" value={form.newPassword} onChangeText={v => updateFormField("newPassword", v)} secureTextEntry error={formPwdErrors.newPassword} />
          <Input label="Conferma nuova password" value={form.confirmPassword} onChangeText={v => updateFormField("confirmPassword", v)} secureTextEntry error={formPwdErrors.confirmPassword} />
          <Btn label="Salva" onPress={save} />
        </FormModal>
      ) : (
        <FormModal visible={showEdit} title="Cambia password" onClose={() => { setShowEdit(false); resetForms(); }}>
          <Input label="Vecchia password" value={pwdForm.oldPassword} onChangeText={v => updatePwdField("oldPassword", v)} secureTextEntry />
          <Input label="Nuova password" value={pwdForm.newPassword} onChangeText={v => updatePwdField("newPassword", v)} secureTextEntry error={pwdErrors.newPassword} />
          <Input label="Conferma nuova password" value={pwdForm.confirmPassword} onChangeText={v => updatePwdField("confirmPassword", v)} secureTextEntry error={pwdErrors.confirmPassword} />
          <Btn label="Salva" onPress={savePassword} />
        </FormModal>
      )}
    </ScrollView>
  );
}
