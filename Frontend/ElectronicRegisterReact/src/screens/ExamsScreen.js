// src/screens/ExamsScreen.js
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
import DateField from "../components/DateField";
import FormModal from "../components/FormModal";
import ExamTests from "../components/ExamTests";
import ExamForm from "../components/ExamForm";
import { useLayout } from "../hooks/useLayout";
import CheckboxField from "../components/CheckboxField";
import { filterBySearch } from "../utils/search";
import { DEMO_EXAMS } from "../data/examsMock";
import { getExams, createExam, updateExam, deleteExam } from "../api/examService";
import { getSubjects } from "../api/subjectService";

export default function ExamsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const { isDesktop } = useLayout();
  const [exams, setExams] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const visibleExams = filterBySearch(exams, search, e => [e.name, e.subjectName]);
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ name: "", subjectId: "", date: "", isRetake: false });

  const load = useCallback(async () => {
      setLoading(true);
      try {
        const data = await getExams("", token);
        const list = Array.isArray(data) ? data : data ? [data] : [];
        setExams(list.length === 0 ? DEMO_EXAMS : list);
        const te = await getSubjects(token);
        setSubjects(Array.isArray(te) ? te : []);
      } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setExams(DEMO_EXAMS); }
      finally { setLoading(false); }
    }, [token]);

    useEffect(() => { load(); }, [load]);
    
      async function add() {
        try {
          await createExam(form, token);
          setShowAdd(false); setForm({ name: "", subjectId: "", date: "", isRetake: false }); load();
        } catch (e) { Alert.alert("Errore", e.message); }
      }
    
      async function save() {
        try {
          await updateExam(showEdit.id, { name: showEdit.name, subjectId: showEdit.subjectId, date: showEdit.date, isRetake: !!showEdit.isRetake }, token);
          setShowEdit(null); load();
        } catch (e) { Alert.alert("Errore", e.message); }
      }
    
      async function remove(id) {
        Alert.alert("Conferma", "Eliminare esame?", [
          { text: "Annulla" },
          { text: "Elimina", style: "destructive", onPress: async () => {
            try { await deleteExam(id, token); load(); }
            catch (e) {
              Alert.alert("Errore", "L'esame non puo' essere eliminato.");
            }
          }},
        ]);
      }

      return (
          <View style={{ flex: 1, backgroundColor: C.bg }}>
            <View style={s.screenPad}>
              {isDesktop ? (
                // Web: ricerca e "+ Esame" sulla stessa riga (il titolo è già nella barra in alto)
                <View style={{ flexDirection: "row", alignItems: "center", gap: 12, marginTop: 8, marginBottom: 12 }}>
                  <TextInput style={[s.input, { flex: 1 }]} placeholder="Cerca per nome…"
                    placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
                  {(role === "admin" || role === "teacher") &&
                    <Btn label="Crea esame" onPress={() => setShowAdd(true)} style={{ height: 48, paddingHorizontal: 24 }} />}
                </View>
              ) : (<>
                <SectionHeader title="Esami" action={
                  (role === "admin" || role === "teacher") && <Btn label="Crea esame" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
                } />
                <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per nome…"
                  placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
              </>)}
            </View>
            {loading ? <Loader /> : visibleExams.length === 0 ? <EmptyState message="Nessun esame trovato" /> :
              <FlatList
                data={visibleExams}
                keyExtractor={exam => exam.id}
                contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
                renderItem={({ item: exam }) => (
                  <Card style={{ marginBottom: 10 }}>
                    <View style={{ flexDirection: "row", alignItems: "center" }}>
                    <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Text style={[s.avatarText, { color: "#92400E" }]}><Ionicons name="book-outline" size={24} /></Text></View>
                    <View style={{ flex: 1, marginLeft: 12 }}>
                      <Text style={s.itemTitle}>{exam.name}{exam.demo ? "  (solo prova)" : ""}{exam.isRetake ? "  · Recupero" : ""}</Text>
                      {exam.subjectName && <Text style={s.itemSub}> <Ionicons name="book-outline" size={16} color={C.text} /> {exam.subjectName}</Text>}
                       {exam.date && <Text style={s.itemSub}> <Ionicons name="calendar-outline" size={16} color={C.text} /> {" "}{exam.date}</Text>}
                    </View>
                    {(role === "admin" || role === "teacher") && !exam.demo && (
                      <View style={{ flexDirection: "row", gap: 8 }}>
                        <TouchableOpacity onPress={() => setShowEdit({ ...exam })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                        <TouchableOpacity onPress={() => remove(exam.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>
                      </View>
                    )}
                    </View>
                    <ExamTests exam={exam} token={token} canWrite={role === "admin" || role === "teacher"} />
                  </Card>
                )}
              />
            }
            <FormModal wide visible={showAdd} title="Nuovo esame" onClose={() => setShowAdd(false)}>
              <ExamForm value={form} onChange={setForm} subjects={subjects} onSubmit={add} onCancel={() => setShowAdd(false)} submitLabel="Crea esame" />
            </FormModal>
            <FormModal wide visible={!!showEdit} title="Modifica esame" onClose={() => setShowEdit(null)}>
              {showEdit && <ExamForm value={showEdit} onChange={setShowEdit} subjects={subjects} onSubmit={save} onCancel={() => setShowEdit(null)} submitLabel="Aggiorna" />}
            </FormModal>
          </View>
        );
      }