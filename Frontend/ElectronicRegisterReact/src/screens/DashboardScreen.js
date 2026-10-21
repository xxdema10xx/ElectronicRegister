// src/screens/DashboardScreen.js
import { useEffect, useState } from "react";
import { Image, ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Badge from "../components/Badge";
import Card from "../components/Card";
import { useAuth } from "../hooks/useAuth";
import NotificationDot from "../components/NotificationDot";
import { useStudentExams } from "../data/studentExams";
import { useExamApi } from "../hooks/useExamApi";
import { EXAM_STATUS, EXAM_STATUS_LABEL, fromEnum, formatDateTime, parseUtc } from "../api/examContract";
import { useLayout } from "../hooks/useLayout";
import { getGradesPage } from "../api/gradeService";
import { getSubjects } from "../api/subjectService";
import { getStudents } from "../api/studentService";
import { getTeachers } from "../api/teacherService";
import { getUsers } from "../api/userService";
import { getMyClasses } from "../api/classService";
import { getRetakeGroups } from "../api/examInsights";
import { getClassesForPicker, getClassSchedule, getTeacherSchedule, getStudentSchedule, addLesson, updateLesson, deleteLesson } from "../api/lessonService";
import LessonCalendar from "../components/LessonCalendar";
import SelectField from "../components/SelectField";

const toList = d => Array.isArray(d) ? d : d ? [d] : [];

// Numero grande con etichetta
function StatTile({ icon, label, value, onPress }) {
  return (
    <TouchableOpacity activeOpacity={onPress ? 0.7 : 1} onPress={onPress} style={{ flexGrow: 1, flexBasis: 180 }}>
      <Card style={{ flexDirection: "row", alignItems: "center" }}>
        <View style={[s.avatar, { backgroundColor: "#E0EEF8", marginRight: 14 }]}>
          <Ionicons name={icon} size={24} color={C.primary} />
        </View>
        <View>
          <Text style={{ fontSize: 24, fontWeight: "800", color: C.text }}>{value ?? "…"}</Text>
          <Text style={s.itemSub}>{label}</Text>
        </View>
      </Card>
    </TouchableOpacity>
  );
}

// Riquadro con titolo e link "Vedi tutti"
function Panel({ title, onMore, children }) {
  return (
    <View style={{ flexGrow: 1, flexBasis: 320 }}>
      <Card>
        <View style={s.sectionHeader}>
          <Text style={s.sectionTitle}>{title}</Text>
          {onMore && <TouchableOpacity onPress={onMore}><Text style={{ color: C.primary, fontWeight: "700" }}>Vedi tutti ›</Text></TouchableOpacity>}
        </View>
        {children}
      </Card>
    </View>
  );
}

const Empty = ({ text }) => <Text style={[s.itemSub, { paddingVertical: 8 }]}>{text}</Text>;

export default function DashboardScreen({ navigate }) {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const { assigned, unseenCount, isSeen } = useStudentExams();
  const examApi = useExamApi();
  const { isDesktop, isWide } = useLayout();

  const [grades, setGrades] = useState(null);       // { items, totalCount }
  const [subjects, setSubjects] = useState([]);
  const [exams, setExams] = useState(null);
  const [counts, setCounts] = useState({});
  const [myClasses, setMyClasses] = useState(null);
  const [retakeGroups, setRetakeGroups] = useState(null);
  const [classOptions, setClassOptions] = useState([]);
  const [calClassId, setCalClassId] = useState(null);
  const [lessons, setLessons] = useState([]);
  const [fullWeek, setFullWeek] = useState(false);

  // Ogni richiesta è indipendente: se una fallisce il riquadro resta vuoto, la home si apre comunque.
  useEffect(() => {
    let alive = true;
    const set = fn => { if (alive) fn(); };
    getSubjects(undefined, token).then(d => set(() => { setSubjects(toList(d)); setCounts(c => ({ ...c, subjects: toList(d).length })); })).catch(() => {});
    if (role !== "admin") getGradesPage({ pageNumber: 1, pageSize: 100 }, token)
      .then(d => set(() => setGrades({ items: toList(d?.items), totalCount: d?.totalCount ?? toList(d?.items).length })))
      .catch(() => set(() => setGrades({ items: [], totalCount: 0 })));
    if (role !== "student") {
      examApi.listExams().then(d => set(() => setExams(toList(d)))).catch(() => set(() => setExams([])));
      getStudents(undefined, token).then(d => set(() => setCounts(c => ({ ...c, students: toList(d).length })))).catch(() => {});
    }
    if (role === "admin") {
      getTeachers(token).then(d => set(() => setCounts(c => ({ ...c, teachers: toList(d).length })))).catch(() => {});
      getUsers("", token).then(d => set(() => setCounts(c => ({ ...c, users: toList(d).length })))).catch(() => {});
    }
    if (role === "teacher") getMyClasses(user?.teacherId, token).then(d => set(() => setMyClasses(toList(d)))).catch(() => set(() => setMyClasses([])));
    return () => { alive = false; };
  }, [token, role, examApi, user]);

  // "Chi deve recuperare": richiede l'elenco delle consegne di ogni esame, quindi parte solo dopo che gli esami sono arrivati.
  useEffect(() => {
    if (role !== "teacher" && role !== "admin") return;
    if (!exams) return;
    let alive = true;
    getRetakeGroups(examApi, exams).then(g => { if (alive) setRetakeGroups(g); }).catch(() => { if (alive) setRetakeGroups([]); });
    return () => { alive = false; };
  }, [role, exams, examApi]);

  // Orario delle lezioni: admin sceglie la classe da vedere/modificare; docente e studente vedono il proprio.
  useEffect(() => {
    if (role === "admin") {
      const opts = getClassesForPicker();
      setClassOptions(opts);
      setCalClassId(prev => prev || opts[0]?.id || null);
    }
  }, [role]);

  function reloadLessons() {
    if (role === "admin") setLessons(calClassId ? getClassSchedule(calClassId) : []);
    else if (role === "teacher") setLessons(getTeacherSchedule(user?.teacherId));
    else if (role === "student") setLessons(getStudentSchedule(user?.studentId));
  }
  useEffect(reloadLessons, [role, calClassId, user]);

  function addSlot(input) { addLesson(input); reloadLessons(); }
  function editSlot(id, patch) { updateLesson(id, patch); reloadLessons(); }
  function removeSlot(id) { deleteLesson(id); reloadLessons(); }

  const gradeItems = (grades?.items || []).slice().sort((a, b) => String(b.date).localeCompare(String(a.date)));
  const average = gradeItems.length ? (gradeItems.reduce((sum, g) => sum + Number(g.value), 0) / gradeItems.length).toFixed(1) : "—";

  const now = Date.now();
  const upcomingExams = (exams || [])
    .filter(e => ["draft", "published"].includes(fromEnum(e.status, EXAM_STATUS)) && parseUtc(e.availableUntil).getTime() >= now)
    .sort((a, b) => parseUtc(a.availableFrom) - parseUtc(b.availableFrom)).slice(0, isDesktop ? 8 : 5);
  const pendingTests = assigned.filter(a => a.canStart);
  const doneExams = (exams || []).filter(e => fromEnum(e.status, EXAM_STATUS) === "closed");

  // Card delle sezioni: servono solo quando non c'è la sidebar (la tab bar mobile ha solo Home e Profilo)
  const nav = (icon, label, screen, dot) => ({ icon: <Ionicons name={icon} size={32} color={C.footer} />, label, screen, dot });
  const cards = [
    ...(role === "student" ? [
      nav("ribbon-outline", "I miei voti", "grades"), nav("library-outline", "Materie", "subjects"),
      nav("pencil-outline", "Professori", "teachers"), nav("document-text-outline", "Esami", "exams", unseenCount > 0),
    ] : []),
    ...(role === "teacher" ? [
      nav("ribbon-outline", "Voti", "grades"), nav("school-outline", "Classi", "myClasses"),
      nav("library-outline", "Materie", "subjects"), nav("document-text-outline", "Esami", "exams"),
    ] : []),
    ...(role === "admin" ? [
      nav("school-outline", "Studenti", "students"), nav("people-outline", "Classi", "classes"),
      nav("pencil-outline", "Professori", "teachers"), nav("library-outline", "Materie", "subjects"),
      nav("people-circle-outline", "Utenti", "users"), nav("document-text-outline", "Esami", "exams"),
    ] : []),
  ];

  const name = user?.studentFirstName || user?.teacherFirstName || user?.email?.split(".")[0] || "Utente";

  const lessonsPanel = (
    <View style={{ flexGrow: 1, flexBasis: 420 }}>
      <Card>
        <View style={s.sectionHeader}>
          <Text style={s.sectionTitle}>Orario delle lezioni</Text>
          <TouchableOpacity onPress={() => setFullWeek(v => !v)}>
            <Text style={{ color: C.primary, fontWeight: "700" }}>{fullWeek ? "Mostra meno ‹" : "Tutta la settimana ›"}</Text>
          </TouchableOpacity>
        </View>
        {role === "admin" && (
          <SelectField label="Classe" value={calClassId} options={classOptions} onSelect={setCalClassId}
            getLabel={c => c.name} placeholder="Seleziona classe" emptyMessage="Nessuna classe" />
        )}
        {role === "admin" && !calClassId ? <Empty text="Nessuna classe disponibile" /> : (
          <LessonCalendar lessons={lessons} compact={!fullWeek} editable={role === "admin"} classId={calClassId}
            onAdd={addSlot} onEdit={editSlot} onDelete={removeSlot} />
        )}
      </Card>
    </View>
  );

  const examsPanel = role === "student" ? (
    <Panel title="Esami da svolgere" onMore={() => navigate("exams")}>
      {pendingTests.length === 0 ? <Empty text="Nessun esame da svolgere" /> : pendingTests.map(a => (
        <TouchableOpacity key={a.id} onPress={() => navigate("exams")} style={[s.profileRow, { alignItems: "center" }]}>
          <View style={{ flex: 1 }}>
            <Text style={s.itemTitle}>{a.name}</Text>
            <Text style={s.itemSub}>{a.durationMinutes} min · fino al {formatDateTime(a.availableUntil)}</Text>
          </View>
          {!isSeen(a.id) && <View style={{ position: "relative", width: 14, height: 14 }}><NotificationDot visible style={{ top: 0, right: 0 }} /></View>}
        </TouchableOpacity>
      ))}
    </Panel>
  ) : (
    <Panel title="Prossimi esami" onMore={() => navigate("exams")}>
      {exams === null ? <Empty text="Caricamento…" /> : upcomingExams.length === 0 ? <Empty text="Nessun esame in programma" /> : upcomingExams.map(e => (
        <TouchableOpacity key={e.id} onPress={() => navigate("exams")} style={[s.profileRow, { alignItems: "center" }]}>
          <View style={{ flex: 1 }}>
            <Text style={s.itemTitle}>{e.name}</Text>
            <Text style={s.itemSub}>{EXAM_STATUS_LABEL[fromEnum(e.status, EXAM_STATUS)]}{e.isRetake ? " · recupero" : ""}</Text>
          </View>
          <Text style={{ color: C.textMuted, fontWeight: "600" }}>{formatDateTime(e.availableFrom)}</Text>
        </TouchableOpacity>
      ))}
    </Panel>
  );

  const myClassesPanel = role === "teacher" && (
    <Panel title="Le mie classi" onMore={() => navigate("myClasses")}>
      {myClasses === null ? <Empty text="Caricamento…" /> : myClasses.length === 0 ? <Empty text="Nessuna classe assegnata" /> : myClasses.map(c => (
        <TouchableOpacity key={c.id} onPress={() => navigate("myClasses")} style={[s.profileRow, { alignItems: "center" }]}>
          <View style={{ flex: 1 }}>
            <Text style={s.itemTitle}>{c.name}</Text>
            <Text style={s.itemSub} numberOfLines={1}>{c.subjects.map(sb => sb.subjectName).join(", ") || "—"}</Text>
          </View>
          <Text style={{ color: C.textMuted, fontWeight: "600" }}>{c.students.length} studenti</Text>
        </TouchableOpacity>
      ))}
    </Panel>
  );

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      {!isDesktop && <View style={[s.dashHeader, { position: "relative" }]}>
        <View style={{ flex: 1, marginRight: 8 }}>
          <Text style={s.dashWelcome}>Benvenuto,</Text>
          <Text style={s.dashName}>{name}</Text>
        </View>
        <Badge text={role} role={role} />
        <View pointerEvents="none" style={{ position: "absolute", top: -40, left: 0, right: 0, alignItems: "center" }}>
          <Image source={require("../../assets/images/logoitsTrim.png")} style={{ width: 150, height: 100 }} resizeMode="contain" />
        </View>
      </View>}
      <ScrollView contentContainerStyle={{ padding: isDesktop ? 32 : 16, gap: isDesktop ? 24 : 16 }}>
        {isDesktop && (
          <View>
            <Text style={{ fontSize: 26, fontWeight: "800", color: C.text }}>Benvenuto, {name}</Text>
            <Text style={[s.itemSub, { fontSize: 15 }]}>Ecco una panoramica di oggi.</Text>
          </View>
        )}
        {/* Numeri chiave */}
        <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 12 }}>
          {role === "student" && <>
            <StatTile icon="stats-chart-outline" label="Media voti" value={grades ? average : null} onPress={() => navigate("grades")} />
            <StatTile icon="ribbon-outline" label="Voti ricevuti" value={grades ? grades.totalCount : null} onPress={() => navigate("grades")} />
            <StatTile icon="document-text-outline" label="Esami da svolgere" value={pendingTests.length} onPress={() => navigate("exams")} />
            <StatTile icon="library-outline" label="Materie" value={counts.subjects} onPress={() => navigate("subjects")} />
          </>}
          {role === "teacher" && <>
            <StatTile icon="document-text-outline" label="Esami in programma" value={exams ? upcomingExams.length : null} onPress={() => navigate("exams")} />
            <StatTile icon="checkmark-done-outline" label="Esami fatti" value={exams ? doneExams.length : null} onPress={() => navigate("exams")} />
            <StatTile icon="alert-circle-outline" label="Da recuperare" value={retakeGroups ? retakeGroups.length : null} onPress={() => navigate("exams")} />
            <StatTile icon="library-outline" label="Materie" value={counts.subjects} onPress={() => navigate("subjects")} />
          </>}
          {role === "admin" && <>
            <StatTile icon="school-outline" label="Studenti" value={counts.students} onPress={() => navigate("students")} />
            <StatTile icon="pencil-outline" label="Professori" value={counts.teachers} onPress={() => navigate("teachers")} />
            <StatTile icon="people-circle-outline" label="Utenti" value={counts.users} onPress={() => navigate("users")} />
            <StatTile icon="library-outline" label="Materie" value={counts.subjects} onPress={() => navigate("subjects")} />
          </>}
        </View>

        {/* Ultime attività */}
        <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 16 }}>
          {examsPanel}
          {myClassesPanel}
          {lessonsPanel}
        </View>

        {/* Sezioni (solo senza sidebar) */}
        {!isDesktop && (
          <View style={s.dashGrid}>
            {cards.map(c => (
              <TouchableOpacity key={c.screen} style={[s.dashCard, { position: "relative" }, isWide && { width: "23.5%" }]} onPress={() => navigate(c.screen)}>
                <NotificationDot visible={c.dot} style={{ top: 8, right: 8 }} />
                <Text style={s.dashCardIcon}>{c.icon}</Text>
                <Text style={s.dashCardLabel}>{c.label}</Text>
              </TouchableOpacity>
            ))}
          </View>
        )}
      </ScrollView>
    </View>
  );
}
