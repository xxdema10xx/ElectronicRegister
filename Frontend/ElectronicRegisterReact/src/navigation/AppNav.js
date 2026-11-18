// src/navigation/AppNav.js
import { useEffect, useState } from "react";
import { Image, Pressable, ScrollView, Text, TouchableOpacity, View } from "react-native";
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
import CalendarScreen from "../screens/CalendarScreen";
import NotificationDot from "../components/NotificationDot";
import Badge from "../components/Badge";
import { useStudentExams } from "../data/studentExams";
import { CONTENT_MAX_WIDTH, useLayout } from "../hooks/useLayout";
import { userDisplayName } from "../utils/userName";
import { guardedNavigate, isWeb, usePageHeader, useScreenUrl } from "./examRoutes";

const TITLES = {
  dashboard: "Electronic Register", grades: "Voti", students: "Studenti", myClasses: "Classi", classes: "Classi", exams: "Esami",
  teachers: "Professori", subjects: "Materie", users: "Membri", profile: "Profilo", calendar: "Calendario",
};

// Testo completo nel tooltip del browser, per i titoli che possono essere troncati con "…"
const fullTextTip = (text) => (isWeb ? (node) => node?.setAttribute?.("title", text || "") : undefined);

const icon = name => <Ionicons name={name} size={32} color={C.white} />;
const sideIcon = name => <Ionicons name={name} size={22} color={C.white} />;

