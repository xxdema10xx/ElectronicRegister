// src/screens/MyClassesScreen.js
// Solo insegnanti: elenco delle classi in cui insegnano (non tutti gli studenti del registro).
// Toccando una classe si vedono le materie insegnate in quella classe e gli studenti che la frequentano.
import { useCallback, useEffect, useState } from "react";
import { FlatList, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import Btn from "../components/Btn";
import Input from "../components/Input";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import SelectField from "../components/SelectField";
import DateField from "../components/DateField";
import FormModal from "../components/FormModal";
import GradeCircle from "../components/GradeCircle";
import { filterBySearch } from "../utils/search";
import { getMyClasses } from "../api/classService";
import { getGradesForStudent, addClassGrade, updateClassGrade, deleteClassGrade } from "../api/classGradeService";
import { notify, confirmAction } from "../utils/dialogs";

const initials = (a, b) => `${a?.[0] ?? ""}${b?.[0] ?? ""}`.toUpperCase();

const DETAIL_TABS = [
  { key: "subjects", label: "Materie" },
  { key: "students", label: "Studenti" },
];

// Il collegamento classe↔docente non esiste ancora lato backend: questi dati sono simulati
// (vedi getMyClasses in classService.js), quindi lo segnaliamo in UI per non trarre in inganno.
const MockBadge = () => (
  <View style={{ backgroundColor: "#FEF3C7", borderRadius: 12, paddingVertical: 4, paddingHorizontal: 10 }}>
    <Text style={{ color: "#92400E", fontSize: 12, fontWeight: "700" }}>Dati di prova</Text>
  </View>
);

// Voti di uno studente, limitati alle materie che il docente insegna in questa classe: può
// aggiungere, modificare ed eliminare un voto, ma solo per quelle materie.
function StudentGradesSheet({ student, klassSubjects, onClose }) {
  const [grades, setGrades] = useState(null);
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [newGrade, setNewGrade] = useState({ subjectId: "", value: "", date: "" });

  const subjectName = (id) => klassSubjects.find(sb => sb.subjectId === id)?.subjectName || id?.slice(0, 8) || "—";
  const load = useCallback(() => setGrades(getGradesForStudent(student.id)), [student.id]);
  useEffect(() => { load(); }, [load]);

  function addGrade() {
    if (!newGrade.subjectId || !newGrade.value || !newGrade.date) return notify("Attenzione", "Compila materia, voto e data.");
    const n = Number(newGrade.value);
    if (!(n >= 1 && n <= 10)) return notify("Attenzione", "Il voto deve essere compreso tra 1 e 10.");
    addClassGrade({ studentId: student.id, ...newGrade });
    setShowAdd(false);
    setNewGrade({ subjectId: "", value: "", date: "" });
    load();
  }
  function saveEdit() {
    const n = Number(showEdit.value);
    if (!(n >= 1 && n <= 10)) return notify("Attenzione", "Il voto deve essere compreso tra 1 e 10.");
    updateClassGrade(showEdit.id, { subjectId: showEdit.subjectId, value: showEdit.value, date: showEdit.date });
    setShowEdit(null);
    load();
  }
  function removeGrade(id) {
    confirmAction("Elimina voto", "Eliminare questo voto?", () => { deleteClassGrade(id); load(); });
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 10, marginBottom: 8 }}>
          <TouchableOpacity onPress={onClose}><Ionicons name="arrow-back" size={26} color={C.text} /></TouchableOpacity>
          <Text style={{ fontSize: 20, fontWeight: "700", color: C.text, flex: 1 }}>{student.firstName} {student.lastName}</Text>
          <Btn label="+ Voto" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
        </View>
      </View>

      {(grades?.length ?? 0) === 0 ? <EmptyState message="Nessun voto per questo studente" /> : (
        <FlatList
          data={grades}
          keyExtractor={g => g.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: g }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <GradeCircle value={g.value} />
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{subjectName(g.subjectId)}</Text>
                <Text style={s.itemSub}>{g.date}</Text>
              </View>
              <View style={{ flexDirection: "row", gap: 6 }}>
                <TouchableOpacity onPress={() => setShowEdit({ ...g })}><Ionicons name="create-outline" size={26} color={C.footer} /></TouchableOpacity>
                <TouchableOpacity onPress={() => removeGrade(g.id)}><Ionicons name="trash-outline" size={26} color={C.footer} /></TouchableOpacity>
              </View>
            </Card>
          )}
        />
      )}

      <FormModal visible={showAdd} title="Aggiungi voto" onClose={() => setShowAdd(false)}>
        <SelectField label="Materia" value={newGrade.subjectId} options={klassSubjects} getLabel={sb => sb.subjectName} getValue={sb => sb.subjectId}
          onSelect={(v) => setNewGrade(f => ({ ...f, subjectId: v }))} placeholder="Seleziona materia…" emptyMessage="Nessuna materia insegnata in questa classe" />
        <Input label="Voto (1-10)" value={newGrade.value} onChangeText={v => setNewGrade(f => ({ ...f, value: v }))} keyboardType="decimal-pad" />
        <DateField label="Data" value={newGrade.date} onChange={(v) => setNewGrade(f => ({ ...f, date: v }))} />
        <Btn label="Salva" onPress={addGrade} />
      </FormModal>

      <FormModal visible={!!showEdit} title="Modifica voto" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <SelectField label="Materia" value={showEdit.subjectId} options={klassSubjects} getLabel={sb => sb.subjectName} getValue={sb => sb.subjectId}
            onSelect={(v) => setShowEdit(f => ({ ...f, subjectId: v }))} placeholder="Seleziona materia…" />
          <Input label="Voto" value={String(showEdit.value)} onChangeText={v => setShowEdit(f => ({ ...f, value: v }))} keyboardType="decimal-pad" />
          <DateField label="Data" value={showEdit.date} onChange={(v) => setShowEdit(f => ({ ...f, date: v }))} />
          <Btn label="Aggiorna" onPress={saveEdit} />
        </>}
      </FormModal>
    </View>
  );
}

