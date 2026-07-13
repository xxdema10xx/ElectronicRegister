import { createContext, useCallback, useContext, useEffect, useState, useRef } from "react";
import {
  ActivityIndicator,
  Alert,
  FlatList,
  KeyboardAvoidingView,
  Modal,
  Platform,
  SafeAreaView,
  ScrollView,
  StatusBar,
  StyleSheet,
  Image,
  Text, TextInput, TouchableOpacity,
  View,
} from "react-native";

import { SafeAreaProvider } from 'react-native-safe-area-context';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import * as AuthSession from 'expo-auth-session';
import * as WebBrowser from 'expo-web-browser';

WebBrowser.maybeCompleteAuthSession();
// ─── CONFIG ──────────────────────────────────────────────────────────────────
const API_BASE = `http://YOUR_SERVER_IP:5000/api`; // ← cambia con il tuo URL

// ─── MICROSOFT AUTH CONFIG ────────────────────────────────────────────────────
const MS_CLIENT_ID = "df405eeb-4453-4f41-86d7-2a4af11446b6";
const MS_TENANT = "common";

const msDiscovery = {
  authorizationEndpoint: `https://login.microsoftonline.com/${MS_TENANT}/oauth2/v2.0/authorize`,
  tokenEndpoint: `https://login.microsoftonline.com/${MS_TENANT}/oauth2/v2.0/token`,
};

// ─── COLORS ──────────────────────────────────────────────────────────────────
const C = {
  primary:    "#154f78",
  primaryDk:  "#4338CA",
  secondary:  "#7C3AED",
  success:    "#10B981",
  danger:     "#EF4444",
  warning:    "#F59E0B",
  bg:         "#F8FAFC",
  card:       "#FFFFFF",
  footer: "#154f78",
  border:     "#E2E8F0",
  text:       "#1E293B",
  textMuted:  "#64748B",
  textLight:  "#94A3B8",
  white:      "#FFFFFF",
  studentBadge: "#DBEAFE",
  teacherBadge: "#D1FAE5",
  adminBadge:   "#FEE2E2",
};

const ROLE_COLOR = { student: C.studentBadge, teacher: C.teacherBadge, admin: C.adminBadge };
const ROLE_TEXT  = { student: "#1D4ED8",       teacher: "#065F46",       admin: "#991B1B" };

// ─── API HELPER ───────────────────────────────────────────────────────────────
async function api(method, path, body, token) {
  const opts = {
    method,
    headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}),
  };
  const res = await fetch(`${API_BASE}${path}`, opts);
  if (res.status === 204) {
    console.log("STATUS:", res.status, "- Nessun contenuto (successo)"); // 👈 aggiunto
    return null;
  }
  const text = await res.text();

  console.log("STATUS:", res.status);               // 👈 verifica sia presente
  console.log("RAW TEXT:", JSON.stringify(text));    // 👈 verifica sia presente

  let data;
  try { data = JSON.parse(text); } catch { data = text; }
  if (!res.ok) throw new Error(typeof data === "string" ? data : data?.message || `Errore ${res.status}`);
  return data;
}

// ─── AUTH CONTEXT ─────────────────────────────────────────────────────────────
const AuthContext = createContext(null);
function useAuth() { return useContext(AuthContext); }

function useMicrosoftLogin() {
  const redirectUri = AuthSession.makeRedirectUri({ scheme: 'electronicregister' });

  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: MS_CLIENT_ID,
      scopes: [`api://${MS_CLIENT_ID}/access_as_user`, "openid", "profile", "offline_access"],
      redirectUri,
      responseType: AuthSession.ResponseType.Code,
      usePKCE: true,
    },
    msDiscovery
  );

  return { request, response, promptAsync, redirectUri };
}

// ─── DECODE JWT (no library needed) ──────────────────────────────────────────
function decodeJwt(token) {
  try {
    const payload = token.split(".")[1];
    const decoded = JSON.parse(atob(payload.replace(/-/g, "+").replace(/_/g, "/")));
    return decoded;
  } catch { return null; }
}

// ─── SMALL COMPONENTS ────────────────────────────────────────────────────────
function Btn({ label, onPress, style, textStyle, icon, variant = "primary", loading, disabled }) {
  const bg = {
    primary: C.primary, danger: C.danger, ghost: "transparent",
    success: C.success, secondary: C.secondary,
  }[variant];
  const tc = variant === "ghost" ? C.primary : C.white;
  return (
    <TouchableOpacity
      style={[s.btn, { backgroundColor: bg, borderWidth: variant === "ghost" ? 1 : 0, borderColor: C.primary, opacity: disabled || loading ? 0.6 : 1 }, style]}
      onPress={onPress} disabled={disabled || loading}
    >
      {loading ? <ActivityIndicator color={C.white} size="small" /> :
        <Text style={[s.btnText, { color: tc }, textStyle]}>{icon ? `${icon}  ` : ""}{label}</Text>}
    </TouchableOpacity>
  );
}

function Input({ label, ...props }) {
  return (
    <View style={{ marginBottom: 14 }}>
      {label && <Text style={s.label}>{label}</Text>}
      <TextInput style={s.input} placeholderTextColor={C.textLight} {...props} />
    </View>
  );
}

// ─── SELECT FIELD (modale con lista scorrevole al posto dell'input manuale) ──
function SelectField({
  label,
  value,
  options = [],
  onSelect,
  placeholder = "Seleziona…",
  getLabel = (o) => o.label,
  getValue = (o) => o.id,
  emptyMessage = "Nessun elemento disponibile",
}) {
  const [open, setOpen] = useState(false);
  const selected = options.find(o => getValue(o) === value);

  return (
    <View style={{ marginBottom: 14 }}>
      {label && <Text style={s.label}>{label}</Text>}
      <TouchableOpacity style={s.selectBox} onPress={() => setOpen(true)} activeOpacity={0.7}>
        <Text style={{ fontSize: 15, color: selected ? C.text : C.textLight, flex: 1 }} numberOfLines={1}>
          {selected ? getLabel(selected) : placeholder}
        </Text>
        <Text style={{ color: C.textLight, fontSize: 12 }}>▾</Text>
      </TouchableOpacity>

      <Modal visible={open} animationType="slide" transparent onRequestClose={() => setOpen(false)}>
        <View style={s.modalOverlay}>
          <View style={[s.modalCard, { maxHeight: "75%" }]}>
            <View style={s.modalHeader}>
              <Text style={s.modalTitle}>{label || "Seleziona"}</Text>
              <TouchableOpacity onPress={() => setOpen(false)}><Text style={s.modalClose}>✕</Text></TouchableOpacity>
            </View>
            {options.length === 0 ? (
              <EmptyState message={emptyMessage} />
            ) : (
              <FlatList
                data={options}
                keyExtractor={(o) => String(getValue(o))}
                renderItem={({ item }) => {
                  const isSelected = getValue(item) === value;
                  return (
                    <TouchableOpacity
                      style={[s.selectOption, isSelected && s.selectOptionActive]}
                      onPress={() => { onSelect(getValue(item)); setOpen(false); }}
                    >
                      <Text style={[s.selectOptionText, isSelected && s.selectOptionTextActive]}>
                        {getLabel(item)}
                      </Text>
                      {isSelected && <Text style={{ color: C.primary, fontWeight: "700" }}>✓</Text>}
                    </TouchableOpacity>
                  );
                }}
              />
            )}
          </View>
        </View>
      </Modal>
    </View>
  );
}

