// src/screens/StudentsScreen.js
// Anagrafica studenti. Admin e Staff creano e modificano; solo l'admin elimina (docs/AUTH.md §4).
import { useCallback, useEffect, useState } from "react";
import { Alert, FlatList, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import FormModal from "../components/FormModal";
import StudentGrades from "../components/StudentGrades";
import { filterBySearch } from "../utils/search";
import { getStudents, createStudent, updateStudent, deleteStudent } from "../api/studentService";

const emptyForm = { firstName: "", lastName: "" };

export default function StudentsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const canEdit = role === "admin" || role === "staff";
  const canDelete = role === "admin";
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const visibleStudents = filterBySearch(students, search, x => [x.firstName, x.lastName]);
  const [showAdd, setShowAdd] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [showEdit, setShowEdit] = useState(null);
  const [saving, setSaving] = useState(false);
  const [gradesOf, setGradesOf] = useState(null); // studente di cui si vedono i voti (admin e staff)

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getStudents("", token);
      setStudents(Array.isArray(data) ? data : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setStudents([]); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  async function add() {
    if (!form.firstName.trim() || !form.lastName.trim()) { Alert.alert("Attenzione", "Inserisci nome e cognome"); return; }
    setSaving(true);
    try {
      await createStudent(form, token);
      setShowAdd(false); setForm(emptyForm); load();
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setSaving(false); }
  }

  async function save() {
    if (!showEdit.firstName.trim() || !showEdit.lastName.trim()) { Alert.alert("Attenzione", "Inserisci nome e cognome"); return; }
    setSaving(true);
    try {
      await updateStudent(showEdit.id, { firstName: showEdit.firstName, lastName: showEdit.lastName }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setSaving(false); }
  }

  async function remove(id) {
    Alert.alert("Conferma", "Eliminare studente?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await deleteStudent(id, token); load(); }
        catch (e) { Alert.alert("Errore", e.status === 409 ? "Lo studente non può essere eliminato: ha voti o altri dati collegati." : e.message); }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Studenti" action={
          canEdit && <Btn label="+ Studente" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per nome o cognome…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : visibleStudents.length === 0 ? <EmptyState message="Nessuno studente trovato" /> :
        <FlatList
          data={visibleStudents}
          keyExtractor={st => st.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: st }) => (
            <TouchableOpacity activeOpacity={canEdit ? 0.7 : 1} onPress={() => canEdit && setGradesOf(st)}>
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={s.avatar}><Text style={s.avatarText}>{st.firstName[0]}{st.lastName[0]}</Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{st.firstName} {st.lastName}</Text>
                {canEdit && <Text style={s.itemSub}>Clicca per vedere i voti</Text>}
              </View>
              {canEdit && (
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...st })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                  {canDelete && <TouchableOpacity onPress={() => remove(st.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>}
                </View>
              )}
            </Card>
            </TouchableOpacity>
          )}
        />
      }
      <StudentGrades student={gradesOf} token={token} canDelete={canDelete} onClose={() => setGradesOf(null)} />
      <FormModal visible={showAdd} title="Nuovo studente" onClose={() => setShowAdd(false)}>
        <Input label="Nome" value={form.firstName} onChangeText={v => setForm(f => ({ ...f, firstName: v }))} />
        <Input label="Cognome" value={form.lastName} onChangeText={v => setForm(f => ({ ...f, lastName: v }))} />
        <Btn label="Salva" onPress={add} loading={saving} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica studente" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.firstName} onChangeText={v => setShowEdit(f => ({ ...f, firstName: v }))} />
          <Input label="Cognome" value={showEdit.lastName} onChangeText={v => setShowEdit(f => ({ ...f, lastName: v }))} />
          <Btn label="Aggiorna" onPress={save} loading={saving} />
        </>}
      </FormModal>
    </View>
  );
}
