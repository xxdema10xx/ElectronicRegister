// src/screens/UsersScreen.js
import { useCallback, useEffect, useState } from "react";
import { Alert, FlatList, Image, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import Badge from "../components/Badge";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import FormModal from "../components/FormModal";
import { getUsers, registerUser, updateUser, deleteUser } from "../api/userService";

const ROLES = ["student", "teacher", "admin"];

export default function UsersScreen() {
  const { token } = useAuth();
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showRegister, setShowRegister] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ email: "", password: "", role: "student", firstName: "", lastName: "" });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getUsers(search, token);
      setUsers(Array.isArray(data) ? data : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setUsers([]); }
    finally { setLoading(false); }
  }, [token, search]);

  useEffect(() => { load(); }, [load]);

  async function register() {
    try {
      await registerUser(form, token);
      setShowRegister(false); setForm({ email: "", password: "", role: "student", firstName: "", lastName: "" }); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function save() {
    try {
      await updateUser(showEdit.id, { email: showEdit.email, role: showEdit.role }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function remove(id) {
    Alert.alert("Conferma", "Eliminare utente?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await deleteUser(id, token); load(); }
        catch (e) { Alert.alert("Errore", e.message); }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Utenti" action={
          <Btn label="+ Utente" onPress={() => setShowRegister(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per nome…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : users.length === 0 ? <EmptyState message="Nessun utente trovato" /> :
        <FlatList
          data={users}
          keyExtractor={u => u.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: u }) => (
            <Card style={{ marginBottom: 10 }}>
              <View style={{ flexDirection: "row", alignItems: "center" }}>
                <View style={s.avatar}><Text style={s.avatarText}>{u.email[0].toUpperCase()}</Text></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{u.email}</Text>
                  {(u.studentFirstName || u.teacherFirstName) &&
                    <Text style={s.itemSub}>{u.studentFirstName || u.teacherFirstName} {u.studentLastName || u.teacherLastName}</Text>}
                </View>
                <Badge text={u.role} role={u.role} />
              </View>
              <View style={{ flexDirection: "row", justifyContent: "flex-end", gap: 8, marginTop: 8 }}>
                <TouchableOpacity onPress={() => setShowEdit({ ...u })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                <TouchableOpacity onPress={() => remove(u.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>
              </View>
            </Card>
          )}
        />
      }
      <FormModal visible={showRegister} title="Registra utente" onClose={() => setShowRegister(false)}>
        <Input label="Email" value={form.email} onChangeText={v => setForm(f => ({ ...f, email: v }))} keyboardType="email-address" autoCapitalize="none" />
        <Input label="Password" value={form.password} onChangeText={v => setForm(f => ({ ...f, password: v }))} secureTextEntry />
        <Text style={s.label}>Ruolo</Text>
        <View style={{ flexDirection: "row", gap: 8, marginBottom: 14 }}>
          {ROLES.map(r => (
            <TouchableOpacity key={r} style={[s.roleBtn, form.role === r && s.roleBtnActive]} onPress={() => setForm(f => ({ ...f, role: r }))}>
              <Text style={[s.roleBtnText, form.role === r && s.roleBtnTextActive]}>{r}</Text>
            </TouchableOpacity>
          ))}
        </View>
        {form.role !== "admin" && <>
          <Input label="Nome" value={form.firstName} onChangeText={v => setForm(f => ({ ...f, firstName: v }))} />
          <Input label="Cognome" value={form.lastName} onChangeText={v => setForm(f => ({ ...f, lastName: v }))} />
        </>}
        <Btn label="Registra" onPress={register} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica utente" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Email" value={showEdit.email} onChangeText={v => setShowEdit(f => ({ ...f, email: v }))} keyboardType="email-address" autoCapitalize="none" />
          <Text style={s.label}>Ruolo</Text>
          <View style={{ flexDirection: "row", gap: 8, marginBottom: 14 }}>
            {ROLES.map(r => (
              <TouchableOpacity key={r} style={[s.roleBtn, showEdit.role === r && s.roleBtnActive]} onPress={() => setShowEdit(f => ({ ...f, role: r }))}>
                <Text style={[s.roleBtnText, showEdit.role === r && s.roleBtnTextActive]}>{r}</Text>
              </TouchableOpacity>
            ))}
          </View>
          <Btn label="Aggiorna" onPress={save} />
        </>}
      </FormModal>
    </View>
  );
}
