// src/screens/TestScreen.js
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
import { getTests, createTest, updateTest, deleteTest } from "../api/testService";
import { getExams } from "../api/examService";
import { getSubjects } from "../api/subjectService";
import * as DocumentPicker from "expo-document-picker";

export default function TestsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [tests, setTests] = useState([]);
  const [exams, setExams] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ name: "", examId: "", subjectId: "", numberOfStudents: "",
    totalQuestions: "", closedQuestions: "", openQuestions: "" });
  const [attachment, setAttachment] = useState(null);


  const load = useCallback(async () => {
      setLoading(true);
      try {
        const data = await getTests(search, token);
        setTests(Array.isArray(data) ? data : data ? [data] : []);
        const examData = await getExams(token);
        setExams(Array.isArray(examData) ? examData : []);
        const subjectData = await getSubjects(token);
        setSubjects(Array.isArray(subjectData) ? subjectData : []);
      } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setTests([]); }
      finally { setLoading(false); }
    }, [token, search]);

    useEffect(() => { load(); }, [load]);

        async function add() {
            try {
              await createTest(form, token);
              setShowAdd(false);
              setForm({ name: "", examId: "", subjectId: "", numberOfStudents: "",
                        totalQuestions: "", closedQuestions: "", openQuestions: "" });
              setAttachment(null);
              load();
            } catch (e) {
                Alert.alert("Errore", e.message);
            }
        }
        
        async function save() {
            try {
              await updateTest(showEdit.id, { name: showEdit.name, examId: showEdit.examId, subjectId: showEdit.subjectId,
                numberOfStudents: showEdit.numberOfStudents, totalQuestions: showEdit.totalQuestions, closedQuestions: showEdit.closedQuestions,
                openQuestions: showEdit.openQuestions }, token);
              setShowEdit(null);
              load();
            } catch (e) { Alert.alert("Errore", e.message); }
        }
        
        async function remove(id) {
            Alert.alert("Conferma", "Eliminare test?", [
              { text: "Annulla" },
              { text: "Elimina", style: "destructive", onPress: async () => {
                try { await deleteTest(id, token);
                load(); }
                catch (e) {
                  Alert.alert("Errore", "Il test non puo' essere eliminato.");
                }
              }},
            ]);
        }

        async function handleAttach() {
            try {
                const result = await DocumentPicker.getDocumentAsync({
                type: "*/*",
                copyToCacheDirectory: true,
                multiple: false,
                });

                if (!result.canceled && result.assets?.length > 0) {
                const file = result.assets[0];

                setAttachment(file);

                Alert.alert(
                    "File selezionato",
                    file.name
                );
                }
            } catch (e) {
                Alert.alert("Errore", "Impossibile selezionare il file.");
            }
        }

        return (
            <View style={{ flex: 1, backgroundColor: C.bg }}>
                <View style={s.screenPad}>
                    <SectionHeader title="Test" action={
                    (role === "admin" || role === "teacher") && <Btn label="+ Test" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
                    } />
                    <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per nome…"
                    placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
                </View>
                {loading ? <Loader /> : tests.length === 0 ? <EmptyState message="Nessun test trovato" /> :
                <FlatList
                data={tests}
                keyExtractor={test => test.id}
                contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
                renderItem={({ item: test }) => (
                    <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
                    <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Text style={[s.avatarText, { color: "#92400E" }]}><Ionicons name="book-outline" size={24} /></Text></View>
                    <View style={{ flex: 1, marginLeft: 12 }}>
                        <Text style={s.itemTitle}>{test.name}</Text>
                        {test.examName && <Text style={s.itemSub}> <Ionicons name="book-outline" size={16} color={C.text} /> {test.examName}</Text>}
                        {test.subjectName && <Text style={s.itemSub}> <Ionicons name="book-outline" size={16} color={C.text} /> {test.subjectName}</Text>}
                    </View>
                    {(role === "admin" || role === "teacher") && (
                        <View style={{ flexDirection: "row", gap: 8 }}>
                        <TouchableOpacity onPress={() => setShowEdit({ ...test })}><Ionicons name="create-outline" size={32} color={C.footer} /></TouchableOpacity>
                        <TouchableOpacity onPress={() => remove(test.id)}><Ionicons name="trash-outline" size={32} color={C.footer} /></TouchableOpacity>
                        </View>
                    )}
                    </Card>
                )}
                />
            }
            <FormModal visible={showAdd} title="Aggiungi test" onClose={() => setShowAdd(false)}>
                <Input label="Nome test" value={form.name} onChangeText={v => setForm(f => ({ ...f, name: v }))} />
                <SelectField
                label="Esame"
                value={form.examId}
                options={exams}
                getLabel={(t) => t.name}
                onSelect={(v) => setForm(f => ({ ...f, examId: v }))}
                placeholder="Seleziona esame"
                emptyMessage="Nessun esame disponibile"
                />
                <SelectField
                label="Materia"
                value={form.subjectId}
                options={subjects}
                getLabel={(t) => t.name}
                onSelect={(v) => setForm(f => ({ ...f, subjectId: v }))}
                placeholder="Seleziona materia"
                emptyMessage="Nessuna materia disponibile"
                />
                <Input label="Numero di studenti" value={form.numberOfStudents} onChangeText={v => setForm(f => ({ ...f, numberOfStudents: v }))} keyboardType="number-pad" />
                <Input label="Numero di domande" value={form.totalQuestions} onChangeText={v => setForm(f => ({ ...f, totalQuestions: v }))} keyboardType="number-pad" />
                <Input label="Domande chiuse" value={form.closedQuestions} onChangeText={v => setForm(f => ({ ...f, closedQuestions: v }))} keyboardType="number-pad" />
                <Input label="Domande aperte" value={form.openQuestions} onChangeText={v => setForm(f => ({ ...f, openQuestions: v }))} keyboardType="number-pad" />
                <Btn label="Allega" onPress={handleAttach} />
                {attachment && (
                <Text style={{ marginTop: 8, marginBottom: 8, color: C.text }}>
                    📎 {attachment.name}
                </Text>
                )}
                <Btn label="Crea" onPress={add} />
            </FormModal>
            <FormModal visible={!!showEdit} title="Modifica test" onClose={() => setShowEdit(null)}>
                {showEdit && <>
                <Input label="Nome test" value={showEdit.name} onChangeText={v => setShowEdit(f => ({ ...f, name: v }))} />
                <SelectField
                label="Esame"
                value={showEdit.examId}
                options={exams}
                getLabel={(t) => t.name}
                onSelect={(v) => setShowEdit(f => ({ ...f, examId: v }))}
                placeholder="Seleziona esame"
                emptyMessage="Nessun esame disponibile"
                />
                <SelectField
                label="Materia"
                value={showEdit.subjectId}
                options={subjects}
                getLabel={(t) => t.name}
                onSelect={(v) => setShowEdit(f => ({ ...f, subjectId: v }))}
                placeholder="Seleziona materia"
                emptyMessage="Nessuna materia disponibile"
                />
                <Input label="Numero di studenti" value={showEdit.numberOfStudents} onChangeText={v => setShowEdit(f => ({ ...f, numberOfStudents: v }))} keyboardType="number-pad" />
                <Input label="Numero di domande" value={showEdit.totalQuestions} onChangeText={v => setShowEdit(f => ({ ...f, totalQuestions: v }))} keyboardType="number-pad" />
                <Input label="Domande chiuse" value={showEdit.closedQuestions} onChangeText={v => setShowEdit(f => ({ ...f, closedQuestions: v }))} keyboardType="number-pad" />
                <Input label="Domande aperte" value={showEdit.openQuestions} onChangeText={v => setShowEdit(f => ({ ...f, openQuestions: v }))} keyboardType="number-pad" />
                <Btn label="Aggiorna" onPress={save} />
                </>}
            </FormModal>
            </View>
            );
            }