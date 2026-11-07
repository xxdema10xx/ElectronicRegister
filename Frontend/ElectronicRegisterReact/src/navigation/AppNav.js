// src/navigation/AppNav.js
import { useState } from "react";
import { Image, ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { C, s } from "../constants/theme";
import DashboardScreen from "../screens/DashboardScreen";
import GradesScreen from "../screens/GradesScreen";
import StudentsScreen from "../screens/StudentsScreen";
import MyClassesScreen from "../screens/MyClassesScreen";
import TeachersScreen from "../screens/TeachersScreen";
import SubjectsScreen from "../screens/SubjectsScreen";
import UsersScreen from "../screens/UsersScreen";
import ProfileScreen from "../screens/ProfileScreen";
import ExamsScreen from "../screens/ExamsScreen";
import ClassesScreen from "../screens/ClassesScreen";
import StudentExamsScreen from "../screens/StudentExamsScreen";
import NotificationDot from "../components/NotificationDot";
import Badge from "../components/Badge";
import { useStudentExams } from "../data/studentExams";
import { CONTENT_MAX_WIDTH, useLayout } from "../hooks/useLayout";
import { guardedNavigate, isWeb, usePageHeader, useScreenUrl } from "./examRoutes";

const TITLES = {
  dashboard: "Electronic Register", grades: "Voti", students: "Studenti", myClasses: "Classi", classes: "Classi", exams: "Esami",
  teachers: "Professori", subjects: "Materie", users: "Membri", profile: "Profilo",
};

const icon = name => <Ionicons name={name} size={32} color={C.white} />;
const sideIcon = name => <Ionicons name={name} size={22} color={C.white} />;

export default function AppNav({ user, onLogout }) {
  const [screen, setScreen] = useState("dashboard");
  const insets = useSafeAreaInsets();
  const { isDesktop } = useLayout();
  const role = user?.role?.toLowerCase();
  const { unseenCount } = useStudentExams();

  const tabs = [
    { key: "dashboard", icon: "home-outline", label: "Home", dot: role === "student" && unseenCount > 0 },
    { key: "profile", icon: "person-circle-outline", label: "Profilo" },
  ];

  // Sulla sidebar (schermi larghi) c'è spazio per tutte le sezioni, non solo per quelle della tab bar.
  // staff = segreteria: anagrafiche, voti ed esami, ma non catalogo, materie e membri (docs/AUTH.md §4.2)
  const sideTabs = [
    { key: "dashboard", icon: "home-outline", label: "Home" },
    ...(role === "student" || role === "staff" ? [{ key: "grades", icon: "ribbon-outline", label: "Voti" }] : []),
    ...(role === "admin" ? [{ key: "classes", icon: "people-outline", label: "Classi" }] : []),
    ...(role === "teacher" ? [{ key: "myClasses", icon: "school-outline", label: "Classi" }] : []),
    ...(role === "admin" || role === "staff" ? [{ key: "students", icon: "school-outline", label: "Studenti" }] : []),
    ...(role === "admin" || role === "staff" || role === "student" ? [{ key: "teachers", icon: "pencil-outline", label: "Professori" }] : []),
    ...(role !== "staff" ? [{ key: "subjects", icon: "library-outline", label: "Materie" }] : []),
    { key: "exams", icon: "document-text-outline", label: "Esami", dot: role === "student" && unseenCount > 0 },
    ...(role === "admin" ? [{ key: "users", icon: "people-circle-outline", label: "Membri" }] : []),
    { key: "profile", icon: "person-circle-outline", label: "Profilo" },
  ];

  // Su web l'URL segue la sezione e, all'avvio, la sceglie (es. ricarica su /esami/123/modifica)
  useScreenUrl(screen, setScreen, [...sideTabs, ...tabs].map(t => t.key), role);
  // Cambio sezione ed "Esci": se una pagina ha modifiche non salvate (editor esami) chiede prima conferma
  // Cliccando la sezione in cui si è già (es. "Esami" dall'editor) si torna alla sua pagina iniziale.
  const [visit, setVisit] = useState(0);
  const pageHeader = usePageHeader(); // titolo della pagina interna (es. "Nuovo esame"), se c'è
  const go = (key) => guardedNavigate(() => {
    if (key !== screen) return setScreen(key);
    if (isWeb && key === "exams") window.history.replaceState(null, "", "/esami");
    setVisit(v => v + 1);
  });
  const logout = () => guardedNavigate(onLogout);

  const SCREENS = {
    dashboard: <DashboardScreen navigate={go} />,
    grades: <GradesScreen />,
    students: <StudentsScreen />,
    myClasses: <MyClassesScreen />,
    classes: <ClassesScreen />,
    teachers: <TeachersScreen />,
    subjects: <SubjectsScreen />,
    exams: role === "student" ? <StudentExamsScreen /> : <ExamsScreen />,
    users: <UsersScreen />,
    profile: <ProfileScreen onLogout={logout} />,
  };

  // Nome e cognome dell'utente loggato (profilo docente o studente), altrimenti nome dell'account
  const fullName = (first, last) => [first, last].filter(Boolean).join(" ");
  const userName = fullName(user?.studentFirstName, user?.studentLastName) || fullName(user?.teacherFirstName, user?.teacherLastName)
    || user?.displayName || user?.email?.split("@")[0] || "Utente";
  const content = <View key={`${screen}-${visit}`} style={{ flex: 1 }}>{SCREENS[screen] || <DashboardScreen navigate={go} />}</View>;

  if (isDesktop) {
    return (
      <View style={{ flex: 1, flexDirection: "row", backgroundColor: C.bg }}>
        {/* Sidebar */}
        <View style={{ width: 240, backgroundColor: C.footer, paddingTop: insets.top + 16, paddingBottom: 16 }}>
          <View style={{ alignItems: "center", paddingBottom: 16 }}>
            <Image source={require("../../assets/images/logoitsTrim.png")} style={{ width: 150, height: 70 }} resizeMode="contain" />
          </View>
          <ScrollView>
            {sideTabs.map(t => {
              const active = screen === t.key;
              return (
                <TouchableOpacity key={t.key} onPress={() => go(t.key)}
                  style={{ flexDirection: "row", alignItems: "center", paddingVertical: 12, paddingHorizontal: 20, marginHorizontal: 10, marginBottom: 2, borderRadius: 10,
                    backgroundColor: active ? "rgba(255,255,255,0.18)" : "transparent" }}>
                  <View style={{ position: "relative", width: 28 }}>
                    {sideIcon(t.icon)}
                    <NotificationDot visible={t.dot} style={{ top: -4, right: 0, borderColor: C.footer }} />
                  </View>
                  <Text style={{ color: C.white, fontSize: 15, marginLeft: 12, fontWeight: active ? "800" : "500" }}>{t.label}</Text>
                </TouchableOpacity>
              );
            })}
          </ScrollView>

          {/* Utente e uscita in fondo alla sidebar */}
          <View style={{ borderTopWidth: 1, borderTopColor: "rgba(255,255,255,0.18)", marginHorizontal: 10, paddingTop: 12 }}>
            <TouchableOpacity onPress={logout} style={{ flexDirection: "row", alignItems: "center", paddingVertical: 10, paddingHorizontal: 10 }}>
              <Ionicons name="log-out-outline" size={22} color={C.white} />
              <Text style={{ color: C.white, fontSize: 15, marginLeft: 12, fontWeight: "500" }}>Esci</Text>
            </TouchableOpacity>
          </View>
        </View>

        {/* Contenuto: barra del titolo chiara a tutta larghezza, pagina con larghezza massima */}
        <View style={{ flex: 1 }}>
          <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", paddingHorizontal: 32, height: 64,
            backgroundColor: C.white, borderBottomWidth: 1, borderBottomColor: C.border }}>
            {pageHeader ? (
              // Pagina interna della sezione: un solo titolo, "← Esami / titolo della pagina"
              <View style={{ flexDirection: "row", alignItems: "center", gap: 10, flex: 1, minWidth: 0, marginRight: 16 }}>
                <TouchableOpacity onPress={pageHeader.onBack} accessibilityRole="link" accessibilityLabel={`Torna a ${TITLES[screen]}`}
                  style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
                  <Ionicons name="arrow-back" size={22} color={C.primary} />
                  <Text style={{ fontSize: 22, fontWeight: "800", color: C.primary }}>{TITLES[screen]}</Text>
                </TouchableOpacity>
                <Text style={{ fontSize: 22, color: C.textLight }}>/</Text>
                <Text style={{ fontSize: 22, fontWeight: "800", color: C.text, flexShrink: 1 }} numberOfLines={1}>{pageHeader.title}</Text>
              </View>
            ) : (
              <Text style={{ fontSize: 22, fontWeight: "800", color: C.text }}>{screen === "dashboard" ? "Dashboard" : TITLES[screen]}</Text>
            )}
            <View style={{ flexDirection: "row", alignItems: "center", gap: 12 }}>
              <Text style={{ color: C.textMuted, fontSize: 14, fontWeight: "600" }}>{userName}</Text>
              <Badge text={role} role={role} />
            </View>
          </View>
          <View style={{ flex: 1, width: "100%", maxWidth: CONTENT_MAX_WIDTH, alignSelf: "center" }}>
            {content}
          </View>
        </View>
      </View>
    );
  }

  return (
    <View style={{ flex: 1 }}>
      {/* Top Bar */}
      <View style={[s.topBar, { paddingTop: insets.top, backgroundColor: C.primary }]}>
        {/* Su una pagina interna (dettaglio, editor) ‹ torna alla lista della sezione, non alla Home */}
        {screen !== "dashboard" && (
          <TouchableOpacity onPress={pageHeader ? pageHeader.onBack : () => go("dashboard")} style={{ marginRight: 12 }}
            accessibilityRole="button" accessibilityLabel={pageHeader ? `Torna a ${TITLES[screen]}` : "Torna alla Home"}>
            <Text style={{ color: C.white, fontSize: 18 }}>‹</Text>
          </TouchableOpacity>
        )}
        <Text style={[s.topBarTitle, { flexShrink: 1 }]} numberOfLines={1}> {pageHeader ? pageHeader.title : TITLES[screen]}</Text>
      </View>

      {/* Screen */}
      <View style={{ flex: 1 }}>{content}</View>

      {/* Bottom Tabs */}
      <View style={[s.tabBar, { paddingBottom: insets.bottom, backgroundColor: C.footer, borderTopWidth: 1, borderTopColor: C.border }]}>
        {tabs.map(t => (
          <TouchableOpacity key={t.key} style={s.tab} onPress={() => go(t.key)}>
            <View style={{ position: "relative" }}>
              <Text style={{ fontSize: 20 }}>{icon(t.icon)}</Text>
              <NotificationDot visible={t.dot} style={{ top: -2, right: -2, borderColor: C.footer }} />
            </View>
            <Text style={[s.tabLabel, screen === t.key && s.tabLabelActive, { color: "white" }]}>{t.label}</Text>
          </TouchableOpacity>
        ))}
      </View>
    </View>
  );
}