export default function MyClassesScreen() {
  const { token, user } = useAuth();
  const [classes, setClasses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [klass, setKlass] = useState(null); // classe aperta
  const [tab, setTab] = useState("subjects");
  const [openStudent, setOpenStudent] = useState(null); // studente aperto (voti)
  const visibleClasses = filterBySearch(classes, search, c => [c.name]);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await getMyClasses(user?.teacherId, token);
      setClasses(Array.isArray(data) ? data : []);
    } catch (e) { notify("Errore", e.message); setClasses([]); }
    finally { setLoading(false); }
  }, [token, user?.teacherId]);

  useEffect(() => { load(); }, [load]);

  if (klass && openStudent) {
    return <StudentGradesSheet student={openStudent} klassSubjects={klass.subjects} onClose={() => setOpenStudent(null)} />;
  }

  if (klass) {
    const list = tab === "subjects" ? klass.subjects : klass.students;
    return (
      <View style={{ flex: 1, backgroundColor: C.bg }}>
        <View style={s.screenPad}>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 10, marginBottom: 8 }}>
            <TouchableOpacity onPress={() => setKlass(null)}><Ionicons name="arrow-back" size={26} color={C.text} /></TouchableOpacity>
            <Text style={{ fontSize: 20, fontWeight: "700", color: C.text, flex: 1 }}>Classe {klass.name}</Text>
            <MockBadge />
          </View>
        </View>

        <View style={{ flexDirection: "row", marginHorizontal: 16, marginBottom: 10, backgroundColor: "#E2E8F0", borderRadius: 10, padding: 3 }}>
          {DETAIL_TABS.map(t => {
            const count = t.key === "subjects" ? klass.subjects.length : klass.students.length;
            const active = tab === t.key;
            return (
              <TouchableOpacity key={t.key} onPress={() => setTab(t.key)} activeOpacity={0.7}
                style={{ flex: 1, paddingVertical: 8, borderRadius: 8, alignItems: "center", backgroundColor: active ? C.white : "transparent" }}>
                <Text style={{ color: active ? C.primary : C.textMuted, fontWeight: active ? "700" : "500" }}>{t.label} ({count})</Text>
              </TouchableOpacity>
            );
          })}
        </View>

        <FlatList
          data={list}
          keyExtractor={(it, i) => String(it.id ?? it.subjectId) + i}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          ListEmptyComponent={<EmptyState message="Nessun elemento" />}
          renderItem={({ item }) => tab === "subjects" ? (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Ionicons name="book-outline" size={22} color="#92400E" /></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{item.subjectName}</Text>
              </View>
            </Card>
          ) : (
            <TouchableOpacity activeOpacity={0.7} onPress={() => setOpenStudent(item)}>
              <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
                <View style={s.avatar}><Text style={s.avatarText}>{initials(item.firstName, item.lastName)}</Text></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{item.firstName} {item.lastName}</Text>
                  <Text style={s.itemSub}>Tocca per vedere i voti</Text>
                </View>
                <Ionicons name="chevron-forward" size={20} color={C.textLight} />
              </Card>
            </TouchableOpacity>
          )}
        />
      </View>
    );
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Classi" action={<MockBadge />} />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca classe…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : visibleClasses.length === 0 ? <EmptyState message="Nessuna classe assegnata" /> :
        <FlatList
          data={visibleClasses}
          keyExtractor={k => k.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: k }) => (
            <TouchableOpacity activeOpacity={0.7} onPress={() => { setTab("subjects"); setKlass(k); }}>
              <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
                <View style={s.avatar}><Ionicons name="people-outline" size={24} color="#1D4ED8" /></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{k.name}</Text>
                  <Text style={s.itemSub}>{k.students.length} studenti · {k.subjects.length} materie</Text>
                </View>
                <Ionicons name="chevron-forward" size={22} color={C.textLight} />
              </Card>
            </TouchableOpacity>
          )}
        />
      }
    </View>
  );
}
