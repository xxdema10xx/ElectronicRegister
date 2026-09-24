// src/screens/StudentsScreen.js
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
import FormModal from "../components/FormModal";
import { getStudents, updateStudent, deleteStudent } from "../api/studentService";

export default function StudentsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showEdit, setShowEdit] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getStudents(search, token);
      setStudents(Array.isArray(data) ? data : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setStudents([]); }
    finally { setLoading(false); }
  }, [token, search]);

  useEffect(() => { load(); }, [load]);

  async function save() {
    try {
      await updateStudent(showEdit.id, { firstName: showEdit.firstName, lastName: showEdit.lastName }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function remove(id) {
    Alert.alert("Conferma", "Eliminare studente?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await deleteStudent(id, token); load(); }
        catch (e) { Alert.alert("Errore", e.message); }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Studenti" />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per cognome…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : students.length === 0 ? <EmptyState message="Nessuno studente trovato" /> :
        <FlatList
          data={students}
          keyExtractor={st => st.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: st }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={s.avatar}><Text style={s.avatarText}>{st.firstName[0]}{st.lastName[0]}</Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{st.firstName} {st.lastName}</Text>
              </View>
              {role === "admin" && (
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...st })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                  <TouchableOpacity onPress={() => remove(st.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>
                </View>
              )}
            </Card>
          )}
        />
      }
      <FormModal visible={!!showEdit} title="Modifica studente" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.firstName} onChangeText={v => setShowEdit(f => ({ ...f, firstName: v }))} />
          <Input label="Cognome" value={showEdit.lastName} onChangeText={v => setShowEdit(f => ({ ...f, lastName: v }))} />
          <Btn label="Aggiorna" onPress={save} />
        </>}
      </FormModal>
    </View>
  );
}
