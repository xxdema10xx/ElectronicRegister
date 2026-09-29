// src/navigation/AppNav.js
import { useState } from "react";
import { Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { useSafeAreaInsets } from "react-native-safe-area-context";
import { C, s } from "../constants/theme";
import DashboardScreen from "../screens/DashboardScreen";
import GradesScreen from "../screens/GradesScreen";
import StudentsScreen from "../screens/StudentsScreen";
import TeachersScreen from "../screens/TeachersScreen";
import SubjectsScreen from "../screens/SubjectsScreen";
import UsersScreen from "../screens/UsersScreen";
import ProfileScreen from "../screens/ProfileScreen";
import ExamsScreen from "../screens/ExamsScreen";
import ClassesScreen from "../screens/ClassesScreen";
import StudentExamsScreen from "../screens/StudentExamsScreen";
import NotificationDot from "../components/NotificationDot";
import { useStudentExams } from "../data/studentExams";

const TITLES = {
  dashboard: "Electronic Register", grades: "Voti", students: "Studenti", classes: "Classi", exams: "Esami",
  teachers: "Professori", subjects: "Materie", users: "Utenti", profile: "Profilo",
};

export default function AppNav({ user, onLogout }) {
  const [screen, setScreen] = useState("dashboard");
  const insets = useSafeAreaInsets();
  const role = user?.role?.toLowerCase();
  const { unseenCount } = useStudentExams();

  const tabs = [
    { key: "dashboard", icon: <Ionicons name="home-outline" size={32} color={C.white} />, label: "Home" },
    { key: "grades", icon: <Ionicons name="ribbon-outline" size={32} color={C.white} />, label: "Voti" },
    { key: "subjects", icon: <Ionicons name="library-outline" size={32} color={C.white} />, label: "Materie" },
    // Admin: le classi (con studenti, insegnanti e materie) al posto della lista studenti
    ...(role === "admin" ? [{ key: "classes", icon: <Ionicons name="people-outline" size={32} color={C.white} />, label: "Classi" }]
      : role !== "student" ? [{ key: "students", icon: <Ionicons name="school-outline" size={32} color={C.white} />, label: "Studenti" }] : []),
    // Studente: gli esami da svolgere, con il pallino se ce n'è uno nuovo
    ...(role === "student" ? [{ key: "exams", icon: <Ionicons name="document-text-outline" size={32} color={C.white} />, label: "Esami", dot: unseenCount > 0 }] : []),
    { key: "profile", icon: <Ionicons name="person-circle-outline" size={32} color={C.white} />, label: "Profilo" },
  ];

  const SCREENS = {
    dashboard: <DashboardScreen navigate={setScreen} />,
    grades: <GradesScreen />,
    students: <StudentsScreen />,
    classes: <ClassesScreen />,
    teachers: <TeachersScreen />,
    subjects: <SubjectsScreen />,
    exams: role === "student" ? <StudentExamsScreen /> : <ExamsScreen />,
    users: <UsersScreen />,
    profile: <ProfileScreen onLogout={onLogout} />,
  };

  return (
    <View style={{ flex: 1 }}>
      {/* Top Bar */}
      <View style={[s.topBar, { paddingTop: insets.top, backgroundColor: C.primary }]}>
        {screen !== "dashboard" && (
          <TouchableOpacity onPress={() => setScreen("dashboard")} style={{ marginRight: 12 }}>
            <Text style={{ color: C.white, fontSize: 18 }}>‹</Text>
          </TouchableOpacity>
        )}
        <Text style={s.topBarTitle}> {TITLES[screen]}</Text>
      </View>

      {/* Screen */}
      <View style={{ flex: 1 }}>
        {SCREENS[screen] || <DashboardScreen navigate={setScreen} />}
      </View>

      {/* Bottom Tabs */}
      <View style={[s.tabBar, { paddingBottom: insets.bottom, backgroundColor: C.footer, borderTopWidth: 1, borderTopColor: C.border }]}>
        {tabs.map(t => (
          <TouchableOpacity key={t.key} style={s.tab} onPress={() => setScreen(t.key)}>
            <View style={{ position: "relative" }}>
              <Text style={{ fontSize: 20 }}>{t.icon}</Text>
              <NotificationDot visible={t.dot} style={{ top: -2, right: -2, borderColor: C.footer }} />
            </View>
            <Text style={[s.tabLabel, screen === t.key && s.tabLabelActive, { color: "white" }]}>{t.label}</Text>
          </TouchableOpacity>
        ))}
      </View>
    </View>
  );
}
