// src/screens/SubjectsScreen.js
import { useCallback, useEffect, useState } from "react";
import { Alert, FlatList, Image, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import SelectField from "../components/SelectField";
import FormModal from "../components/FormModal";
import { getSubjects, createSubject, updateSubject, deleteSubject } from "../api/subjectService";
import { getTeachers } from "../api/teacherService";

export default function SubjectsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [subjects, setSubjects] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ name: "", teacherId: "" });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getSubjects(search, token);
      setSubjects(Array.isArray(data) ? data : data ? [data] : []);
      const te = await getTeachers(token);
      setTeachers(Array.isArray(te) ? te : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setSubjects([]); }
    finally { setLoading(false); }
  }, [token, search]);

  useEffect(() => { load(); }, [load]);

  async function add() {
    try {
      await createSubject(form, token);
      setShowAdd(false); setForm({ name: "", teacherId: "" }); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function save() {
    try {
      await updateSubject(showEdit.id, { name: showEdit.name, teacherId: showEdit.teacherId }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function remove(id) {
    Alert.alert("Conferma", "Eliminare materia?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await deleteSubject(id, token); load(); }
        catch (e) {
          Alert.alert("Errore", "La materia non può essere eliminata perché ha dei voti associati");
        }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Materie" action={
          role === "admin" && <Btn label="+ Materia" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per nome…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : subjects.length === 0 ? <EmptyState message="Nessuna materia trovata" /> :
        <FlatList
          data={subjects}
          keyExtractor={sub => sub.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: sub }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Text style={[s.avatarText, { color: "#92400E" }]}><Ionicons name="book-outline" size={24} /></Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{sub.name}</Text>
                {sub.teacherFirstName && <Text style={s.itemSub}> <Ionicons name="person-outline" size={16} color={C.text} /> {sub.teacherFirstName} {sub.teacherLastName}</Text>}
              </View>
              {role === "admin" && (
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...sub })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                  <TouchableOpacity onPress={() => remove(sub.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>
                </View>
              )}
            </Card>
          )}
        />
      }
      <FormModal visible={showAdd} title="Aggiungi materia" onClose={() => setShowAdd(false)}>
        <Input label="Nome materia" value={form.name} onChangeText={v => setForm(f => ({ ...f, name: v }))} />
        <SelectField
          label="Professore"
          value={form.teacherId}
          options={teachers}
          getLabel={(t) => `${t.firstName} ${t.lastName}`}
          onSelect={(v) => setForm(f => ({ ...f, teacherId: v }))}
          placeholder="Seleziona professore…"
          emptyMessage="Nessun professore disponibile"
        />
        <Btn label="Salva" onPress={add} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica materia" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.name} onChangeText={v => setShowEdit(f => ({ ...f, name: v }))} />
          <SelectField
            label="Professore"
            value={showEdit.teacherId}
            options={teachers}
            getLabel={(t) => `${t.firstName} ${t.lastName}`}
            onSelect={(v) => setShowEdit(f => ({ ...f, teacherId: v }))}
            placeholder="Seleziona professore…"
            emptyMessage="Nessun professore disponibile"
          />
          <Btn label="Aggiorna" onPress={save} />
        </>}
      </FormModal>
    </View>
  );
}
