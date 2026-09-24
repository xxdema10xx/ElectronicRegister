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
import { getExams, createExam, updateExam, deleteExam } from "../api/examService";
import { getSubjects } from "../api/subjectService";

export default function ExamsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [exams, setExams] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ name: "", subjectId: "", date: ""});

  const load = useCallback(async () => {
      setLoading(true);
      try {
        const data = await getExams(search, token);
        setExams(Array.isArray(data) ? data : data ? [data] : []);
        const te = await getSubjects(token);
        setSubjects(Array.isArray(te) ? te : []);
      } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setExams([]); }
      finally { setLoading(false); }
    }, [token, search]);

    useEffect(() => { load(); }, [load]);
    
      async function add() {
        try {
          await createExam(form, token);
          setShowAdd(false); setForm({ name: "", subjectId: "", date: "" }); load();
        } catch (e) { Alert.alert("Errore", e.message); }
      }
    
      async function save() {
        try {
          await updateExam(showEdit.id, { name: showEdit.name, subjectId: showEdit.subjectId, date: showEdit.date }, token);
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
              <SectionHeader title="Esami" action={
                (role === "admin" || role === "teacher") && <Btn label="+ Esame" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
              } />
              <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per nome…"
                placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
            </View>
            {loading ? <Loader /> : exams.length === 0 ? <EmptyState message="Nessun esame trovato" /> :
              <FlatList
                data={exams}
                keyExtractor={exam => exam.id}
                contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
                renderItem={({ item: exam }) => (
                  <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
                    <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Text style={[s.avatarText, { color: "#92400E" }]}><Ionicons name="book-outline" size={24} /></Text></View>
                    <View style={{ flex: 1, marginLeft: 12 }}>
                      <Text style={s.itemTitle}>{exam.name}</Text>
                      {exam.subjectName && <Text style={s.itemSub}> <Ionicons name="book-outline" size={16} color={C.text} /> {exam.subjectName}</Text>}
                       {exam.date && <Text style={s.itemSub}> <Ionicons name="calendar-outline" size={16} color={C.text} /> {" "}{exam.date}</Text>}
                    </View>
                    {(role === "admin" || role === "teacher") && (
                      <View style={{ flexDirection: "row", gap: 8 }}>
                        <TouchableOpacity onPress={() => setShowEdit({ ...exam })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                        <TouchableOpacity onPress={() => remove(exam.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>
                      </View>
                    )}
                  </Card>
                )}
              />
            }
            <FormModal visible={showAdd} title="Aggiungi esame" onClose={() => setShowAdd(false)}>
              <Input label="Nome esame" value={form.name} onChangeText={v => setForm(f => ({ ...f, name: v }))} />
              <SelectField
                label="Materia"
                value={form.subjectId}
                options={subjects}
                getLabel={(t) => `${t.name}`}
                onSelect={(v) => setForm(f => ({ ...f, subjectId: v }))}
                placeholder="Seleziona materia"
                emptyMessage="Nessuna materia disponibile"
              />
              <DateField label="Data" value={form.date} onChange={(v) => setForm(f => ({ ...f, date: v }))} />
              <Btn label="Crea" onPress={add} />
            </FormModal>
            <FormModal visible={!!showEdit} title="Modifica esame" onClose={() => setShowEdit(null)}>
              {showEdit && <>
                <Input label="Nome" value={showEdit.name} onChangeText={v => setShowEdit(f => ({ ...f, name: v }))} />
                <SelectField
                  label="Materia"
                  value={showEdit.subjectId}
                  options={subjects}
                  getLabel={(t) => `${t.name}`}
                  onSelect={(v) => setShowEdit(f => ({ ...f, subjectId: v }))}
                  placeholder="Seleziona materia"
                  emptyMessage="Nessuna materia disponibile"
                />
                <DateField label="Data" value={showEdit.date} onChange={(v) => setShowEdit(f => ({ ...f, date: v }))} />
                <Btn label="Aggiorna" onPress={save} />
              </>}
            </FormModal>
          </View>
        );
      }