export default function AppNav({ user, onLogout }) {
  const [screen, setScreen] = useState("dashboard");
  const [menuOpen, setMenuOpen] = useState(false); // menu a tendina del web su finestra stretta
  useEffect(() => {
    if (!menuOpen || !isWeb) return;
    const onKey = (e) => { if (e.key === "Escape") setMenuOpen(false); };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [menuOpen]);
  const insets = useSafeAreaInsets();
  const { isDesktop, width } = useLayout();
  const headerSize = width < 720 ? 18 : 22; // con la sidebar lo spazio per il titolo è poco
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
    // Voti: libretto per lo studente, registro (stessa schermata) per segreteria e admin
    ...(role === "student" || role === "staff" || role === "admin" ? [{ key: "grades", icon: "ribbon-outline", label: "Voti" }] : []),
    ...(role === "admin" ? [{ key: "classes", icon: "people-outline", label: "Classi" }] : []),
    ...(role === "teacher" ? [{ key: "myClasses", icon: "school-outline", label: "Classi" }] : []),
    ...(role === "admin" || role === "staff" ? [{ key: "students", icon: "school-outline", label: "Studenti" }] : []),
    ...(role === "admin" || role === "staff" || role === "student" ? [{ key: "teachers", icon: "pencil-outline", label: "Professori" }] : []),
    ...(role !== "staff" ? [{ key: "subjects", icon: "library-outline", label: "Materie" }] : []),
    { key: "exams", icon: "document-text-outline", label: "Esami", dot: role === "student" && unseenCount > 0 },
    ...(role === "admin" ? [{ key: "calendar", icon: "calendar-outline", label: "Calendario" }] : []),
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
    if (isWeb && key === "students") window.history.replaceState(null, "", "/studenti");
    // Home e Calendario ripartono dalla settimana corrente e dalla vista iniziale
    if (isWeb && key === "dashboard") window.history.replaceState(null, "", "/");
    if (isWeb && key === "calendar") window.history.replaceState(null, "", "/calendario");
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
    calendar: role === "admin" ? <CalendarScreen role={role} /> : null,
    profile: <ProfileScreen onLogout={logout} />,
  };

  const userName = userDisplayName(user);
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
                <Pressable key={t.key} onPress={() => go(t.key)} accessibilityRole="link" accessibilityState={{ selected: active }}
                  aria-current={active ? "page" : undefined}
                  style={({ hovered }) => ({ flexDirection: "row", alignItems: "center", paddingVertical: 12, paddingHorizontal: 20, marginHorizontal: 10, marginBottom: 2, borderRadius: 10,
                    backgroundColor: active ? "rgba(255,255,255,0.18)" : hovered ? "rgba(255,255,255,0.08)" : "transparent" })}>
                  <View style={{ position: "relative", width: 28 }}>
                    {sideIcon(t.icon)}
                    <NotificationDot visible={t.dot} style={{ top: -4, right: 0, borderColor: C.footer }} />
                  </View>
                  <Text style={{ color: C.white, fontSize: 15, marginLeft: 12, fontWeight: active ? "800" : "500" }}>{t.label}</Text>
                </Pressable>
              );
            })}
          </ScrollView>

          {/* Utente e uscita in fondo alla sidebar */}
          <View style={{ borderTopWidth: 1, borderTopColor: "rgba(255,255,255,0.18)", marginHorizontal: 10, paddingTop: 12 }}>
            <Pressable onPress={logout} accessibilityRole="button"
              style={({ hovered }) => ({ flexDirection: "row", alignItems: "center", paddingVertical: 10, paddingHorizontal: 10, borderRadius: 10,
                backgroundColor: hovered ? "rgba(255,255,255,0.08)" : "transparent" })}>
              <Ionicons name="log-out-outline" size={22} color={C.white} />
              <Text style={{ color: C.white, fontSize: 15, marginLeft: 12, fontWeight: "500" }}>Esci</Text>
            </Pressable>
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
                  <Text style={{ fontSize: headerSize, fontWeight: "800", color: C.primary }}>{TITLES[screen]}</Text>
                </TouchableOpacity>
                <Text style={{ fontSize: headerSize, color: C.textLight }}>/</Text>
                <Text ref={fullTextTip(pageHeader.title)} style={{ fontSize: headerSize, fontWeight: "800", color: C.text, flexShrink: 1, minWidth: 0 }} numberOfLines={1}>{pageHeader.title}</Text>
              </View>
            ) : (
              <Text style={{ fontSize: headerSize, fontWeight: "800", color: C.text }}>{screen === "dashboard" ? "Dashboard" : TITLES[screen]}</Text>
            )}
            {/* Il nome cede spazio al titolo della pagina: si tronca con "…" anch'esso */}
            <View style={{ flexDirection: "row", alignItems: "center", gap: 12, flexShrink: 1, maxWidth: "40%" }}>
              <Text ref={fullTextTip(userName)} style={{ color: C.textMuted, fontSize: 14, fontWeight: "600", flexShrink: 1, minWidth: 0 }} numberOfLines={1}>{userName}</Text>
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

  // Web con finestra stretta: intestazione da sito (logo, titolo, menu ☰ a tendina con tutte le sezioni)
  // invece della barra in alto e della tab bar in basso tipiche di un'app.
  if (isWeb) {
    const title = pageHeader ? pageHeader.title : screen === "dashboard" ? "Dashboard" : TITLES[screen];
    const anyDot = sideTabs.some(t => t.dot);
    return (
      <View style={{ flex: 1, backgroundColor: C.bg }}>
        <View style={{ zIndex: 20 }}>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 12, height: 56, paddingHorizontal: 16, backgroundColor: C.footer }}>
            <Pressable onPress={() => go("dashboard")} accessibilityRole="link" accessibilityLabel="Home">
              <Image source={require("../../assets/images/logoitsTrim.png")} style={{ width: 90, height: 40 }} resizeMode="contain" />
            </Pressable>
            <View style={{ flex: 1 }} />
            <Pressable onPress={() => setMenuOpen(o => !o)} accessibilityRole="button" accessibilityLabel="Menu" accessibilityState={{ expanded: menuOpen }}
              style={({ hovered }) => ({ flexDirection: "row", alignItems: "center", gap: 6, paddingVertical: 6, paddingHorizontal: 10, borderRadius: 8,
                backgroundColor: menuOpen || hovered ? "rgba(255,255,255,0.15)" : "transparent" })}>
              <View style={{ position: "relative" }}>
                <Ionicons name={menuOpen ? "close" : "menu"} size={24} color={C.white} />
                <NotificationDot visible={anyDot && !menuOpen} style={{ top: -2, right: -2, borderColor: C.footer }} />
              </View>
              <Text style={{ color: C.white, fontSize: 15, fontWeight: "600" }}>Menu</Text>
            </Pressable>
          </View>

          {/* Titolo della pagina, con "← sezione" sulle pagine interne */}
          <View style={{ flexDirection: "row", alignItems: "center", gap: 8, height: 48, paddingHorizontal: 16, backgroundColor: C.white, borderBottomWidth: 1, borderBottomColor: C.border }}>
            {pageHeader && (
              <Pressable onPress={pageHeader.onBack} accessibilityRole="link" accessibilityLabel={`Torna a ${TITLES[screen]}`}>
                <Ionicons name="arrow-back" size={20} color={C.primary} />
              </Pressable>
            )}
            <Text ref={fullTextTip(title)} style={{ fontSize: 17, fontWeight: "800", color: C.text, flexShrink: 1 }} numberOfLines={1}>{title}</Text>
          </View>

          {menuOpen && (
            <View accessibilityRole="menu" style={{ position: "absolute", top: 56, left: 0, right: 0, backgroundColor: C.footer, paddingVertical: 8,
              shadowColor: "#000", shadowOpacity: 0.2, shadowRadius: 12, shadowOffset: { width: 0, height: 6 } }}>
              <View style={{ paddingHorizontal: 16, paddingBottom: 8, flexDirection: "row", alignItems: "center", gap: 8 }}>
                <Text style={{ color: "rgba(255,255,255,0.75)", fontSize: 13, flexShrink: 1 }} numberOfLines={1}>{userName}</Text>
                <Badge text={role} role={role} />
              </View>
              {[...sideTabs, { key: "logout", icon: "log-out-outline", label: "Esci" }].map(t => {
                const active = screen === t.key;
                return (
                  <Pressable key={t.key} onPress={() => { setMenuOpen(false); t.key === "logout" ? logout() : go(t.key); }}
                    accessibilityRole={t.key === "logout" ? "button" : "link"} accessibilityState={{ selected: active }}
                    aria-current={active ? "page" : undefined}
                    style={({ hovered }) => ({ flexDirection: "row", alignItems: "center", gap: 12, paddingVertical: 10, paddingHorizontal: 16,
                      backgroundColor: active ? "rgba(255,255,255,0.18)" : hovered ? "rgba(255,255,255,0.08)" : "transparent" })}>
                    <View style={{ position: "relative" }}>
                      {sideIcon(t.icon)}
                      <NotificationDot visible={t.dot} style={{ top: -4, right: -4, borderColor: C.footer }} />
                    </View>
                    <Text style={{ color: C.white, fontSize: 15, fontWeight: active ? "800" : "500" }}>{t.label}</Text>
                  </Pressable>
                );
              })}
            </View>
          )}
        </View>
        <View style={{ flex: 1 }}>{content}</View>
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
        <Text ref={fullTextTip(pageHeader ? pageHeader.title : TITLES[screen])} style={[s.topBarTitle, { flexShrink: 1 }]} numberOfLines={1}> {pageHeader ? pageHeader.title : TITLES[screen]}</Text>
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
