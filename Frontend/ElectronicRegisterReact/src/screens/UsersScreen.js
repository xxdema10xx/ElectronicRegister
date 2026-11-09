// src/screens/UsersScreen.js
// Membri dell'istituto (solo admin): ruoli, sospensione e riattivazione. Nuovi utenti: si registrano
// da soli dalla schermata di login e poi vengono invitati nell'istituto.
import { useCallback, useEffect, useState } from "react";
import { Alert, FlatList, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import Input from "../components/Input";
import Btn from "../components/Btn";
import Badge from "../components/Badge";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import FormModal from "../components/FormModal";
import { filterBySearch } from "../utils/search";
import { getMembers, mutateMember } from "../api/userService";

const ROLES = [
  { id: "InstitutionAdmin", label: "Amministratore", badge: "admin" },
  { id: "Staff", label: "Segreteria", badge: "staff" },
  { id: "Teacher", label: "Docente", badge: "teacher" },
  { id: "Student", label: "Studente", badge: "student" },
];
const STATUS_LABEL = { active: "Attivo", suspended: "Sospeso", pending: "In attesa", left: "Uscito" };

export default function UsersScreen() {
  const { token, user } = useAuth();
  const [members, setMembers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const visible = filterBySearch(members, search, m => [m.email]);
  const [editing, setEditing] = useState(null); // membro aperto
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try { setMembers(await getMembers(token)); }
    catch (e) { if (e.status !== 404) Alert.alert("Errore", e.message); setMembers([]); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  async function apply(action, role) {
    if (!reason.trim()) { Alert.alert("Attenzione", "Indica il motivo della modifica (resta nello storico)."); return; }
    setBusy(true);
    try {
      const res = await mutateMember(editing.membershipId, { action, role, reason }, token);
      setEditing(m => ({ ...m, status: res.status, roles: res.roles }));
      setReason("");
      load();
    } catch (e) {
      Alert.alert("Errore", e.status === 409
        ? "Modifica non consentita (ad esempio non si può togliere l'ultimo amministratore). Ricarica e riprova."
        : e.message);
    } finally { setBusy(false); }
  }

  const isMe = (m) => m?.userId === user?.id;

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Membri dell'istituto" />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per email…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : visible.length === 0 ? <EmptyState message="Nessun membro trovato" /> :
        <FlatList
          data={visible}
          keyExtractor={m => m.membershipId}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: m }) => (
            <Card style={{ marginBottom: 10 }}>
              <View style={{ flexDirection: "row", alignItems: "center" }}>
                <View style={s.avatar}><Text style={s.avatarText}>{(m.email || "?")[0].toUpperCase()}</Text></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{m.email}{isMe(m) ? " (tu)" : ""}</Text>
                  <Text style={s.itemSub}>{STATUS_LABEL[m.status] || m.status}</Text>
                </View>
                <TouchableOpacity onPress={() => { setReason(""); setEditing({ ...m }); }}>
                  <Ionicons name="create-outline" size={32} color={C.footer} />
                </TouchableOpacity>
              </View>
              <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 6, marginTop: 8 }}>
                {(m.roles || []).map(r => { const def = ROLES.find(x => x.id === r); return <Badge key={r} text={def?.label || r} role={def?.badge} />; })}
              </View>
            </Card>
          )}
        />
      }
      <FormModal visible={!!editing} title="Gestisci membro" onClose={() => setEditing(null)}>
        {editing && <>
          <Text style={s.itemTitle}>{editing.email}</Text>
          <Text style={[s.itemSub, { marginBottom: 12 }]}>Stato: {STATUS_LABEL[editing.status] || editing.status}</Text>
          <Input label="Motivo della modifica (obbligatorio)" value={reason} onChangeText={setReason} placeholder="Es. incarico di segreteria" />
          <Text style={s.label}>Ruoli</Text>
          {ROLES.map(r => {
            const has = (editing.roles || []).includes(r.id);
            return (
              <View key={r.id} style={[s.profileRow, { alignItems: "center" }]}>
                <Text style={[s.profileKey, { flex: 1 }]}>{r.label}</Text>
                <Btn label={has ? "Togli" : "Aggiungi"} variant={has ? "ghost" : "primary"} disabled={busy}
                  onPress={() => apply(has ? "RemoveRole" : "AddRole", r.id)} style={s.smBtn} textStyle={s.smBtnText} />
              </View>
            );
          })}
          {!isMe(editing) && (editing.status === "active"
            ? <Btn label="Sospendi membro" variant="danger" onPress={() => apply("Suspend")} loading={busy} style={{ marginTop: 14 }} />
            : editing.status === "suspended" && <Btn label="Riattiva membro" variant="success" onPress={() => apply("Reactivate")} loading={busy} style={{ marginTop: 14 }} />)}
        </>}
      </FormModal>
    </View>
  );
}
