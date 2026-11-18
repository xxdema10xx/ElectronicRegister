// src/screens/UsersScreen.js
// Membri dell'istituto (solo admin): nuovi account, ruoli, sospensione e riattivazione. Gli utenti non si
// registrano da soli: l'account lo crea l'istituto (POST /api/institution/accounts) con una password temporanea.
import { useCallback, useEffect, useState } from "react";
import { Alert, FlatList, Text, TextInput, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import ListRow, { listProps } from "../components/ListRow";
import IconButton from "../components/IconButton";
import Input from "../components/Input";
import Btn from "../components/Btn";
import Badge from "../components/Badge";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import FormModal from "../components/FormModal";
import AccountFormModal from "../components/AccountFormModal";
import { getClassesForPicker } from "../api/lessonService";
import { filterBySearch } from "../utils/search";
import { getMember, getMembers, mutateMember } from "../api/userService";

const ROLES = [
  { id: "InstitutionAdmin", label: "Amministratore", badge: "admin" },
  { id: "Staff", label: "Segreteria", badge: "staff" },
  { id: "Teacher", label: "Docente", badge: "teacher" },
  { id: "Student", label: "Studente", badge: "student" },
];
const STATUS_LABEL = { active: "Attivo", suspended: "Sospeso", pending: "In attesa", left: "Uscito" };
export const MEMBER_CHANGED_MESSAGE = "Il membro è stato modificato nel frattempo: ho ricaricato i dati, controlla e riprova.";
export const LAST_ADMIN_MESSAGE = "Non si può togliere l'ultimo amministratore attivo dell'istituto: prima assegna il ruolo di amministratore a un altro membro.";
export const NO_OP_MESSAGE = "La modifica non ha effetto: il membro ha già questo ruolo o questo stato. Ho ricaricato i dati.";
export const EVENT_CONFLICT_MESSAGE = "La stessa richiesta risulta già inviata con dati diversi: ho ricaricato i dati, controlla e riprova.";
export const INVALID_TRANSITION_MESSAGE = "Questa azione non è possibile nello stato attuale del membro (in attesa o uscito dall'istituto). Ho ricaricato i dati.";
// Motivo del 409 dal `code` del server (POST /institution/admin/members/{id}/mutations); senza code, testo neutro
const CONFLICT_MESSAGES = {
  LastAdministrator: LAST_ADMIN_MESSAGE,
  StaleRevision: MEMBER_CHANGED_MESSAGE,
  ConcurrentChange: MEMBER_CHANGED_MESSAGE, // come StaleRevision: ricarica e riprova
  InvalidTransition: INVALID_TRANSITION_MESSAGE,
  NoOp: NO_OP_MESSAGE,
  EventIdConflict: EVENT_CONFLICT_MESSAGE,
};
export const memberConflictMessage = (e) => CONFLICT_MESSAGES[e?.code] || MEMBER_CHANGED_MESSAGE;
// Il 409 delle mutazioni è generico (revisione cambiata, transizione non valida, ultimo amministratore):
// il caso "ultimo amministratore" si dice solo se è il server a indicarlo (titolo/detail o code)

export default function UsersScreen() {
  const { token, user } = useAuth();
  const [members, setMembers] = useState([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const visible = filterBySearch(members, search, m => [m.email]);
  const [editing, setEditing] = useState(null); // membro aperto
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);
  const [creating, setCreating] = useState(false); // modulo "Nuovo account"
  const [classOptions, setClassOptions] = useState([]); // per assegnare subito la classe a un nuovo studente
  // Il ruolo serve a scegliere la fonte dell'elenco: lo staff non può leggere il catalogo delle classi
  useEffect(() => { getClassesForPicker(token, user?.role).then(setClassOptions).catch(() => setClassOptions([])); }, [token, user?.role]);

  const load = useCallback(async () => {
    setLoading(true);
    try { setMembers(await getMembers(token)); }
    catch (e) { if (e.status !== 404) Alert.alert("Errore", e.message); setMembers([]); }
    finally { setLoading(false); }
  }, [token]);

  useEffect(() => { load(); }, [load]);

  // Il dialog lavora sul dettaglio appena letto dal server: ruoli, stato e revisione (expectedRevision)
  async function refreshEditing(membershipId) {
    const fresh = await getMember(membershipId, token);
    setEditing(m => (m && m.membershipId === membershipId ? { ...m, ...fresh } : m));
    return fresh;
  }

  function openMember(m) {
    setReason("");
    setEditing({ ...m });
    refreshEditing(m.membershipId).catch(() => { /* resta la riga dell'elenco: la revisione si rilegge alla modifica */ });
  }

  async function apply(action, role) {
    if (!reason.trim()) { Alert.alert("Attenzione", "Indica il motivo della modifica (resta nello storico)."); return; }
    setBusy(true);
    try {
      const res = await mutateMember(editing.membershipId, { action, role, reason, expectedRevision: editing.revision }, token);
      setEditing(m => ({ ...m, status: res.status, roles: res.roles, revision: res.revision ?? m.revision }));
      setReason("");
      refreshEditing(editing.membershipId).catch(() => {});
      load();
    } catch (e) {
      if (e.status === 409) {
        // Ruoli, stato e revisione possono essere cambiati: si ricaricano prima di far riprovare
        await refreshEditing(editing.membershipId).catch(() => {});
        load();
        Alert.alert("Modifica non applicata", memberConflictMessage(e));
      } else Alert.alert("Errore", e.message);
    } finally { setBusy(false); }
  }

  const isMe = (m) => m?.userId === user?.id;

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <SectionHeader title="Membri dell'istituto" action={
          <Btn label="+ Account" onPress={() => setCreating(true)} style={s.smBtn} textStyle={s.smBtnText} />
        } />
        <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per email…"
          placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
      </View>
      {loading ? <Loader /> : visible.length === 0 ? <EmptyState message="Nessun membro trovato" /> :
        <FlatList
          data={visible}
          keyExtractor={m => m.membershipId}
          {...listProps()}
          renderItem={({ item: m }) => (
            <ListRow style={{ flexWrap: "wrap", rowGap: 8 }}>
              <View style={s.avatar}><Text style={s.avatarText}>{(m.email || "?")[0].toUpperCase()}</Text></View>
              <View style={{ flex: 1, minWidth: 160, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{m.email}{isMe(m) ? " (tu)" : ""}</Text>
                <Text style={s.itemSub}>{STATUS_LABEL[m.status] || m.status}</Text>
              </View>
              <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 6, marginHorizontal: 12 }}>
                {(m.roles || []).map(r => { const def = ROLES.find(x => x.id === r); return <Badge key={r} text={def?.label || r} role={def?.badge} />; })}
              </View>
              <IconButton icon="create-outline" label="Gestisci membro" onPress={() => openMember(m)} />
            </ListRow>
          )}
        />
      }
      <AccountFormModal visible={creating} onClose={() => setCreating(false)} onCreated={() => load()}
        allowedRoles={["Student", "Teacher", "Staff", "InstitutionAdmin"]} classOptions={classOptions} />
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
