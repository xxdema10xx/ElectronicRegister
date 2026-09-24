// src/screens/GradesScreen.js
import { useCallback, useEffect, useRef, useState } from "react";
import { ActivityIndicator, Alert, FlatList, Image, RefreshControl, Text, TouchableOpacity, View } from "react-native";
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
import { getGradesPage, getGradeFilters, createGrade, updateGrade, deleteGrade } from "../api/gradeService";
import { getSubjects } from "../api/subjectService";
import { getStudents } from "../api/studentService";
import { getTeachers } from "../api/teacherService";

const GRADES_PAGE_SIZE = 20;

export default function GradesScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [grades, setGrades] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [students, setStudents] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  // Filtri come su grades.html: materia e allievo sono select (solo tra le
  // materie/allievi che hanno almeno un voto), la data è un calendario.
  const [filterSubjectId, setFilterSubjectId] = useState("");
  const [filterStudentId, setFilterStudentId] = useState("");
  const [filterDate, setFilterDate] = useState("");
  const [filterOptions, setFilterOptions] = useState({ subjects: [], students: [] });
  const [filtersOpen, setFiltersOpen] = useState(false);
  const [newGrade, setNewGrade] = useState({ studentId: "", subjectId: "", teacherId: "", value: "", date: "" });
  // Paginazione lato server (come sul web): carichiamo GRADES_PAGE_SIZE voti
  // alla volta, aggiungendo altre pagine man mano che l'utente scorre verso
  // il basso.
  const [loadingMore, setLoadingMore] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const pageRef = useRef(0);
  const totalRef = useRef(0);

  const hasActiveFilters = !!(filterSubjectId || filterStudentId || filterDate);

  function clearFilters() {
    setFilterSubjectId("");
    setFilterStudentId("");
    setFilterDate("");
  }

  const fetchGradesPage = useCallback((pageNumber) => (
    getGradesPage({
      pageNumber,
      pageSize: GRADES_PAGE_SIZE,
      subjectId: filterSubjectId,
      studentId: filterStudentId,
      date: filterDate,
    }, token)
  ), [token, filterSubjectId, filterStudentId, filterDate]);

  // Anagrafiche e opzioni dei filtri: non dipendono dai filtri attivi, quindi
  // vengono ricaricate solo al mount e col pull-to-refresh, non a ogni tap
  // su un filtro.
  const loadMeta = useCallback(async () => {
    try {
      const sub = await getSubjects(undefined, token);
      setSubjects(Array.isArray(sub) ? sub : []);
      if (role !== "student") {
        const st = await getStudents(undefined, token);
        setStudents(Array.isArray(st) ? st : []);
        const te = await getTeachers(token);
        setTeachers(Array.isArray(te) ? te : []);
      }
      const filters = await getGradeFilters(token).catch(() => ({ subjects: [], students: [] }));
      setFilterOptions({
        subjects: Array.isArray(filters?.subjects) ? filters.subjects : [],
        students: Array.isArray(filters?.students) ? filters.students : [],
      });
    } catch (e) { /* non bloccante per la lista voti */ }
  }, [token, role]);

  const loadGrades = useCallback(async ({ isRefresh } = {}) => {
    isRefresh ? setRefreshing(true) : setLoading(true);
    try {
      const result = await fetchGradesPage(1);
      pageRef.current = 1;
      totalRef.current = result.totalCount;
      setGrades(result.items);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setGrades([]); }
    finally { isRefresh ? setRefreshing(false) : setLoading(false); }
  }, [fetchGradesPage]);

  useEffect(() => { loadMeta(); }, [loadMeta]);
  useEffect(() => { loadGrades(); }, [loadGrades]);

  const hasMore = grades.length < totalRef.current;

  async function loadMoreGrades() {
    if (loadingMore || loading || refreshing || !hasMore) return;
    setLoadingMore(true);
    try {
      const nextPage = pageRef.current + 1;
      const result = await fetchGradesPage(nextPage);
      pageRef.current = nextPage;
      totalRef.current = result.totalCount;
      setGrades(prev => prev.concat(result.items));
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setLoadingMore(false); }
  }

  function onRefresh() {
    loadMeta();
    loadGrades({ isRefresh: true });
  }

  function refreshAll() {
    loadMeta();
    loadGrades();
  }

  function subjectName(id) { return subjects.find(s => s.id === id)?.name || id?.slice(0, 8) || "—"; }
  function studentName(id) { const st = students.find(s => s.id === id); return st ? `${st.firstName} ${st.lastName}` : id?.slice(0, 8) || "—"; }

  async function addGrade() {
    try {
      await createGrade(newGrade, token);
      setShowAdd(false);
      setNewGrade({ studentId: "", subjectId: "", teacherId: "", value: "", date: "" });
      refreshAll();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function saveEditedGrade() {
    try {
      await updateGrade(showEdit.id, { subjectId: showEdit.subjectId, value: showEdit.value, date: showEdit.date }, token);
      setShowEdit(null);
      refreshAll();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function removeGrade(id) {
    Alert.alert("Conferma", "Eliminare questo voto?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await deleteGrade(id, token); refreshAll(); }
        catch (e) { Alert.alert("Errore", e.message); }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Voti" action={
          (role === "teacher" || role === "admin") &&
          <Btn label="+ Voto" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
        {/* Filters */}
        <View style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginBottom: filtersOpen ? 8 : 4 }}>
          <TouchableOpacity onPress={() => setFiltersOpen(o => !o)} style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
            <Ionicons name={filtersOpen ? "chevron-up-outline" : "chevron-down-outline"} size={18} color={C.primary} />
            <Text style={{ color: C.primary, fontWeight: "600" }}>Filtri{hasActiveFilters ? " •" : ""}</Text>
          </TouchableOpacity>
          {hasActiveFilters && (
            <TouchableOpacity onPress={clearFilters}>
              <Text style={{ color: C.danger, fontWeight: "600" }}>✕ Cancella filtri</Text>
            </TouchableOpacity>
          )}
        </View>
        {filtersOpen && (
          <>
            <View style={{ flexDirection: "row", gap: 8, alignItems: "flex-start" }}>
              <View style={{ flex: 1 }}>
                <SelectField
                  label="Materia"
                  value={filterSubjectId}
                  options={[{ id: "", label: "Tutte le materie" }, ...filterOptions.subjects.map(sub => ({ id: sub.id, label: sub.name }))]}
                  getLabel={o => o.label}
                  getValue={o => o.id}
                  onSelect={setFilterSubjectId}
                />
              </View>
              <View style={{ flex: 1 }}>
                <DateField label="Data" value={filterDate} onChange={setFilterDate} placeholder="Tutte le date" />
              </View>
            </View>
            {role !== "student" && (
              <SelectField
                label="Allievo"
                value={filterStudentId}
                options={[{ id: "", label: "Tutti gli allievi" }, ...filterOptions.students.map(st => ({ id: st.id, label: `${st.firstName} ${st.lastName}` }))]}
                getLabel={o => o.label}
                getValue={o => o.id}
                onSelect={setFilterStudentId}
              />
            )}
          </>
        )}
      </View>
      {loading ? <Loader /> :
        <FlatList
          data={grades}
          keyExtractor={g => g.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32, flexGrow: 1 }}
          ListEmptyComponent={<EmptyState message="Nessun voto trovato" />}
          onEndReached={loadMoreGrades}
          onEndReachedThreshold={0.4}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} colors={[C.primary]} tintColor={C.primary} />}
          ListFooterComponent={loadingMore ? (
            <View style={{ paddingVertical: 16, alignItems: "center" }}>
              <ActivityIndicator size="small" color={C.primary} />
            </View>
          ) : null}
          renderItem={({ item: g }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <GradeCircle value={g.value} />
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{subjectName(g.subjectId)}</Text>
                {role !== "student" && <Text style={s.itemSub}>👤 {studentName(g.studentId)}</Text>}
                <Text style={s.itemSub}>📅 {g.date}</Text>
              </View>
              {(role === "teacher" || role === "admin") && (
                <View style={{ flexDirection: "row", gap: 6 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...g })}>
                    <Ionicons name="create-outline" size={32} color={C.footer} />
                  </TouchableOpacity>
                  {role === "admin" && (
                    <TouchableOpacity onPress={() => removeGrade(g.id)}>
                      <Ionicons name="trash-outline" size={32} color={C.footer} />
                    </TouchableOpacity>
                  )}
                </View>
              )}
            </Card>
          )}
        />
      }

      {/* ADD MODAL */}
      <FormModal visible={showAdd} title="Aggiungi voto" onClose={() => setShowAdd(false)}>
        <SelectField
          label="Studente"
          value={newGrade.studentId}
          options={students}
          getLabel={(st) => `${st.firstName} ${st.lastName}`}
          onSelect={(v) => setNewGrade(f => ({ ...f, studentId: v }))}
          placeholder="Seleziona studente…"
          emptyMessage="Nessuno studente disponibile"
        />
        <SelectField
          label="Materia"
          value={newGrade.subjectId}
          options={subjects}
          getLabel={(sub) => sub.name}
          onSelect={(v) => setNewGrade(f => ({ ...f, subjectId: v }))}
          placeholder="Seleziona materia…"
          emptyMessage="Nessuna materia disponibile"
        />
        <Input label="Voto (1-10)" value={newGrade.value} onChangeText={v => setNewGrade(f => ({ ...f, value: v }))} keyboardType="decimal-pad" />
        <DateField label="Data" value={newGrade.date} onChange={(v) => setNewGrade(f => ({ ...f, date: v }))} />
        <Btn label="Salva" onPress={addGrade} />
      </FormModal>

      {/* EDIT MODAL */}
      <FormModal visible={!!showEdit} title="Modifica voto" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nuovo voto" value={String(showEdit.value)} onChangeText={v => setShowEdit(f => ({ ...f, value: v }))} keyboardType="decimal-pad" />
          <DateField label="Data" value={showEdit.date} onChange={(v) => setShowEdit(f => ({ ...f, date: v }))} />
          <Btn label="Aggiorna" onPress={saveEditedGrade} />
        </>}
      </FormModal>
    </View>
  );
}
