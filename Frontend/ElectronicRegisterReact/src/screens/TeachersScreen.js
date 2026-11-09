// src/screens/TeachersScreen.js
// Anagrafica docenti. Admin e Staff creano e modificano; solo l'admin elimina (docs/AUTH.md §4).
import { useCallback, useEffect, useState } from "react";
import { Alert, FlatList, Text, TouchableOpacity, View } from "react-native";
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
import { getTeachers, createTeacher, updateTeacher, deleteTeacher } from "../api/teacherService";

const emptyForm = { firstName: "", lastName: "" };

export default function TeachersScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const canEdit = role === "admin" || role === "staff";
  const canDelete = role === "admin";
  const [teachers, setTeachers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [showEdit, setShowEdit] = useState(null);
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getTeachers(token);
      setTeachers(Array.isArray(data) ? data : []);
    } catch (e) { if (e.status !== 404) Alert.alert("Errore", e.message); setTeachers([]); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  async function add() {
    if (!form.firstName.trim() || !form.lastName.trim()) { Alert.alert("Attenzione", "Inserisci nome e cognome"); return; }
    setSaving(true);
    try {
      await createTeacher(form, token);
      setShowAdd(false); setForm(emptyForm); load();
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setSaving(false); }
  }

  async function save() {
    if (!showEdit.firstName.trim() || !showEdit.lastName.trim()) { Alert.alert("Attenzione", "Inserisci nome e cognome"); return; }
    setSaving(true);
    try {
      await updateTeacher(showEdit.id, { firstName: showEdit.firstName, lastName: showEdit.lastName }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setSaving(false); }
  }

  async function remove(id) {
    Alert.alert("Conferma", "Eliminare insegnante?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await deleteTeacher(id, token); load(); }
        catch (e) {
          Alert.alert("Errore", e.status === 409
            ? "Il docente insegna in una o più classi: assegna quelle materie a un altro docente prima di eliminarlo."
            : e.message);
        }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Professori" action={
          canEdit && <Btn label="+ Professore" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
      </View>
      {loading ? <Loader /> : teachers.length === 0 ? <EmptyState message="Nessun professore trovato" /> :
        <FlatList
          data={teachers}
          keyExtractor={t => t.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: t }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={[s.avatar, { backgroundColor: "#C7D2FE" }]}><Text style={[s.avatarText, { color: "#4338CA" }]}>{t.firstName[0]}{t.lastName[0]}</Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>Prof. {t.firstName} {t.lastName}</Text>
              </View>
              {canEdit && (
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...t })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                  {canDelete && <TouchableOpacity onPress={() => remove(t.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>}
                </View>
              )}
            </Card>
          )}
        />
      }
      <FormModal visible={showAdd} title="Nuovo professore" onClose={() => setShowAdd(false)}>
        <Input label="Nome" value={form.firstName} onChangeText={v => setForm(f => ({ ...f, firstName: v }))} />
        <Input label="Cognome" value={form.lastName} onChangeText={v => setForm(f => ({ ...f, lastName: v }))} />
        <Btn label="Salva" onPress={add} loading={saving} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica professore" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.firstName} onChangeText={v => setShowEdit(f => ({ ...f, firstName: v }))} />
          <Input label="Cognome" value={showEdit.lastName} onChangeText={v => setShowEdit(f => ({ ...f, lastName: v }))} />
          <Btn label="Aggiorna" onPress={save} loading={saving} />
        </>}
      </FormModal>
    </View>
  );
}