function Card({ children, style }) {
  return <View style={[s.card, style]}>{children}</View>;
}

function Badge({ text, role }) {
  return (
    <View style={[s.badge, { backgroundColor: ROLE_COLOR[role] || C.border }]}>
      <Text style={[s.badgeText, { color: ROLE_TEXT[role] || C.text }]}>{text}</Text>
    </View>
  );
}

function GradeCircle({ value }) {
  const color = value >= 6 ? C.success : value >= 5 ? C.warning : C.danger;
  return (
    <View style={[s.gradeCircle, { backgroundColor: color }]}>
      <Text style={s.gradeValue}>{value}</Text>
    </View>
  );
}

function SectionHeader({ title, action }) {
  return (
    <View style={s.sectionHeader}>
      <Text style={s.sectionTitle}>{title}</Text>
      {action}
    </View>
  );
}

function EmptyState({ message }) {
  return (
    <View style={s.emptyState}>
      <Text style={s.emptyIcon}>📭</Text>
      <Text style={s.emptyText}>{message}</Text>
    </View>
  );
}

function Loader() {
  return (
    <View style={s.loaderWrap}>
      <ActivityIndicator size="large" color={C.primary} />
    </View>
  );
}

// ─── MODAL FORM ───────────────────────────────────────────────────────────────
function FormModal({ visible, title, onClose, children }) {
  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <KeyboardAvoidingView behavior={Platform.OS === "ios" ? "padding" : undefined} style={{ flex: 1 }}>
        <View style={s.modalOverlay}>
          <View style={s.modalCard}>
            <View style={s.modalHeader}>
              <Text style={s.modalTitle}>{title}</Text>
              <TouchableOpacity onPress={onClose}><Text style={s.modalClose}>✕</Text></TouchableOpacity>
            </View>
            <ScrollView keyboardShouldPersistTaps="handled">{children}</ScrollView>
          </View>
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );
}

function LoginScreen({ onLogin, goRegister }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [msLoading, setMsLoading] = useState(false);

  const redirectUri = AuthSession.makeRedirectUri({ scheme: 'electronicregister' });
  console.log("REDIRECT URI (registra questo su Entra ID):", redirectUri);

  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: MS_CLIENT_ID,
      scopes: [
        `api://${MS_CLIENT_ID}/access_as_user`,
        "openid",
        "profile",
        "offline_access"
      ],
      redirectUri,
      responseType: AuthSession.ResponseType.Code,
      usePKCE: true,
    },
    msDiscovery
  );

  useEffect(() => {
    console.log("RESPONSE COMPLETO:", JSON.stringify(response));

    if (response?.type === "success") {
      const { code } = response.params;

      console.log("ENTRATO SUCCESS");
      console.log("CODE PRESENTE:", !!code);
      console.log("CODE VERIFIER:", request?.codeVerifier);
      console.log("REDIRECT USATO:", redirectUri);

      setMsLoading(true);

      (async () => {
        try {
          console.log("PRIMA EXCHANGE");

          const tokenResponse = await AuthSession.exchangeCodeAsync(
            {
              clientId: MS_CLIENT_ID,
              code,
              redirectUri,
              extraParams: {
                code_verifier: request.codeVerifier,
              },
            },
            msDiscovery
          );

          console.log("DOPO EXCHANGE ACCESS TOKEN:", !!tokenResponse.accessToken);
          console.log("TOKEN MICROSOFT LENGTH:", tokenResponse.accessToken?.length);
          console.log(
            "TOKEN MICROSOFT START:",
            tokenResponse.accessToken?.substring(0, 50)
          );

          await submitMicrosoftToken(tokenResponse.accessToken);

        } catch (err) {
          console.log("ERRORE EXCHANGE:", err);

          Alert.alert(
            "Errore login Microsoft",
            err?.message || JSON.stringify(err)
          );

          setMsLoading(false);
        }
      })();

    } else if (response?.type === "error") {

      console.log("RISPOSTA ERRORE:", response.error);

      Alert.alert(
        "Errore login Microsoft",
        response.error?.message || "Login annullato"
      );

      setMsLoading(false);
    }

  }, [response]);


  async function submitMicrosoftToken(msAccessToken) {
    try {
      console.log("INVIO TOKEN ALLA API");

      const data = await api(
        "POST",
        "/Auth/microsoft-login",
        {
          accessToken: msAccessToken
        }
      );

      console.log("LOGIN API OK:", data);

      onLogin(data.token);

    } catch (e) {

      console.log("ERRORE LOGIN MICROSOFT API:", e.message);
      console.log("ERRORE COMPLETO:", e);

      Alert.alert(
        "Errore login Microsoft",
        String(e.message)
      );

    } finally {
      setMsLoading(false);
    }
  }

  // ─── Login normale (invariato) ───
  async function submit() {
    if (!email || !password) { Alert.alert("Attenzione", "Compila tutti i campi"); return; }
    setLoading(true);
    try {
      const data = await api("POST", "/Auth/login", { email, password });
      onLogin(data.token);
    } catch (e) { Alert.alert("Errore login", e.message); }
    finally { setLoading(false); }
  }

  return (
    <SafeAreaView style={s.authBg}>
      <StatusBar barStyle="light-content" backgroundColor={C.primary} />
      <View style={s.authHeader}>
        <Image
          source={require('./assets/images/logoits.png')}
          style={{ width: 150, height: 100 }}
        />
        <Text style={s.authTitle}>Electronic Register</Text>
        <Text style={s.authSubtitle}>ITS Umbria</Text>
      </View>
      <View style={s.authBody}>
        <Card>
          <Text style={s.cardTitle}>Accedi</Text>
          <Input label="Email" value={email} onChangeText={setEmail} keyboardType="email-address"
            autoCapitalize="none" placeholder="allievo_nome@itsumbria.it" />
          <Input label="Password" value={password} onChangeText={setPassword} secureTextEntry placeholder="••••••••" />
          <Btn label="Entra" onPress={submit} loading={loading} style={{ marginTop: 4 }} />

          <Btn
            label="Accedi con Microsoft"
            onPress={() => promptAsync()}
            loading={msLoading}
            disabled={!request}
            style={{ marginTop: 10, backgroundColor: "#2F2F2F" }}
          />
        </Card>
      </View>
    </SafeAreaView>
  );
}

