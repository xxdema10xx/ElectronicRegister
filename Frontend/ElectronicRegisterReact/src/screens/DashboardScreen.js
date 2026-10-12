// src/screens/DashboardScreen.js
import { useEffect, useState } from "react";
import { Image, ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Badge from "../components/Badge";
import Card from "../components/Card";
import GradeCircle from "../components/GradeCircle";
import { useAuth } from "../hooks/useAuth";
import NotificationDot from "../components/NotificationDot";
import { useStudentExams } from "../data/studentExams";
import { DEMO_EXAMS } from "../data/examsMock";
import { useLayout } from "../hooks/useLayout";
import { formatDateForDisplay, toISODate } from "../utils/dateUtils";
import { getGradesPage } from "../api/gradeService";
import { getSubjects } from "../api/subjectService";
import { getStudents } from "../api/studentService";
import { getTeachers } from "../api/teacherService";
import { getUsers } from "../api/userService";
import { getExams } from "../api/examService";

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
  const { items: assigned, unseenCount } = useStudentExams();
  const { isDesktop, isWide } = useLayout();

  const [grades, setGrades] = useState(null);       // { items, totalCount }
  const [subjects, setSubjects] = useState([]);
  const [exams, setExams] = useState(null);
  const [counts, setCounts] = useState({});

  // Ogni richiesta è indipendente: se una fallisce il riquadro resta vuoto, la home si apre comunque.
  useEffect(() => {
    let alive = true;
    const set = fn => { if (alive) fn(); };
    getSubjects(undefined, token).then(d => set(() => { setSubjects(toList(d)); setCounts(c => ({ ...c, subjects: toList(d).length })); })).catch(() => {});
    if (role !== "admin") getGradesPage({ pageNumber: 1, pageSize: 100 }, token)
      .then(d => set(() => setGrades({ items: toList(d?.items), totalCount: d?.totalCount ?? toList(d?.items).length })))
      .catch(() => set(() => setGrades({ items: [], totalCount: 0 })));
    if (role !== "student") {
      getExams("", token).then(d => set(() => setExams(toList(d).length ? toList(d) : DEMO_EXAMS))).catch(() => set(() => setExams(DEMO_EXAMS)));
      getStudents(undefined, token).then(d => set(() => setCounts(c => ({ ...c, students: toList(d).length })))).catch(() => {});
    }
    if (role === "admin") {
      getTeachers(token).then(d => set(() => setCounts(c => ({ ...c, teachers: toList(d).length })))).catch(() => {});
      getUsers("", token).then(d => set(() => setCounts(c => ({ ...c, users: toList(d).length })))).catch(() => {});
    }
    return () => { alive = false; };
  }, [token, role]);

  const gradeItems = (grades?.items || []).slice().sort((a, b) => String(b.date).localeCompare(String(a.date)));
  const recentGrades = gradeItems.slice(0, isDesktop ? 8 : 5);
  const average = gradeItems.length ? (gradeItems.reduce((sum, g) => sum + Number(g.value), 0) / gradeItems.length).toFixed(1) : "—";
  const subjectName = id => subjects.find(x => x.id === id)?.name || "—";

  const today = toISODate(new Date());
  const upcomingExams = (exams || []).filter(e => String(e.date) >= today).sort((a, b) => String(a.date).localeCompare(String(b.date))).slice(0, isDesktop ? 8 : 5);
  const pendingTests = assigned.filter(i => !i.submission);

  // Card delle sezioni: servono solo quando non c'è la sidebar (la tab bar mobile ha solo Home e Profilo)
  const nav = (icon, label, screen, dot) => ({ icon: <Ionicons name={icon} size={32} color={C.footer} />, label, screen, dot });
  const cards = [
    ...(role === "student" ? [
      nav("ribbon-outline", "I miei voti", "grades"), nav("library-outline", "Materie", "subjects"),
      nav("pencil-outline", "Professori", "teachers"), nav("document-text-outline", "Esami", "exams", unseenCount > 0),
    ] : []),
    ...(role === "teacher" ? [
      nav("ribbon-outline", "Voti", "grades"), nav("school-outline", "Studenti", "students"),
      nav("library-outline", "Materie", "subjects"), nav("document-text-outline", "Esami", "exams"),
    ] : []),
    ...(role === "admin" ? [
      nav("school-outline", "Studenti", "students"), nav("people-outline", "Classi", "classes"),
      nav("pencil-outline", "Professori", "teachers"), nav("library-outline", "Materie", "subjects"),
      nav("people-circle-outline", "Utenti", "users"), nav("document-text-outline", "Esami", "exams"),
    ] : []),
  ];

  const name = user?.studentFirstName || user?.teacherFirstName || user?.email?.split(".")[0] || "Utente";

  const gradesPanel = (
    <Panel title="Ultimi voti" onMore={() => navigate("grades")}>
      {grades === null ? <Empty text="Caricamento…" /> : recentGrades.length === 0 ? <Empty text="Nessun voto presente" /> :
        recentGrades.map(g => (
          <View key={g.id} style={[s.profileRow, { alignItems: "center" }]}>
            <GradeCircle value={g.value} />
            <View style={{ flex: 1, marginLeft: 12 }}>
              <Text style={s.itemTitle}>{subjectName(g.subjectId)}</Text>
              <Text style={s.itemSub}>{formatDateForDisplay(g.date)}</Text>
            </View>
          </View>
        ))}
    </Panel>
  );

  const examsPanel = role === "student" ? (
    <Panel title="Esami da svolgere" onMore={() => navigate("exams")}>
      {pendingTests.length === 0 ? <Empty text="Nessun esame da svolgere" /> : pendingTests.map(i => (
        <TouchableOpacity key={i.testId} onPress={() => navigate("exams")} style={[s.profileRow, { alignItems: "center" }]}>
          <View style={{ flex: 1 }}>
            <Text style={s.itemTitle}>{i.exam?.name || i.test?.name}</Text>
            <Text style={s.itemSub}>{i.test?.name} · {i.durationMinutes} min</Text>
          </View>
          {i.isNew && <View style={{ position: "relative", width: 14, height: 14 }}><NotificationDot visible style={{ top: 0, right: 0 }} /></View>}
        </TouchableOpacity>
      ))}
    </Panel>
  ) : (
    <Panel title="Prossimi esami" onMore={() => navigate("exams")}>
      {exams === null ? <Empty text="Caricamento…" /> : upcomingExams.length === 0 ? <Empty text="Nessun esame in programma" /> : upcomingExams.map(e => (
        <TouchableOpacity key={e.id} onPress={() => navigate("exams")} style={[s.profileRow, { alignItems: "center" }]}>
          <View style={{ flex: 1 }}>
            <Text style={s.itemTitle}>{e.name}</Text>
            <Text style={s.itemSub}>{e.subjectName || subjectName(e.subjectId)}{e.isRetake ? " · recupero" : ""}</Text>
          </View>
          <Text style={{ color: C.textMuted, fontWeight: "600" }}>{formatDateForDisplay(e.date)}</Text>
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
            <StatTile icon="school-outline" label="Studenti" value={counts.students} onPress={() => navigate("students")} />
            <StatTile icon="ribbon-outline" label="Voti inseriti" value={grades ? grades.totalCount : null} onPress={() => navigate("grades")} />
            <StatTile icon="document-text-outline" label="Esami in programma" value={exams ? upcomingExams.length : null} onPress={() => navigate("exams")} />
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
          {role !== "admin" && gradesPanel}
          {examsPanel}
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
