// src/screens/MyClassesScreen.js
// Solo insegnanti: elenco delle classi in cui insegnano (non tutti gli studenti del registro).
// Toccando una classe si vedono le materie insegnate in quella classe e gli studenti che la frequentano.
import { useCallback, useEffect, useState } from "react";
import { FlatList, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import { filterBySearch } from "../utils/search";
import { getMyClasses } from "../api/classService";
import { notify } from "../utils/dialogs";

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

export default function MyClassesScreen() {
  const { token, user } = useAuth();
  const [classes, setClasses] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [klass, setKlass] = useState(null); // classe aperta
  const [tab, setTab] = useState("subjects");
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
            <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
              <View style={s.avatar}><Text style={s.avatarText}>{initials(item.firstName, item.lastName)}</Text></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{item.firstName} {item.lastName}</Text>
              </View>
            </Card>
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
