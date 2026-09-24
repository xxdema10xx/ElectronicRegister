// src/screens/TeachersScreen.js
import { useCallback, useEffect, useState } from "react";
import { Alert, FlatList, Image, Text, TouchableOpacity, View } from "react-native";
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
import { getTeachers, updateTeacher, deleteTeacher } from "../api/teacherService";

export default function TeachersScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [teachers, setTeachers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showEdit, setShowEdit] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getTeachers(token);
      setTeachers(Array.isArray(data) ? data : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setTeachers([]); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  async function save() {
    try {
      await updateTeacher(showEdit.id, { firstName: showEdit.firstName, lastName: showEdit.lastName }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function remove(id) {
    Alert.alert("Conferma", "Eliminare insegnante?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await deleteTeacher(id, token); load(); }
        catch (e) {
          Alert.alert("Errore", "Il docente ha una o più materie a suo carico, assegnarle ad un altro prima di procedere all'eliminazione");
        }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Professori" />
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
              {role === "admin" && (
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...t })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                  <TouchableOpacity onPress={() => remove(t.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>
                </View>
              )}
            </Card>
          )}
        />
      }
      <FormModal visible={!!showEdit} title="Modifica professore" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.firstName} onChangeText={v => setShowEdit(f => ({ ...f, firstName: v }))} />
          <Input label="Cognome" value={showEdit.lastName} onChangeText={v => setShowEdit(f => ({ ...f, lastName: v }))} />
          <Btn label="Aggiorna" onPress={save} />
        </>}
      </FormModal>
    </View>
  );
}