// ─── REGISTER SCREEN ──────────────────────────────────────────────────────────
function RegisterScreen({ onBack }) {
  const [form, setForm] = useState({ email: "", password: "", firstName: "", lastName: "" });
  const [loading, setLoading] = useState(false);

  function set(k, v) { setForm(f => ({ ...f, [k]: v })); }

  async function submit() {
    setLoading(true);
    try {
      await api("POST", "/Auth/register", form);
      Alert.alert("Successo", "Registrazione completata!", [{ text: "OK", onPress: onBack }]);
    } catch (e) { Alert.alert("Errore", e.message); }
    finally { setLoading(false); }
  }

  return (
    <SafeAreaView style={s.authBg}>
      <StatusBar barStyle="light-content" backgroundColor={C.primary} />
      <View style={s.authHeader}>
        <Image
          source={require('./assets/images/logoits.png')}
          style={{ width: 150, height: 100 }}
        />
        <Text style={s.authTitle}>Electronic Register</Text>
      </View>
      <ScrollView contentContainerStyle={s.authBody}>
        <Card>
          <Text style={s.cardTitle}>Crea account</Text>
          <Input label="Nome" value={form.firstName} onChangeText={v => set("firstName", v)} placeholder="Mario" />
          <Input label="Cognome" value={form.lastName} onChangeText={v => set("lastName", v)} placeholder="Rossi" />
          <Input label="Email" value={form.email} onChangeText={v => set("email", v)}
            keyboardType="email-address" autoCapitalize="none" placeholder="allievo_mario@itsumbria.it" />
          <Input label="Password (min 8 car. + simbolo)" value={form.password} onChangeText={v => set("password", v)}
            secureTextEntry placeholder="••••••••" />
          <Btn label="Registrati" onPress={submit} loading={loading} />
          <TouchableOpacity onPress={onBack} style={{ marginTop: 14, alignItems: "center" }}>
            <Text style={{ color: C.primary, fontWeight: "600" }}>Hai già un account? Accedi</Text>
          </TouchableOpacity>
        </Card>
      </ScrollView>
    </SafeAreaView>
  );
}

// ─── DASHBOARD (role-based home) ──────────────────────────────────────────────
function DashboardScreen({ navigate }) {
  const { user } = useAuth();
  const role = user?.role?.toLowerCase();

  const cards = [
    ...(role === "student" ? [
      { icon: "📊", label: "I miei voti",    screen: "grades" },
      { icon: "📚", label: "Materie",         screen: "subjects" },
      { icon: "👩‍🏫", label: "Professori",     screen: "teachers" },
    ] : []),
    ...(role === "teacher" ? [
      { icon: "📊", label: "Voti",            screen: "grades" },
      { icon: "👨‍🎓", label: "Studenti",       screen: "students" },
      { icon: "📚", label: "Materie",         screen: "subjects" },
    ] : []),
    ...(role === "admin" ? [
      { icon: "📊", label: "Voti",            screen: "grades" },
      { icon: "👨‍🎓", label: "Studenti",       screen: "students" },
      { icon: "👩‍🏫", label: "Professori",     screen: "teachers" },
      { icon: "📚", label: "Materie",         screen: "subjects" },
      { icon: "👤", label: "Utenti",          screen: "users" },
    ] : []),
    { icon: "⚙️", label: "Profilo",           screen: "profile" },
  ];

  const name = user?.studentFirstName || user?.teacherFirstName || user?.email?.split("@")[0] || "Utente";

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={[s.dashHeader, { position: "relative" }]}>
        <View style={{ flex: 1, marginRight: 8 }}>
          <Text style={s.dashWelcome}>Benvenuto,</Text>
          <Text style={s.dashName}>{name} {user?.studentLastName || user?.teacherLastName || ""}</Text>
        </View>
        <Badge text={role} role={role} />
        <View
          pointerEvents="none"
          style={{ position: "absolute", top: 0, bottom: 0, left: 0, right: 0, alignItems: "center", justifyContent: "center" }}
        >
          <Image
            source={require('./assets/images/logoits.png')}
            style={{ width: 150, height: 100 }}
            resizeMode="contain"
          />
        </View>
      </View>
      <ScrollView contentContainerStyle={{ padding: 16 }}>
        <View style={s.dashGrid}>
          {cards.map(c => (
            <TouchableOpacity key={c.screen} style={s.dashCard} onPress={() => navigate(c.screen)}>
              <Text style={s.dashCardIcon}>{c.icon}</Text>
              <Text style={s.dashCardLabel}>{c.label}</Text>
            </TouchableOpacity>
          ))}
        </View>
      </ScrollView>
    </View>
  );
}

// ─── GRADES SCREEN ────────────────────────────────────────────────────────────
const GRADES_PAGE_SIZE = 20;

function GradesScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [grades, setGrades] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [students, setStudents] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [filterSubject, setFilterSubject] = useState("");
  const [filterDate, setFilterDate] = useState("");
  const [newGrade, setNewGrade] = useState({ studentId: "", subjectId: "", teacherId: "", value: "", date: "" });
  // Smart loading: teniamo in memoria tutti i voti ricevuti dall'API, ma ne
  // mostriamo solo GRADES_PAGE_SIZE alla volta, caricandone altri man mano
  // che l'utente scorre verso il basso.
  const [visibleCount, setVisibleCount] = useState(GRADES_PAGE_SIZE);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      let path = "/Grade";
      if (filterSubject) path = `/Grade/bysubject/${filterSubject}`;
      else if (filterDate) path = `/Grade/bydate/${filterDate}`;
      const g = await api("GET", path, null, token);
      setGrades(Array.isArray(g) ? g : []);
      setVisibleCount(GRADES_PAGE_SIZE);
      const sub = await api("GET", "/Subject", null, token);
      setSubjects(Array.isArray(sub) ? sub : []);
      if (role !== "student") {
        const st = await api("GET", "/Student", null, token);
        setStudents(Array.isArray(st) ? st : []);
        const te = await api("GET", "/Teacher", null, token);
        setTeachers(Array.isArray(te) ? te : []);
      }
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setGrades([]); }
    finally { setLoading(false); }
  }, [token, filterSubject, filterDate, role]);

  useEffect(() => { load(); }, [load]);

  const visibleGrades = grades.slice(0, visibleCount);
  const hasMore = visibleCount < grades.length;

  function loadMoreGrades() {
    if (!hasMore) return;
    setVisibleCount(c => Math.min(c + GRADES_PAGE_SIZE, grades.length));
  }

  function subjectName(id) { return subjects.find(s => s.id === id)?.name || id?.slice(0, 8) || "—"; }
  function studentName(id) { const st = students.find(s => s.id === id); return st ? `${st.firstName} ${st.lastName}` : id?.slice(0, 8) || "—"; }

  async function addGrade() {
    try {
      await api("POST", "/Grade", {
        studentId: newGrade.studentId,
        subjectId: newGrade.subjectId,
        value: parseFloat(newGrade.value),
        date: newGrade.date,
      }, token);
      setShowAdd(false);
      setNewGrade({ studentId: "", subjectId: "", teacherId: "", value: "", date: "" });
      load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function updateGrade() {
    try {
      await api("PUT", `/Grade/update/${showEdit.id}`, { value: parseFloat(showEdit.value), date: showEdit.date }, token);
      setShowEdit(null);
      load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function deleteGrade(id) {
    Alert.alert("Conferma", "Eliminare questo voto?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await api("DELETE", `/Grade/${id}`, null, token); load(); }
        catch (e) { Alert.alert("Errore", e.message); }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', marginTop: 50, marginBottom: 50 }}>
          <Image
            source={require('./assets/images/logoits.png')}
            style={{ width: 100, height: 100 }}
          />
        </View>
        <SectionHeader title="Voti" action={
          (role === "teacher" || role === "admin") &&
          <Btn label="+ Voto" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
        {/* Filters */}
        <View style={{ flexDirection: "row", gap: 8, marginBottom: 12 }}>
          <TextInput style={[s.input, { flex: 1 }]} placeholder="Filtra materia" placeholderTextColor={C.textLight}
            value={filterSubject} onChangeText={v => { setFilterSubject(v); setFilterDate(""); }} />
          <TextInput style={[s.input, { flex: 1 }]} placeholder="Data (YYYY-MM-DD)" placeholderTextColor={C.textLight}
            value={filterDate} onChangeText={v => { setFilterDate(v); setFilterSubject(""); }} />
        </View>
      </View>
      {loading ? <Loader /> : grades.length === 0 ? <EmptyState message="Nessun voto trovato" /> :
        <FlatList
          data={visibleGrades}
          keyExtractor={g => g.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          onEndReached={loadMoreGrades}
          onEndReachedThreshold={0.4}
          ListFooterComponent={hasMore ? (
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
                    <Text style={s.actionIcon}>✏️</Text>
                  </TouchableOpacity>
                  {role === "admin" && (
                    <TouchableOpacity onPress={() => deleteGrade(g.id)}>
                      <Text style={s.actionIcon}>🗑️</Text>
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
        <Input label="Data (YYYY-MM-DD)" value={newGrade.date} onChangeText={v => setNewGrade(f => ({ ...f, date: v }))} />
        <Btn label="Salva" onPress={addGrade} />
      </FormModal>

      {/* EDIT MODAL */}
      <FormModal visible={!!showEdit} title="Modifica voto" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nuovo voto" value={String(showEdit.value)} onChangeText={v => setShowEdit(f => ({ ...f, value: v }))} keyboardType="decimal-pad" />
          <Input label="Data (YYYY-MM-DD)" value={showEdit.date} onChangeText={v => setShowEdit(f => ({ ...f, date: v }))} />
          <Btn label="Aggiorna" onPress={updateGrade} />
        </>}
      </FormModal>
    </View>
  );
}

// ─── STUDENTS SCREEN ──────────────────────────────────────────────────────────
function StudentsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [students, setStudents] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ firstName: "", lastName: "" });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const path = search ? `/Student/bylastname/${search}` : "/Student";
      const data = await api("GET", path, null, token);
      setStudents(Array.isArray(data) ? data : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setStudents([]); }
    finally { setLoading(false); }
  }, [token, search]);

  useEffect(() => { load(); }, [load]);

  async function add() {
    try {
      await api("POST", "/Student", form, token);
      setShowAdd(false); setForm({ firstName: "", lastName: "" }); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function update() {
    try {
      await api("PUT", `/Student/update/${showEdit.id}`, { firstName: showEdit.firstName, lastName: showEdit.lastName }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function del(id) {
    Alert.alert("Conferma", "Eliminare studente?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await api("DELETE", `/Student/${id}`, null, token); load(); }
        catch (e) { Alert.alert("Errore", e.message); }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', marginTop: 50, marginBottom: 50 }}>
          <Image
            source={require('./assets/images/logoits.png')}
            style={{ width: 100, height: 100 }}
          />
        </View>
        <SectionHeader title="Studenti" action={
          role === "admin" && <Btn label="+ Studente" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per cognome…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : students.length === 0 ? <EmptyState message="Nessuno studente trovato" /> :
        <FlatList
          data={students}
          keyExtractor={s => s.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: st }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={s.avatar}><Text style={s.avatarText}>{st.firstName[0]}{st.lastName[0]}</Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{st.firstName} {st.lastName}</Text>
              </View>
              {role === "admin" && (
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...st })}><Text style={s.actionIcon}>✏️</Text></TouchableOpacity>
                  <TouchableOpacity onPress={() => del(st.id)}><Text style={s.actionIcon}>🗑️</Text></TouchableOpacity>
                </View>
              )}
            </Card>
          )}
        />
      }
      <FormModal visible={showAdd} title="Aggiungi studente" onClose={() => setShowAdd(false)}>
        <Input label="Nome" value={form.firstName} onChangeText={v => setForm(f => ({ ...f, firstName: v }))} />
        <Input label="Cognome" value={form.lastName} onChangeText={v => setForm(f => ({ ...f, lastName: v }))} />
        <Btn label="Salva" onPress={add} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica studente" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.firstName} onChangeText={v => setShowEdit(f => ({ ...f, firstName: v }))} />
          <Input label="Cognome" value={showEdit.lastName} onChangeText={v => setShowEdit(f => ({ ...f, lastName: v }))} />
          <Btn label="Aggiorna" onPress={update} />
        </>}
      </FormModal>
    </View>
  );
}

// ─── TEACHERS SCREEN ──────────────────────────────────────────────────────────
function TeachersScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [teachers, setTeachers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ firstName: "", lastName: "" });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await api("GET", "/Teacher", null, token);
      setTeachers(Array.isArray(data) ? data : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setTeachers([]); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  async function add() {
    try {
      await api("POST", "/Teacher", form, token);
      setShowAdd(false); setForm({ firstName: "", lastName: "" }); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function update() {
    try {
      await api("PUT", `/Teacher/update/${showEdit.id}`, { firstName: showEdit.firstName, lastName: showEdit.lastName }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function del(id) {
    Alert.alert("Conferma", "Eliminare insegnante?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await api("DELETE", `/Teacher/${id}`, null, token); load(); }
        catch (e) { Alert.alert("Errore", e.message); }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', marginTop: 50, marginBottom: 50 }}>
          <Image
            source={require('./assets/images/logoits.png')}
            style={{ width: 100, height: 100 }}
          />
        </View>
        <SectionHeader title="Professori" action={
        role === "admin" && <Btn label="+ Professore" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
      } /></View>
      {loading ? <Loader /> : teachers.length === 0 ? <EmptyState message="Nessun professore trovato" /> :
        <FlatList
          data={teachers}
          keyExtractor={t => t.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: t }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={[s.avatar, { backgroundColor: "#C7D2FE" }]}><Text style={[s.avatarText, { color: "#4338CA" }]}>{t.firstName[0]}{t.lastName[0]}</Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>Prof. {t.firstName} {t.lastName}</Text>
              </View>
              {role === "admin" && (
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...t })}><Text style={s.actionIcon}>✏️</Text></TouchableOpacity>
                  <TouchableOpacity onPress={() => del(t.id)}><Text style={s.actionIcon}>🗑️</Text></TouchableOpacity>
                </View>
              )}
            </Card>
          )}
        />
      }
      <FormModal visible={showAdd} title="Aggiungi professore" onClose={() => setShowAdd(false)}>
        <Input label="Nome" value={form.firstName} onChangeText={v => setForm(f => ({ ...f, firstName: v }))} />
        <Input label="Cognome" value={form.lastName} onChangeText={v => setForm(f => ({ ...f, lastName: v }))} />
        <Btn label="Salva" onPress={add} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica professore" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.firstName} onChangeText={v => setShowEdit(f => ({ ...f, firstName: v }))} />
          <Input label="Cognome" value={showEdit.lastName} onChangeText={v => setShowEdit(f => ({ ...f, lastName: v }))} />
          <Btn label="Aggiorna" onPress={update} />
        </>}
      </FormModal>
    </View>
  );
}

// ─── SUBJECTS SCREEN ──────────────────────────────────────────────────────────
function SubjectsScreen() {
  const { token, user } = useAuth();
  const role = user?.role?.toLowerCase();
  const [subjects, setSubjects] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [showAdd, setShowAdd] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ name: "", teacherId: "" });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const path = search ? `/Subject/byname/${search}` : "/Subject";
      const data = await api("GET", path, null, token);
      setSubjects(Array.isArray(data) ? data : data ? [data] : []);
      const te = await api("GET", "/Teacher", null, token);
      setTeachers(Array.isArray(te) ? te : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setSubjects([]); }
    finally { setLoading(false); }
  }, [token, search]);

  useEffect(() => { load(); }, [load]);

  async function add() {
    try {
      await api("POST", "/Subject", form, token);
      setShowAdd(false); setForm({ name: "", teacherId: "" }); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function update() {
    try {
      await api("PUT", `/Subject/update/${showEdit.id}`, { name: showEdit.name, teacherId: showEdit.teacherId }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function del(id) {
    Alert.alert("Conferma", "Eliminare materia?", [
      { text: "Annulla" },
      { text: "Elimina", style: "destructive", onPress: async () => {
        try { await api("DELETE", `/Subject/${id}`, null, token); load(); }
        catch (e) { Alert.alert("Errore", e.message); }
      }},
    ]);
  }

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', marginTop: 50, marginBottom: 50 }}>
          <Image
            source={require('./assets/images/logoits.png')}
            style={{ width: 100, height: 100 }}
          />
        </View>
        <SectionHeader title="Materie" action={
          role === "admin" && <Btn label="+ Materia" onPress={() => setShowAdd(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per nome…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : subjects.length === 0 ? <EmptyState message="Nessuna materia trovata" /> :
        <FlatList
          data={subjects}
          keyExtractor={s => s.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: sub }) => (
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Text style={[s.avatarText, { color: "#92400E" }]}>📚</Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{sub.name}</Text>
                {sub.teacherFirstName && <Text style={s.itemSub}>👩‍🏫 {sub.teacherFirstName} {sub.teacherLastName}</Text>}
              </View>
              {role === "admin" && (
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <TouchableOpacity onPress={() => setShowEdit({ ...sub })}><Text style={s.actionIcon}>✏️</Text></TouchableOpacity>
                  <TouchableOpacity onPress={() => del(sub.id)}><Text style={s.actionIcon}>🗑️</Text></TouchableOpacity>
                </View>
              )}
            </Card>
          )}
        />
      }
      <FormModal visible={showAdd} title="Aggiungi materia" onClose={() => setShowAdd(false)}>
        <Input label="Nome materia" value={form.name} onChangeText={v => setForm(f => ({ ...f, name: v }))} />
        <SelectField
          label="Professore"
          value={form.teacherId}
          options={teachers}
          getLabel={(t) => `${t.firstName} ${t.lastName}`}
          onSelect={(v) => setForm(f => ({ ...f, teacherId: v }))}
          placeholder="Seleziona professore…"
          emptyMessage="Nessun professore disponibile"
        />
        <Btn label="Salva" onPress={add} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica materia" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Nome" value={showEdit.name} onChangeText={v => setShowEdit(f => ({ ...f, name: v }))} />
          <SelectField
            label="Professore"
            value={showEdit.teacherId}
            options={teachers}
            getLabel={(t) => `${t.firstName} ${t.lastName}`}
            onSelect={(v) => setShowEdit(f => ({ ...f, teacherId: v }))}
            placeholder="Seleziona professore…"
            emptyMessage="Nessun professore disponibile"
          />
          <Btn label="Aggiorna" onPress={update} />
        </>}
      </FormModal>
    </View>
  );
}

// ─── USERS SCREEN (admin only) ────────────────────────────────────────────────
function UsersScreen() {
  const { token } = useAuth();
  const [users, setUsers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [showRegister, setShowRegister] = useState(false);
  const [showEdit, setShowEdit] = useState(null);
  const [form, setForm] = useState({ email: "", password: "", role: "student", firstName: "", lastName: "" });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await api("GET", "/Users", null, token);
      setUsers(Array.isArray(data) ? data : []);
    } catch (e) { if (!e.message.includes("404")) Alert.alert("Errore", e.message); setUsers([]); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  async function register() {
    try {
      await api("POST", "/Auth/RegisterForAdmin", form, token);
      setShowRegister(false); setForm({ email: "", password: "", role: "student", firstName: "", lastName: "" }); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function updateUser() {
    try {
      await api("PUT", `/Users/update/${showEdit.id}`, { email: showEdit.email, role: showEdit.role }, token);
      setShowEdit(null); load();
    } catch (e) { Alert.alert("Errore", e.message); }
  }

  async function del(id) {
  Alert.alert("Conferma", "Eliminare utente?", [
    { text: "Annulla" },
    { text: "Elimina", style: "destructive", onPress: async () => {
      try {
        console.log("ELIMINAZIONE UTENTE ID:", id); // 👈 aggiunto
        await api("DELETE", `/Users/${id}`, null, token);
        console.log("DELETE COMPLETATA CON SUCCESSO"); // 👈 aggiunto
        load();
      }
      catch (e) {
        console.log("ERRORE DELETE:", e.message); // 👈 aggiunto
        Alert.alert("Errore", e.message);
      }
    }},
  ]);
}

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <View style={{ flex: 1, justifyContent: 'center', alignItems: 'center', marginTop: 50, marginBottom: 50 }}>
          <Image
            source={require('./assets/images/logoits.png')}
            style={{ width: 100, height: 100 }}
          />
        </View>
        <SectionHeader title="Utenti" action={
        <Btn label="+ Utente" onPress={() => setShowRegister(true)} style={s.smBtn} textStyle={s.smBtnText} />
      } /></View>
      {loading ? <Loader /> : users.length === 0 ? <EmptyState message="Nessun utente trovato" /> :
        <FlatList
          data={users}
          keyExtractor={u => u.id}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: u }) => (
            <Card style={{ marginBottom: 10 }}>
              <View style={{ flexDirection: "row", alignItems: "center" }}>
                <View style={s.avatar}><Text style={s.avatarText}>{u.email[0].toUpperCase()}</Text></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{u.email}</Text>
                  {(u.studentFirstName || u.teacherFirstName) &&
                    <Text style={s.itemSub}>{u.studentFirstName || u.teacherFirstName} {u.studentLastName || u.teacherLastName}</Text>}
                </View>
                <Badge text={u.role} role={u.role} />
              </View>
              <View style={{ flexDirection: "row", justifyContent: "flex-end", gap: 8, marginTop: 8 }}>
                <TouchableOpacity onPress={() => setShowEdit({ ...u })}><Text style={s.actionIcon}>✏️</Text></TouchableOpacity>
                <TouchableOpacity onPress={() => del(u.id)}><Text style={s.actionIcon}>🗑️</Text></TouchableOpacity>
              </View>
            </Card>
          )}
        />
      }
      <FormModal visible={showRegister} title="Registra utente" onClose={() => setShowRegister(false)}>
        <Input label="Email" value={form.email} onChangeText={v => setForm(f => ({ ...f, email: v }))} keyboardType="email-address" autoCapitalize="none" />
        <Input label="Password" value={form.password} onChangeText={v => setForm(f => ({ ...f, password: v }))} secureTextEntry />
        <Text style={s.label}>Ruolo</Text>
        <View style={{ flexDirection: "row", gap: 8, marginBottom: 14 }}>
          {["student", "teacher", "admin"].map(r => (
            <TouchableOpacity key={r} style={[s.roleBtn, form.role === r && s.roleBtnActive]} onPress={() => setForm(f => ({ ...f, role: r }))}>
              <Text style={[s.roleBtnText, form.role === r && s.roleBtnTextActive]}>{r}</Text>
            </TouchableOpacity>
          ))}
        </View>
        {form.role !== "admin" && <>
          <Input label="Nome" value={form.firstName} onChangeText={v => setForm(f => ({ ...f, firstName: v }))} />
          <Input label="Cognome" value={form.lastName} onChangeText={v => setForm(f => ({ ...f, lastName: v }))} />
        </>}
        <Btn label="Registra" onPress={register} />
      </FormModal>
      <FormModal visible={!!showEdit} title="Modifica utente" onClose={() => setShowEdit(null)}>
        {showEdit && <>
          <Input label="Email" value={showEdit.email} onChangeText={v => setShowEdit(f => ({ ...f, email: v }))} keyboardType="email-address" autoCapitalize="none" />
          <Text style={s.label}>Ruolo</Text>
          <View style={{ flexDirection: "row", gap: 8, marginBottom: 14 }}>
            {["student", "teacher", "admin"].map(r => (
              <TouchableOpacity key={r} style={[s.roleBtn, showEdit.role === r && s.roleBtnActive]} onPress={() => setShowEdit(f => ({ ...f, role: r }))}>
                <Text style={[s.roleBtnText, showEdit.role === r && s.roleBtnTextActive]}>{r}</Text>
              </TouchableOpacity>
            ))}
          </View>
          <Btn label="Aggiorna" onPress={updateUser} />
        </>}
      </FormModal>
    </View>
  );
}

// ─── PROFILE SCREEN ───────────────────────────────────────────────────────────
function ProfileScreen({ onLogout }) {
  const { user, token } = useAuth();
  const [showEdit, setShowEdit] = useState(false);
 
  // Form per l'admin: email, nome, cognome (via update) + cambio password (via updatepassword)
  const [form, setForm] = useState({ email: "", firstName: "", lastName: "", oldPassword: "", newPassword: "" });
 
  // Form per studente/docente (solo cambio password)
  const [pwdForm, setPwdForm] = useState({ oldPassword: "", newPassword: "" });
 
  const role = user?.role?.toLowerCase();
  const isAdmin = role === "admin";
 
  const name = `${user?.studentFirstName || user?.teacherFirstName || ""} ${user?.studentLastName || user?.teacherLastName || ""}`.trim();
 
  function resetForms() {
    setForm({ email: "", firstName: "", lastName: "", oldPassword: "", newPassword: "" });
    setPwdForm({ oldPassword: "", newPassword: "" });
  }
 
  function validateNewPassword(pwd) {
    if (pwd.length < 8) {
      Alert.alert("Attenzione", "La nuova password deve avere almeno 8 caratteri");
      return false;
    }
    const hasSpecialChar = /[!@#$%^&*()_\-+=<>?/[\]{}]/.test(pwd);
    if (!hasSpecialChar) {
      Alert.alert("Attenzione", "La nuova password deve contenere almeno un carattere speciale");
      return false;
    }
    return true;
  }
 
  async function save() {
    // Admin: email/firstName/lastName tramite /Users/update/{id}, password tramite /Users/updatepassword/{id}
    const body = {};
    if (form.email) body.email = form.email;
    if (form.firstName) body.firstName = form.firstName;
    if (form.lastName) body.lastName = form.lastName;
 
    const wantsPasswordChange = form.oldPassword || form.newPassword;
 
    if (Object.keys(body).length === 0 && !wantsPasswordChange) {
      Alert.alert("Attenzione", "Inserisci almeno un campo da modificare");
      return;
    }
 
    if (wantsPasswordChange) {
      if (!form.oldPassword || !form.newPassword) {
        Alert.alert("Attenzione", "Per cambiare la password inserisci vecchia e nuova password");
        return;
      }
      if (!validateNewPassword(form.newPassword)) return;
    }
 
    try {
      if (Object.keys(body).length > 0) {
        await api("PUT", `/Users/update/${user.id}`, body, token);
      }
      if (wantsPasswordChange) {
        await api("PUT", `/Users/updatepassword/${user.id}`, {
          oldPassword: form.oldPassword,
          newPassword: form.newPassword,
        }, token);
      }
      Alert.alert("Successo", "Profilo aggiornato!");
      setShowEdit(false);
      resetForms();
    } catch (e) { Alert.alert("Errore", e.message); }
  }
 
  async function savePassword() {
    // Studente/Docente: solo password tramite /Users/updatepassword/{id}
    if (!pwdForm.oldPassword || !pwdForm.newPassword) {
      Alert.alert("Attenzione", "Inserisci vecchia e nuova password");
      return;
    }
    if (!validateNewPassword(pwdForm.newPassword)) return;
 
    try {
      await api("PUT", `/Users/updatepassword/${user.id}`, {
        oldPassword: pwdForm.oldPassword,
        newPassword: pwdForm.newPassword,
      }, token);
      Alert.alert("Successo", "Password aggiornata!");
      setShowEdit(false);
      resetForms();
    } catch (e) { Alert.alert("Errore", e.message); }
  }
 
  return (
    <ScrollView style={{ flex: 1, backgroundColor: C.bg }} contentContainerStyle={{ padding: 16 }}>
    <View style={s.profileHeader}>
    <View style={s.profileAvatar}>
    <Text style={s.profileAvatarText}>{(user?.email || "?")[0].toUpperCase()}</Text>
    </View>
            {name && <Text style={s.profileName}>{name}</Text>}
    <Text style={s.profileEmail}>{user?.email}</Text>
    <Badge text={role} role={role} />
    </View>
    
          <Card style={{ marginTop: 16 }}>
    <Text style={[s.cardTitle, { fontSize: 14 }]}>Dettagli account</Text>
    <View style={s.profileRow}><Text style={s.profileKey}>Email</Text><Text style={s.profileVal}>{user?.email}</Text></View>
    <View style={s.profileRow}><Text style={s.profileKey}>Ruolo</Text><Text style={s.profileVal}>{role}</Text></View>
            {user?.studentId && <View style={s.profileRow}><Text style={s.profileKey}>ID Studente</Text><Text style={s.profileVal}>{user.studentId.slice(0, 16)}…</Text></View>}
            {user?.teacherId && <View style={s.profileRow}><Text style={s.profileKey}>ID Docente</Text><Text style={s.profileVal}>{user.teacherId.slice(0, 16)}…</Text></View>}
    </Card>
 
    <Btn
      label={isAdmin ? "✏️  Modifica profilo" : "🔒  Cambia password"}
      onPress={() => setShowEdit(true)}
      style={{ marginTop: 16 }}
      variant="primary"
    />
    <Btn label="🚪  Logout" onPress={onLogout} style={{ marginTop: 10 }} variant="danger" />
    
          {isAdmin ? (
    <FormModal visible={showEdit} title="Modifica profilo" onClose={() => { setShowEdit(false); resetForms(); }}>
    <Input label="Nuova email (opzionale)" value={form.email} onChangeText={v => setForm(f => ({ ...f, email: v }))} keyboardType="email-address" autoCapitalize="none" />
    <Input label="Vecchia password (opzionale)" value={form.oldPassword} onChangeText={v => setForm(f => ({ ...f, oldPassword: v }))} secureTextEntry />
    <Input label="Nuova password (opzionale)" value={form.newPassword} onChangeText={v => setForm(f => ({ ...f, newPassword: v }))} secureTextEntry />
    <Btn label="Salva" onPress={save} />
    </FormModal>
          ) : (
    <FormModal visible={showEdit} title="Cambia password" onClose={() => { setShowEdit(false); resetForms(); }}>
    <Input label="Vecchia password" value={pwdForm.oldPassword} onChangeText={v => setPwdForm(f => ({ ...f, oldPassword: v }))} secureTextEntry />
    <Input label="Nuova password" value={pwdForm.newPassword} onChangeText={v => setPwdForm(f => ({ ...f, newPassword: v }))} secureTextEntry />
    <Btn label="Salva" onPress={savePassword} />
    </FormModal>
          )}
    </ScrollView>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// NAVIGATION
// ═══════════════════════════════════════════════════════════════════════════════

function AppNav({ token, user, onLogout }) {
  const [screen, setScreen] = useState("dashboard");
  const insets = useSafeAreaInsets(); // ← aggiungi questa riga

  const tabs = [
    { key: "dashboard", icon: "🏠", label: "Home" },
    { key: "grades",    icon: "📊", label: "Voti" },
    { key: "subjects",  icon: "📚", label: "Materie" },
    ...(user?.role !== "student" ? [{ key: "students", icon: "👨‍🎓", label: "Studenti" }] : []),
    { key: "profile",   icon: "👤", label: "Profilo" },
  ];

  const SCREENS = {
    dashboard: <DashboardScreen navigate={setScreen} />,
    grades:    <GradesScreen />,
    students:  <StudentsScreen />,
    teachers:  <TeachersScreen />,
    subjects:  <SubjectsScreen />,
    users:     <UsersScreen />,
    profile:   <ProfileScreen onLogout={onLogout} />,
  };

  const titleMap = {
    dashboard: "Electronic Register", grades: "Voti", students: "Studenti",
    teachers: "Professori", subjects: "Materie", users: "Utenti", profile: "Profilo",
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
        <Text style={s.topBarTitle}> {titleMap[screen]}</Text>
      </View>

      {/* Screen */}
      <View style={{ flex: 1 }}>
        {SCREENS[screen] || <DashboardScreen navigate={setScreen} />}
      </View>

      {/* Bottom Tabs */}
      <View style={[s.tabBar, { paddingBottom: insets.bottom, backgroundColor: C.footer, borderTopWidth: 1, borderTopColor: C.border }]}>
        {tabs.map(t => (
          <TouchableOpacity key={t.key} style={s.tab} onPress={() => setScreen(t.key)}>
            <Text style={{ fontSize: 20 }}>{t.icon}</Text>
            <Text style={[s.tabLabel, screen === t.key && s.tabLabelActive], { color: "white"}}>{t.label}</Text>
          </TouchableOpacity>
        ))}
      </View>
    </View>
  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// ROOT
// ═══════════════════════════════════════════════════════════════════════════════

export default function App() {
  const [token, setToken] = useState(null);
  const [user, setUser] = useState(null);
  const [authScreen, setAuthScreen] = useState("login");
  const [booting, setBooting] = useState(false);

  async function handleLogin(t) {
    setToken(t);
    setBooting(true);
    try {
      const me = await api("GET", "/Auth/me", null, t);
      setUser(me);
    } catch (e) { Alert.alert("Errore", e.message); setToken(null); }
    finally { setBooting(false); }
  }

  function handleLogout() { setToken(null); setUser(null); setAuthScreen("login"); }

  if (booting) return (
    <View style={[s.loaderWrap, { flex: 1 }]}>
      <ActivityIndicator size="large" color={C.primary} />
      <Text style={{ color: C.textMuted, marginTop: 12 }}>Caricamento…</Text>
    </View>
  );

  if (!token) {
    if (authScreen === "register") return <RegisterScreen onBack={() => setAuthScreen("login")} />;
    return <LoginScreen onLogin={handleLogin} goRegister={() => setAuthScreen("register")} />;
  }

  return (
    <SafeAreaProvider>
      <AuthContext.Provider value={{ token, user }}>
        <AppNav token={token} user={user} onLogout={handleLogout} />
      </AuthContext.Provider>
    </SafeAreaProvider>

  );
}

// ═══════════════════════════════════════════════════════════════════════════════
// STYLES
// ═══════════════════════════════════════════════════════════════════════════════
const s = StyleSheet.create({
  // Auth
  authBg:        { flex: 1, backgroundColor: C.primary },
  authHeader:    { alignItems: "center", paddingTop: 60, paddingBottom: 32 },
  authLogo:      { fontSize: 48, marginBottom: 8 },
  authTitle:     { fontSize: 26, fontWeight: "800", color: C.white },
  authSubtitle:  { fontSize: 14, color: "rgba(255,255,255,0.7)", marginTop: 4 },
  authBody:      { backgroundColor: C.bg, borderTopLeftRadius: 28, borderTopRightRadius: 28, padding: 24, flexGrow: 1 },
  // Card
  card:          { backgroundColor: C.card, borderRadius: 14, padding: 16, shadowColor: "#000", shadowOpacity: 0.06, shadowRadius: 8, shadowOffset: { width: 0, height: 2 }, elevation: 3 },
  cardTitle:     { fontSize: 18, fontWeight: "700", color: C.text, marginBottom: 16 },
  // Inputs
  label:         { fontSize: 13, fontWeight: "600", color: C.textMuted, marginBottom: 6 },
  input:         { backgroundColor: C.bg, borderWidth: 1, borderColor: C.border, borderRadius: 10, padding: 12, fontSize: 15, color: C.text },
  // Select field
  selectBox:     { backgroundColor: C.bg, borderWidth: 1, borderColor: C.border, borderRadius: 10, padding: 12, flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  selectOption:  { flexDirection: "row", alignItems: "center", justifyContent: "space-between", paddingVertical: 14, paddingHorizontal: 4, borderBottomWidth: 1, borderBottomColor: C.border },
  selectOptionActive: { backgroundColor: "#EFF6FF" },
  selectOptionText: { fontSize: 15, color: C.text },
  selectOptionTextActive: { color: C.primary, fontWeight: "700" },
  // Buttons
  btn:           { borderRadius: 10, padding: 14, alignItems: "center", justifyContent: "center", flexDirection: "row" },
  btnText:       { fontSize: 15, fontWeight: "700" },
  smBtn:         { paddingVertical: 6, paddingHorizontal: 12, borderRadius: 8 },
  smBtnText:     { fontSize: 13 },
  // Badge
  badge:         { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 20 },
  badgeText:     { fontSize: 12, fontWeight: "700", textTransform: "uppercase" },
  // Dashboard
  dashHeader:    { backgroundColor: C.primary, padding: 20, paddingTop: 16, flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  dashWelcome:   { color: "rgba(255,255,255,0.7)", fontSize: 13 },
  dashName:      { color: C.white, fontSize: 20, fontWeight: "800" },
  dashGrid:      { flexDirection: "row", flexWrap: "wrap", gap: 12 },
  dashCard:      { width: "47%", backgroundColor: C.card, borderRadius: 14, padding: 20, alignItems: "center", shadowColor: "#000", shadowOpacity: 0.05, shadowRadius: 6, elevation: 2 },
  dashCardIcon:  { fontSize: 32, marginBottom: 8 },
  dashCardLabel: { fontSize: 14, fontWeight: "600", color: C.text, textAlign: "center" },
  // Top/Tab bar
  topBar:        { flexDirection: "row", alignItems: "center", justifyContent: 'center', paddingHorizontal: 16, paddingVertical: 14 },
  topBarTitle:   { color: C.white, fontSize: 24, fontWeight: "700" },
  tabBar:        { flexDirection: "row" },
  tab:           { flex: 1, alignItems: "center", paddingVertical: 8 },
  tabLabel:      { fontSize: 11, color: C.textLight, marginTop: 2 },
  tabLabelActive:{ color: C.primary, fontWeight: "700" },
  // List items
  screenPad:     { padding: 16, paddingBottom: 0 },
  sectionHeader: { flexDirection: "row", alignItems: "center", justifyContent: "space-between", marginBottom: 12 },
  sectionTitle:  { fontSize: 18, fontWeight: "800", color: C.text },
  itemTitle:     { fontSize: 15, fontWeight: "600", color: C.text },
  itemSub:       { fontSize: 13, color: C.textMuted, marginTop: 2 },
  actionIcon:    { fontSize: 18 },
  avatar:        { width: 44, height: 44, borderRadius: 22, backgroundColor: "#DBEAFE", alignItems: "center", justifyContent: "center" },
  avatarText:    { fontWeight: "700", color: "#1D4ED8", fontSize: 15 },
  // Grade circle
  gradeCircle:   { width: 48, height: 48, borderRadius: 24, alignItems: "center", justifyContent: "center" },
  gradeValue:    { color: C.white, fontWeight: "800", fontSize: 16 },
  // Profile
  profileHeader: { alignItems: "center", paddingVertical: 24 },
  profileAvatar: { width: 72, height: 72, borderRadius: 36, backgroundColor: C.primary, alignItems: "center", justifyContent: "center", marginBottom: 12 },
  profileAvatarText: { color: C.white, fontSize: 28, fontWeight: "800" },
  profileName:   { fontSize: 20, fontWeight: "800", color: C.text, marginBottom: 4 },
  profileEmail:  { fontSize: 14, color: C.textMuted, marginBottom: 10 },
  profileRow:    { flexDirection: "row", justifyContent: "space-between", paddingVertical: 8, borderBottomWidth: 1, borderBottomColor: C.border },
  profileKey:    { fontSize: 14, color: C.textMuted },
  profileVal:    { fontSize: 14, color: C.text, fontWeight: "600", maxWidth: "60%" },
  // Modal
  modalOverlay:  { flex: 1, backgroundColor: "rgba(0,0,0,0.45)", justifyContent: "flex-end" },
  modalCard:     { backgroundColor: C.card, borderTopLeftRadius: 24, borderTopRightRadius: 24, padding: 24, maxHeight: "85%" },
  modalHeader:   { flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginBottom: 20 },
  modalTitle:    { fontSize: 18, fontWeight: "700", color: C.text },
  modalClose:    { fontSize: 20, color: C.textMuted },
  // Role selector
  roleBtn:       { flex: 1, paddingVertical: 8, borderRadius: 8, borderWidth: 1, borderColor: C.border, alignItems: "center" },
  roleBtnActive: { backgroundColor: C.primary, borderColor: C.primary },
  roleBtnText:   { fontSize: 13, color: C.textMuted, fontWeight: "600" },
  roleBtnTextActive: { color: C.white },
  // Empty / Loader
  emptyState:    { flex: 1, alignItems: "center", justifyContent: "center", paddingTop: 60 },
  emptyIcon:     { fontSize: 40, marginBottom: 12 },
  emptyText:     { fontSize: 15, color: C.textMuted },
  loaderWrap:    { flex: 1, alignItems: "center", justifyContent: "center" },
});
