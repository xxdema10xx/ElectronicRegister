// src/screens/ExamsScreen.js
// Esami per docenti e amministratori: elenco, creazione/modifica con l'editor guidato, pubblicazione,
// chiusura, annullamento, eliminazione, consegne e correzione. Dati: ExamController (src/api/examApi.js).
import { useCallback, useEffect, useState } from "react";
import { FlatList, Modal, Platform, ScrollView, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import { useLayout } from "../hooks/useLayout";
import { useExamApi } from "../hooks/useExamApi";
import Card from "../components/Card";
import ExamCard from "../components/ExamCard";
import Btn from "../components/Btn";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import SelectField from "../components/SelectField";
import StatusChip, { EXAM_TONE } from "../components/StatusChip";
import { newTestDraft, validateTestDraft } from "../components/TestBuilder";
import ExamEditorScreen from "./ExamEditorScreen";
import { useExamRoute } from "../navigation/examRoutes";
import TestViewer from "../components/TestViewer";
import ExamSubmissions from "../components/ExamSubmissions";
import FormModal from "../components/FormModal";
import ClassFilter from "../components/ClassFilter";
import { filterBySearch } from "../utils/search";
import { notify, confirmAction } from "../utils/dialogs";
import { resetMockExams } from "../api/mockExamBackend";
import { EXAM_STATUS, EXAM_STATUS_LABEL, draftToDto, fromEnum, formatDateTime, parseUtc } from "../api/examContract";
import { getRetakeCandidates, getRetakeGroups } from "../api/examInsights";

// La bozza in corso sopravvive al refresh (F5) su web grazie a sessionStorage.
const DRAFT_KEY = "examDraft4";
const draftStore = {
  get() { try { return Platform.OS === "web" ? JSON.parse(window.sessionStorage.getItem(DRAFT_KEY)) : null; } catch { return null; } },
  set(d) { try { if (Platform.OS === "web") window.sessionStorage.setItem(DRAFT_KEY, JSON.stringify(d)); } catch {} },
  clear() { try { if (Platform.OS === "web") window.sessionStorage.removeItem(DRAFT_KEY); } catch {} },
};

// Scheda di un esame: bozze (non ancora pubblicati), attivi (pubblicati), storico (chiusi o annullati)
function tabOf(exam) {
  const st = fromEnum(exam.status, EXAM_STATUS);
  return st === "draft" ? "drafts" : st === "published" ? "active" : "history";
}

// Ordine dal più recente al più vecchio, per scheda: il primo campo presente decide.
// TODO(API): unico punto da cambiare quando ExamSummaryDto esporrà CreatedAt/UpdatedAt. Oggi li dà solo
// la modalità prova; con il server vero le bozze ripiegano sulla data di inizio disponibilità.
const SORT_FIELDS = {
  drafts: ["updatedAt", "createdAt", "availableFrom"],
  active: ["publishedAt", "availableFrom"],
  history: ["closedAt", "cancelledAt", "availableUntil"],
};
function sortExams(list, tab) {
  const time = (e) => {
    const field = SORT_FIELDS[tab].find(f => e[f]);
    return field ? parseUtc(e[field])?.getTime() || 0 : 0;
  };
  return [...list].sort((a, b) => time(b) - time(a));
}

const TABS = [["drafts", "Bozze", "gray"], ["active", "Attivi", "green"], ["history", "Storico", "blue"]];
const EMPTY = {
  drafts: "Nessuna bozza: usa “Crea esame” per prepararne una.",
  active: "Nessun esame pubblicato.",
  history: "Nessun esame nello storico.",
};
const BAR_H = 40; // altezza comune di filtri e pulsanti nella barra

export default function ExamsScreen() {
  const { user } = useAuth();
  const role = user?.role?.toLowerCase();
  const canWrite = role === "admin" || role === "teacher" || role === "staff";
  // Lo Staff gestisce gli esami ma non può annullarli né eliminarli (docs/AUTH.md §4.1)
  const canDestroy = role === "admin" || role === "teacher";
  const { isDesktop } = useLayout();
  const api = useExamApi();

  const [exams, setExams] = useState([]);
  const [classSubjects, setClassSubjects] = useState(null); // null: il server non offre l'elenco
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [busy, setBusy] = useState(null); // id dell'esame su cui si sta operando

  // Rotta corrente (URL su web, pila in memoria sull'app): lista, nuovo, dettaglio, modifica.
  // Editor e dettaglio sono aperti finché la rotta li indica: chiuderli = tornare indietro.
  const nav = useExamRoute();
  const route = nav.route;
  const [draft, setDraft] = useState(() => draftStore.get());
  const editing = canWrite && (route.name === "new" || route.name === "edit");
  const [saving, setSaving] = useState(false);
  const [viewing, setViewing] = useState(null);
  const [submissionsFor, setSubmissionsFor] = useState(null);
  const [tab, setTabState] = useState(() => (route.name === "list" && route.tab) || "active"); // "drafts" | "active" | "history"
  const [retakes, setRetakes] = useState(null); // null: chiuso, altrimenti { loading, groups }
  const [classId, setClassId] = useState(null); // filtro facoltativo: null = tutte le classi
  const [picking, setPicking] = useState(null); // scelta di classe e materia prima di creare: { classSubjectId }

  const csLabel = (id) => classSubjects?.find(c => c.id === id)?.label;

  // Classi disponibili, derivate dalle classi-materia (un docente insegna in più classi: va scelta subito).
  const classOptions = (() => {
    const byId = new Map();
    for (const cs of classSubjects || []) {
      if (!byId.has(cs.classId)) byId.set(cs.classId, { id: cs.classId, name: cs.label.split(" · ")[0] });
    }
    return [...byId.values()];
  })();
  const classSubjectsOfClass = (classSubjects || []).filter(cs => cs.classId === classId);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [list, cs] = await Promise.all([api.listExams(), api.getClassSubjects().catch(() => null)]);
      setExams(Array.isArray(list) ? list : []);
      setClassSubjects(cs);
    } catch (e) { notify("Errore", e.message); setExams([]); }
    finally { setLoading(false); }
  }, [api]);
  useEffect(() => { load(); }, [load]);

  const inClass = (e) => !classId || classSubjects?.find(c => c.id === e.classSubjectId)?.classId === classId;
  // Filtro classe e ricerca valgono per tutte e tre le schede: i contatori dicono cosa si vedrebbe aprendole.
  const filtered = filterBySearch(exams.filter(inClass), search, e => [e.name, e.description, csLabel(e.classSubjectId)]);

  // Bozza locale da riprendere: la card sta in cima alla scheda Bozze. Se riguarda un esame già salvato,
  // quella voce non si ripete in lista: la card ne è la versione più aggiornata (con le modifiche locali).
  const showResume = !!draft && !editing && inClass(draft);
  const resumedId = showResume ? draft.examId : null;
  const listed = filtered.filter(e => e.id !== resumedId);
  const countByTab = { drafts: showResume ? 1 : 0, active: 0, history: 0 };
  for (const e of listed) countByTab[tabOf(e)]++;
  const visible = sortExams(listed.filter(e => tabOf(e) === tab), tab);

  // Elenco di chi deve recuperare: raggruppa per studente le consegne non superate (bocciate o "non superabile")
  // su tutti gli esami pubblicati/chiusi della classe scelta. Richiede una chiamata per esame: va bene per i volumi di un registro di classe.
  async function openRetakes() {
    setRetakes({ loading: true, groups: [] });
    try { setRetakes({ loading: false, groups: await getRetakeGroups(api, exams.filter(inClass)) }); }
    catch (e) { notify("Errore", e.message); setRetakes(null); }
  }

  // dirty: la bozza ha modifiche non ancora inviate al server (false appena caricata da edit()).
  // savedAt: ora dell'ultimo salvataggio locale; solo su web, dove esiste sessionStorage.
  // Sull'app non c'è un archivio locale (AsyncStorage non è tra le dipendenze): lo stato lo dice.
  // keepDirty: la modifica non cambia lo stato rispetto al server (es. solo cambio di step).
  const [dirty, setDirty] = useState(() => !!draftStore.get());
  const [savedAt, setSavedAt] = useState(null);
  function updateDraft(d, { fromServer = false, keepDirty = false } = {}) {
    setDraft(d);
    d ? draftStore.set(d) : draftStore.clear();
    if (!keepDirty) setDirty(!!d && !fromServer);
    else if (!d) setDirty(false);
    setSavedAt(d && Platform.OS === "web" ? new Date() : null);
  }
  const hhmm = (t) => `${String(t.getHours()).padStart(2, "0")}:${String(t.getMinutes()).padStart(2, "0")}`;
  const localStatus = Platform.OS === "web" ? `conservata in questa scheda${savedAt ? ` alle ${hhmm(savedAt)}` : ""}` : "non salvata sul dispositivo";
  const draftStatus = !draft ? "" : !draft.examId ? `Non ancora salvata sul server · ${localStatus}`
    : dirty ? `Modifiche non salvate sul server · ${localStatus}` : "Salvata sul server";
  // Classe e materia si scelgono una volta sola, qui, prima di aprire l'editor: nel form restano fisse.
  // Se il server non offre l'elenco delle classi-materia, l'identificativo si inserisce nel form.
  // Scheda: nello stato e, su web, nell'URL (/esami?tab=bozze). Indietro/avanti la ripristinano.
  function setTab(next) { setTabState(next); nav.replace({ name: "list", tab: next }); }
  useEffect(() => {
    if (route.name !== "list") return;
    if (route.tab && route.tab !== tab) setTabState(route.tab);
    else if (!route.tab) nav.replace({ name: "list", tab });
  }, [route]);
  const backToList = () => nav.back({ name: "list", tab });

  // "Crea esame" → /esami/nuovo. Classe e materia si scelgono una volta sola, prima dell'editor
  // (finestra sopra la lista); nel form restano fisse. Se il server non offre l'elenco delle
  // classi-materia, l'identificativo si inserisce nel form.
  function startNew() {
    if (draft) return resume();
    nav.push({ name: "new" });
  }
  function resume() { nav.push(draft.examId ? { name: "edit", id: draft.examId } : { name: "new" }); }

  // Rotte che chiedono qualcosa a questa schermata: permessi, nuovo esame, dettaglio
  useEffect(() => {
    if (route.name !== "view" && viewing) setViewing(null); // riaprendo il dettaglio si ricarica, non si mostra quello vecchio
    if (route.name === "list") return;
    // Editor solo per chi gestisce gli esami: comodità, i permessi veri li verifica il server
    if (!canWrite && (route.name === "new" || route.name === "edit")) { nav.replace({ name: "list", tab }); return; }
    if (route.name === "new") {
      if (draft?.examId) {
        notify("Modifica in corso", "Hai una modifica non salvata su un altro esame: la riapro.");
        nav.replace({ name: "edit", id: draft.examId });
      } else if (!draft && !loading) {
        if (!Array.isArray(classSubjects)) updateDraft(newTestDraft());
        else {
          const options = classId ? classSubjectsOfClass : classSubjects;
          setPicking({ classSubjectId: options.length === 1 ? options[0].id : "" });
        }
      }
      return;
    }
    if (route.name === "view" && viewing?.id !== route.id) {
      let cancelled = false;
      setBusy(route.id);
      // TODO(API): in modalità prova risponde il mock; con il server vero è GET /institution/exams/{id}
      api.getExam(route.id)
        .then((detail) => { if (!cancelled) setViewing(detail); })
        .catch((e) => {
          if (cancelled) return;
          notify("Esame non trovato", e.status === 404 || /404/.test(e.message) ? "L'esame non esiste o è stato eliminato." : e.message);
          nav.replace({ name: "list", tab });
        })
        .finally(() => { if (!cancelled) setBusy(null); });
      return () => { cancelled = true; };
    }
  }, [route, loading]);

  function confirmPick() {
    if (!picking?.classSubjectId) return notify("Attenzione", "Scegli la classe e la materia dell'esame.");
    updateDraft({ ...newTestDraft(), classSubjectId: picking.classSubjectId });
    setPicking(null);
  }
  function cancelPick() { setPicking(null); backToList(); }
  function discardDraft() {
    confirmAction("Conferma", "Eliminare la bozza dell'esame?", () => { updateDraft(null); backToList(); }, "Elimina");
  }

  // "Modifica" dal dettaglio: il dettaglio lascia il posto all'editor, così "indietro" torna alla lista
  function edit(exam) {
    if (draft && !draft.examId && dirty) {
      return notify("Bozza in corso", "Hai un nuovo esame non salvato: salvalo o eliminalo prima di modificarne un altro.");
    }
    nav.replace({ name: "edit", id: exam.id });
  }

  async function save() {
    const err = validateTestDraft(draft);
    if (err) return notify("Attenzione", err);
    setSaving(true);
    try {
      const dto = draftToDto(draft);
      if (draft.examId) {
        await api.updateExam(draft.examId, dto);
        await api.updateExamStudents(draft.examId, { students: dto.assignedStudents });
      } else {
        await api.createExam(dto);
      }
      updateDraft(null); backToList();
      await load();
      notify("Esame salvato", "È in bozza: pubblicalo dall'elenco quando è pronto.");
    } catch (e) { notify("Errore", e.message); }
    finally { setSaving(false); }
  }

  // Tocco sulla card → /esami/{id}; il dettaglio si carica nell'effetto delle rotte qui sopra
  function view(exam) { nav.push({ name: "view", id: exam.id }); }
  const viewed = route.name === "view" && viewing?.id === route.id ? viewing : null;

  // Azioni di stato: pubblica, chiudi, annulla, elimina
  function act(exam, label, message, run, okLabel) {
    confirmAction(label, message, async () => {
      setBusy(exam.id);
      try { await run(); await load(); }
      catch (e) { notify("Errore", e.message); }
      finally { setBusy(null); }
    }, okLabel);
  }
  const publish = (e) => act(e, "Pubblica", `Pubblicare “${e.name}”? Gli studenti assegnati potranno svolgerlo e la struttura non sarà più modificabile.`, () => api.publishExam(e.id), "Pubblica");
  const close = (e) => act(e, "Chiudi", `Chiudere “${e.name}”? Nessuno studente potrà più iniziarlo.`, () => api.closeExam(e.id), "Chiudi");
  const cancel = (e) => act(e, "Annulla", `Annullare “${e.name}”? È possibile solo se nessuno lo ha ancora iniziato.`, () => api.cancelExam(e.id), "Annulla esame");
  const remove = (e) => act(e, "Elimina", `Eliminare “${e.name}”? Si può eliminare solo una bozza.`, () => api.deleteExam(e.id), "Elimina");

  const env = {
    classSubjects: classSubjects && classId ? classSubjectsOfClass : classSubjects,
    classSubjectLabel: csLabel(draft?.classSubjectId),
    retakeOptions: exams.filter(inClass).map(e => ({ id: e.id, name: e.name, classSubjectId: e.classSubjectId })),
    getStudents: (classSubjectId) => api.getStudentsOfClassSubject(classSubjectId),
    getRetakeCandidates: (examId) => getRetakeCandidates(api, examId),
  };

  const barBtn = { height: BAR_H, paddingVertical: 0, paddingHorizontal: 18 };
  const newBtn = canWrite && <Btn label="Crea esame" onPress={startNew} style={barBtn} textStyle={s.smBtnText} />;

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        {/* Barra dei filtri: una riga su schermi larghi; su quelli stretti filtri sopra e pulsanti sotto.
            Il titolo "Esami" lo mostra già la barra in alto (AppNav), anche su mobile. */}
        <View style={{ flexDirection: "row", flexWrap: "wrap", alignItems: "center", gap: 8, marginBottom: 12 }}>
          <View style={{ flexDirection: "row", gap: 8, flexBasis: isDesktop ? "auto" : "100%", flexShrink: 1 }}>
            {classOptions.length > 1 && (
              <ClassFilter value={classId} options={classOptions} onChange={setClassId} height={BAR_H}
                style={isDesktop ? { width: 220 } : { flex: 1, minWidth: 0 }} />
            )}
            <TextInput style={[s.input, { height: BAR_H, paddingVertical: 0, fontSize: 14 }, isDesktop ? { width: 300 } : { flex: 1, minWidth: 0 }]}
              placeholder="Cerca per nome…" placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
          </View>
          <View style={{ flexDirection: "row", gap: 8, marginLeft: isDesktop ? "auto" : 0 }}>
            {canWrite && <Btn label="Chi deve recuperare" variant="ghost" onPress={openRetakes} style={barBtn} textStyle={s.smBtnText} />}
            {newBtn}
          </View>
        </View>

        <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8, marginBottom: 12 }} accessibilityRole="tablist">
          {TABS.map(([key, label, tone]) => {
            const on = tab === key;
            return (
              <TouchableOpacity key={key} onPress={() => setTab(key)} accessibilityRole="tab" accessibilityState={{ selected: on }}
                accessibilityLabel={`${label}, ${countByTab[key]}`}
                style={{ flexDirection: "row", alignItems: "center", gap: 8, paddingVertical: 7, paddingHorizontal: 16, borderRadius: 18, borderWidth: 1, borderColor: on ? C.primary : C.border, backgroundColor: on ? C.primary : C.white }}>
                <Text style={{ color: on ? C.white : C.text, fontWeight: "600" }}>{label}</Text>
                <StatusChip label={String(countByTab[key])} tone={tone} style={{ alignSelf: "center", paddingVertical: 1, paddingHorizontal: 8 }} />
              </TouchableOpacity>
            );
          })}
        </View>

        {api.isMock() && (
          <View style={{ backgroundColor: "#FEF3C7", borderRadius: 10, padding: 10, marginBottom: 8, flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <Text style={{ color: "#92400E", flex: 1, minWidth: 200 }}>
              Modalità prova: il server non espone ancora gli esami (/Exam), i dati sono locali a questo browser.
            </Text>
            <TouchableOpacity onPress={() => { resetMockExams(); load(); }}><Text style={{ color: "#92400E", fontWeight: "700" }}>Ripristina dati</Text></TouchableOpacity>
          </View>
        )}
        {showResume && tab === "drafts" && (
          // Bozza in corso, in cima alla scheda Bozze: sparisce quando viene salvata sul server o eliminata (updateDraft(null)).
          <Card style={{ marginBottom: 12, flexDirection: "row", alignItems: "center", gap: 12, flexWrap: "wrap", borderLeftWidth: 5, borderLeftColor: C.warning }}>
            <View style={{ width: 40, height: 40, borderRadius: 12, alignItems: "center", justifyContent: "center", backgroundColor: "#FEF3C7" }}>
              <Ionicons name="create-outline" size={20} color="#92400E" />
            </View>
            <View style={{ flex: 1, minWidth: 180 }}>
              <Text style={s.itemTitle} numberOfLines={1}>
                {draft.examId ? "Modifica in corso" : "Esame in preparazione"}: {draft.title.trim() || "senza titolo"}
              </Text>
              <Text style={s.itemSub} numberOfLines={2}>
                {[csLabel(draft.classSubjectId), draftStatus].filter(Boolean).join(" · ")}
              </Text>
            </View>
            <Btn label="Riprendi bozza" onPress={resume} style={[s.smBtn, { paddingHorizontal: 16, paddingVertical: 10 }]} textStyle={s.smBtnText} />
          </Card>
        )}
      </View>

      {loading ? <Loader /> : visible.length === 0 ? (
        // Con la card "Riprendi bozza" in cima la scheda Bozze non è vuota: niente messaggio
        showResume && tab === "drafts" ? null : <EmptyState message={search.trim() ? `Nessun esame trovato per “${search.trim()}”.` : EMPTY[tab]} />
      ) : (
        <FlatList
          data={visible}
          keyExtractor={e => String(e.id)}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: exam }) => {
            const status = fromEnum(exam.status, EXAM_STATUS);
            return (
              <ExamCard exam={exam} status={status} contextLabel={csLabel(exam.classSubjectId)} busy={busy === exam.id}
                onPress={() => view(exam)}
                actions={{
                  submissions: canWrite && (status === "published" || status === "closed") ? () => setSubmissionsFor(exam) : null,
                  close: canWrite && status === "published" ? () => close(exam) : null,
                  publish: canWrite && status === "draft" ? () => publish(exam) : null,
                  cancel: canDestroy && status === "published" ? () => cancel(exam) : null,
                  remove: canDestroy && status === "draft" ? () => remove(exam) : null,
                }} />
            );
          }}
        />
      )}

      {/* "Modifica" solo per le bozze: un esame pubblicato non si può più cambiare (lo verifica comunque il server) */}
      <TestViewer exam={viewed} contextLabel={csLabel(viewed?.classSubjectId)} onClose={backToList}
        onEdit={viewed && canWrite && fromEnum(viewed.status, EXAM_STATUS) === "draft" ? () => edit(viewed) : null} />
      {submissionsFor && <ExamSubmissions exam={submissionsFor} api={api} onClose={() => setSubmissionsFor(null)} />}

      {retakes && (
        <Modal visible animationType="slide" onRequestClose={() => setRetakes(null)}>
          <View style={{ flex: 1, backgroundColor: C.bg }}>
            <View style={{ flexDirection: "row", alignItems: "center", gap: 10, backgroundColor: C.white, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderBottomColor: C.border }}>
              <TouchableOpacity onPress={() => setRetakes(null)}><Ionicons name="arrow-back" size={26} color={C.text} /></TouchableOpacity>
              <View style={{ flex: 1 }}>
                <Text style={{ color: C.text, fontSize: 16, fontWeight: "700" }}>Chi deve recuperare</Text>
                <Text style={{ color: C.textMuted, fontSize: 12 }}>Studenti non in regola su almeno un esame pubblicato o chiuso</Text>
              </View>
            </View>
            {retakes.loading ? <Loader /> : retakes.groups.length === 0 ? <EmptyState message="Nessuno studente da recuperare" /> : (
              <ScrollView contentContainerStyle={{ padding: 16, alignItems: "center" }}>
                <View style={{ width: "100%", maxWidth: 760 }}>
                  {retakes.groups.map(g => (
                    <Card key={g.studentId} style={{ marginBottom: 10 }}>
                      <Text style={s.itemTitle}>{g.lastName} {g.firstName}</Text>
                      {g.exams.map(e => (
                        <View key={e.id} style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", marginTop: 6 }}>
                          <Text style={{ color: C.text, flex: 1 }} numberOfLines={1}>{e.name}{csLabel(e.classSubjectId) ? ` · ${csLabel(e.classSubjectId)}` : ""}</Text>
                          <StatusChip label={EXAM_STATUS_LABEL[e.status] || e.status} tone={EXAM_TONE[e.status]} />
                        </View>
                      ))}
                    </Card>
                  ))}
                </View>
              </ScrollView>
            )}
          </View>
        </Modal>
      )}
      {picking && (
        <FormModal visible title="Nuovo esame" onClose={cancelPick}>
          <View style={{ padding: 16 }}>
            <Text style={[s.itemSub, { marginTop: 0, marginBottom: 14 }]}>
              Per quale classe e materia è l'esame? Non si potrà cambiare dopo: serve a scegliere gli studenti e l'esame da recuperare.
            </Text>
            <SelectField label="Classe e materia" value={picking.classSubjectId} options={classId ? classSubjectsOfClass : classSubjects}
              getLabel={c => c.label} onSelect={(classSubjectId) => setPicking({ classSubjectId })}
              placeholder="Es. SW-25A · Programmazione" emptyMessage="Nessuna classe-materia disponibile" />
            <View style={{ flexDirection: "row", justifyContent: "flex-end", gap: 8, marginTop: 4 }}>
              <Btn label="Annulla" variant="ghost" onPress={cancelPick} style={s.smBtn} textStyle={s.smBtnText} />
              <Btn label="Inizia →" onPress={confirmPick} style={[s.smBtn, { paddingHorizontal: 20, paddingVertical: 10 }]} textStyle={s.smBtnText} />
            </View>
          </View>
        </FormModal>
      )}
      {editing && (
        <ExamEditorScreen key={`${route.name}:${route.id || ""}`} route={route} nav={nav} api={api}
          draft={draft} dirty={dirty} updateDraft={updateDraft} env={env} draftStatus={draftStatus}
          contextLabel={draft?.examId ? "Modifica della bozza" : "Nuovo esame"}
          onSave={save} saving={saving} onExit={backToList}
          onDiscard={draft?.examId ? () => { updateDraft(null); backToList(); } : discardDraft} />
      )}
    </View>
  );
}
