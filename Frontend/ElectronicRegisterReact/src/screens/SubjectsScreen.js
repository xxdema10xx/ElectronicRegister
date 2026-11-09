// src/screens/SubjectsScreen.js
// Materie dell'istituto. La materia non ha un docente: chi la insegna si decide classe per classe
// (classi-materie, visibili nel dettaglio di una classe).
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
import { filterBySearch } from "../utils/search";
import { getSubjects, createSubject, updateSubject, deleteSubject } from "../api/subjectService";

export default function SubjectsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const visibleSubjects = filterBySearch(subjects, search, x => [x.name]);
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ name: "" });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getSubjects("", token);
      setSubjects(Array.isArray(data) ? data : data ? [data] : []);
    } catch (e) { if (e.status !== 404) Alert.alert("Errore", e.message); setSubjects([]); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  async function add() {
    if (!form.name.trim()) { Alert.alert("Attenzione", "Inserisci il nome della materia"); return; }
    try {
      await createSubject(form, token);
      setShowAdd(false); setForm({ name: "" }); load();
    } catch (e) { Alert.alert("Errore", e.status === 409 ? "Esiste già una materia con questo nome." : e.message); }
  }

  async function save() {
    if (!showEdit.name.trim()) { Alert.alert("Attenzione", "Inserisci il nome della materia"); return; }
    try {
      await updateSubject(showEdit.id, { name: showEdit.name }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.status === 409 ? "Esiste già una materia con questo nome." : e.message); }
  }

  async function remove(id) {
    Alert.alert("Conferma", "Eliminare materia?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await deleteSubject(id, token); load(); }
        catch (e) {
          Alert.alert("Errore", e.status === 409 ? "La materia non può essere eliminata perché è assegnata a una o più classi." : e.message);
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
      {loading ? <Loader /> : visibleSubjects.length === 0 ? <EmptyState message="Nessuna materia trovata" /> :
        <FlatList
          data={visibleSubjects}
          keyExtractor={sub => sub.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: sub }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Text style={[s.avatarText, { color: "#92400E" }]}><Ionicons name="book-outline" size={24} /></Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{sub.name}</Text>
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
        <Btn label="Salva" onPress={add} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica materia" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.name} onChangeText={v => setShowEdit(f => ({ ...f, name: v }))} />
          <Btn label="Aggiorna" onPress={save} />
        </>}
      </FormModal>
    </View>
  );
}
