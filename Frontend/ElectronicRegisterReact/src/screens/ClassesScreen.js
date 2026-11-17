// src/screens/ClassesScreen.js
// Solo admin. Navigazione a scendere: biennio → area di studio → corso di studio → classe → dettaglio
// (insegnanti, materie, studenti). A ogni livello si può aggiungere un elemento; nel dettaglio si assegnano
// le materie della classe con il loro docente (senza assegnazioni il docente non vede la classe).
import { useEffect, useState } from "react";
import { FlatList, ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import ListRow, { listProps } from "../components/ListRow";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import Btn from "../components/Btn";
import IconButton from "../components/IconButton";
import { CatalogAddModal, ClassSubjectModal } from "../components/ClassCatalogForms";
import { getActiveBienni, getStudyAreas, getStudyPaths, getClasses, getClassDetail, removeClassSubject } from "../api/classService";
import { confirmAction, notify } from "../utils/dialogs";

const LEVELS = {
  bienni: { title: "Bienni", empty: "Nessun biennio: aggiungine uno", add: "+ Biennio" },
  areas: { title: "Aree di studio", empty: "Nessuna area di studio per questo biennio", add: "+ Area" },
  paths: { title: "Corsi di studio", empty: "Nessun corso di studio per questa area", add: "+ Corso" },
  classes: { title: "Classi", empty: "Nessuna classe per questo corso", add: "+ Classe" },
};
const DETAIL_TABS = [
  { key: "teachers", label: "Insegnanti" },
  { key: "subjects", label: "Materie" },
  { key: "students", label: "Studenti" },
];

const initials = (a, b) => `${a?.[0] ?? ""}${b?.[0] ?? ""}`.toUpperCase();

export default function ClassesScreen() {
  const { token, user } = useAuth();
  const isAdmin = user?.role?.toLowerCase() === "admin";

  const [sel, setSel] = useState({ biennium: null, area: null, path: null, klass: null });
  const [items, setItems] = useState([]);
  const [detail, setDetail] = useState(null);
  const [tab, setTab] = useState("teachers");
  const [loading, setLoading] = useState(true);
  const [reload, setReload] = useState(0); // dopo un'aggiunta o una modifica si rilegge il livello corrente
  const [adding, setAdding] = useState(false); // modulo "+ Biennio / Area / Corso / Classe"
  const [subjectForm, setSubjectForm] = useState(null); // { current? }: aggiungi materia o cambia docente
  const refresh = () => setReload(n => n + 1);

  const level = !sel.biennium ? "bienni" : !sel.area ? "areas" : !sel.path ? "paths" : !sel.klass ? "classes" : "detail";

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setLoading(true);
      try {
        if (level === "detail") {
          const d = await getClassDetail(sel.klass.id, sel.klass.name, token);
          if (!cancelled) setDetail(d);
        } else {
          const data = level === "bienni" ? await getActiveBienni(token)
            : level === "areas" ? await getStudyAreas(sel.biennium.id, token)
            : level === "paths" ? await getStudyPaths(sel.area.id, token)
            : await getClasses(sel.path.id, token);
          if (!cancelled) setItems(Array.isArray(data) ? data : []);
        }
      } catch (e) {
        if (!cancelled) { setItems([]); setDetail(null); notify("Errore", e.message); }
      } finally { if (!cancelled) setLoading(false); }
    }
    load();
    return () => { cancelled = true; };
  }, [level, sel, token, reload]);

  if (!isAdmin) return <EmptyState message="Sezione riservata agli amministratori" />;

  // Percorso scelto finora, cliccabile per tornare a un livello precedente
  const crumbs = [
    { label: "Bienni", value: sel.biennium && `${sel.biennium.startYear}-${sel.biennium.endYear}`, reset: { biennium: null, area: null, path: null, klass: null } },
    { label: "Area", value: sel.area?.name, reset: { area: null, path: null, klass: null } },
    { label: "Corso", value: sel.path?.name, reset: { path: null, klass: null } },
    { label: "Classe", value: sel.klass?.name, reset: { klass: null } },
  ];
  const goBack = () => setSel(p => level === "areas" ? { ...p, biennium: null } : level === "paths" ? { ...p, area: null }
    : level === "classes" ? { ...p, path: null } : { ...p, klass: null });
  const choose = (item) => {
    setTab("teachers");
    setSel(p => level === "bienni" ? { ...p, biennium: item } : level === "areas" ? { ...p, area: item }
      : level === "paths" ? { ...p, path: item } : { ...p, klass: item });
  };

  // Insegnanti = insegnanti distinti delle materie della classe, con le materie che insegnano
  const teachers = [];
  (detail?.subjects || []).forEach(sb => {
    let t = teachers.find(x => x.id === sb.teacherId);
    if (!t) { t = { id: sb.teacherId, firstName: sb.teacherFirstName, lastName: sb.teacherLastName, subjects: [] }; teachers.push(t); }
    t.subjects.push(sb.subjectName);
  });

  function removeSubject(item) {
    confirmAction("Togliere la materia?", `${item.subjectName} non sarà più insegnata in questa classe.`, async () => {
      try { await removeClassSubject(item.classSubjectId, token); refresh(); }
      catch (e) { notify("Errore", e.status === 409 ? "Non si può togliere: ci sono esami o voti collegati a questa materia nella classe." : e.message); }
    }, "Togli", { destructive: true });
  }

  const itemTitle = (it) => level === "bienni" ? `Biennio ${it.startYear}-${it.endYear}` : it.name;
  const itemIcon = level === "bienni" ? "calendar-outline" : level === "areas" ? "layers-outline" : level === "paths" ? "git-branch-outline" : "people-outline";

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 10, marginBottom: 8 }}>
          {level !== "bienni" && (
            <TouchableOpacity onPress={goBack}><Ionicons name="arrow-back" size={26} color={C.text} /></TouchableOpacity>
          )}
          <Text style={{ fontSize: 20, fontWeight: "700", color: C.text, flex: 1 }}>
            {level === "detail" ? `Classe ${sel.klass.name}` : LEVELS[level].title}
          </Text>
          {level === "detail"
            ? <Btn label="+ Materia" onPress={() => setSubjectForm({})} style={s.smBtn} textStyle={s.smBtnText} />
            : <Btn label={LEVELS[level].add} onPress={() => setAdding(true)} style={s.smBtn} textStyle={s.smBtnText} />}
        </View>

        {/* Percorso: tocca un elemento per tornare a quel livello */}
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ marginBottom: 8 }}>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
            {crumbs.filter(c => c.value).map((c, i) => (
              <View key={c.label} style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
                {i > 0 && <Ionicons name="chevron-forward" size={14} color={C.textLight} />}
                <TouchableOpacity onPress={() => setSel(p => ({ ...p, ...c.reset }))}
                  style={{ backgroundColor: "#E0E7FF", borderRadius: 14, paddingVertical: 4, paddingHorizontal: 10 }}>
                  <Text style={{ color: C.primary, fontSize: 13 }} numberOfLines={1}>{c.value}</Text>
                </TouchableOpacity>
              </View>
            ))}
          </View>
        </ScrollView>
      </View>

      {loading ? <Loader /> : level !== "detail" ? (
        items.length === 0 ? <EmptyState message={LEVELS[level].empty} /> :
        <FlatList
          data={items}
          keyExtractor={it => String(it.id)}
          {...listProps()}
          renderItem={({ item }) => (
            <ListRow onPress={() => choose(item)}>
              <View style={s.avatar}><Ionicons name={itemIcon} size={20} color="#1D4ED8" /></View>
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{itemTitle(item)}</Text>
                {!!item.description && <Text style={s.itemSub}>{item.description}</Text>}
              </View>
              <Ionicons name="chevron-forward" size={20} color={C.textLight} />
            </ListRow>
          )}
        />
      ) : !detail ? <EmptyState message="Dettaglio non disponibile" /> : (
        <View style={{ flex: 1 }}>
          <View style={{ flexDirection: "row", marginHorizontal: 16, marginBottom: 10, backgroundColor: "#E2E8F0", borderRadius: 10, padding: 3 }}>
            {DETAIL_TABS.map(t => {
              const count = t.key === "teachers" ? teachers.length : t.key === "subjects" ? detail.subjects.length : detail.students.length;
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
            data={tab === "teachers" ? teachers : tab === "subjects" ? detail.subjects : detail.students}
            keyExtractor={(it, i) => String(it.id ?? it.subjectId) + i}
            {...listProps()}
            ListEmptyComponent={<EmptyState message="Nessun elemento" />}
            renderItem={({ item }) => tab === "teachers" ? (
              <ListRow>
                <View style={[s.avatar, { backgroundColor: "#D1FAE5" }]}><Text style={[s.avatarText, { color: "#065F46" }]}>{initials(item.firstName, item.lastName)}</Text></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{item.firstName} {item.lastName}</Text>
                  <Text style={s.itemSub}>{item.subjects.join(" · ")}</Text>
                </View>
              </ListRow>
            ) : tab === "subjects" ? (
              <ListRow>
                <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Ionicons name="book-outline" size={20} color="#92400E" /></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{item.subjectName}</Text>
                  <Text style={s.itemSub}>Prof. {item.teacherFirstName} {item.teacherLastName}</Text>
                </View>
                <View style={{ flexDirection: "row", gap: 8 }}>
                  <IconButton icon="swap-horizontal-outline" label="Cambia docente" onPress={() => setSubjectForm({ current: item })} />
                  <IconButton icon="trash-outline" label="Togli materia" onPress={() => removeSubject(item)} />
                </View>
              </ListRow>
            ) : (
              <ListRow>
                <View style={s.avatar}><Text style={s.avatarText}>{initials(item.firstName, item.lastName)}</Text></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{item.firstName} {item.lastName}</Text>
                </View>
              </ListRow>
            )}
          />
        </View>
      )}

      <CatalogAddModal visible={adding} level={level} sel={sel} linked={items} onClose={() => setAdding(false)} onDone={refresh} />
      <ClassSubjectModal visible={!!subjectForm} classId={sel.klass?.id} current={subjectForm?.current}
        taken={(detail?.subjects || []).map(x => x.subjectId)} onClose={() => setSubjectForm(null)} onDone={refresh} />
    </View>
  );
}
