// src/screens/ExamsScreen.js
// Esami per docenti e amministratori: elenco, creazione/modifica con l'editor guidato, pubblicazione,
// chiusura, annullamento, eliminazione, consegne e correzione. Dati: ExamController (src/api/examApi.js).
import { useCallback, useEffect, useRef, useState } from "react";
import { BackHandler, FlatList, Platform, ScrollView, Text, TextInput, TouchableOpacity, View } from "react-native";
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
import { draftHasContent, newTestDraft, serverIssue, validateTestDraft } from "../components/TestBuilder";
import ExamEditorScreen from "./ExamEditorScreen";
import { examHref, setPageHeader, useExamRoute } from "../navigation/examRoutes";
import TestViewer from "../components/TestViewer";
import ExamSubmissions, { SubmissionReview } from "../components/ExamSubmissions";
import ExamRetakes from "../components/ExamRetakes";
import ClassFilter from "../components/ClassFilter";
import { filterBySearch } from "../utils/search";
import { notify, confirmAction } from "../utils/dialogs";
import { isGuid, readUser, removeUser, writeUser } from "../utils/userStorage";
import { examErrorMessage } from "../utils/examErrors";
import { EXAM_STATUS, EXAM_STATUS_LABEL, detailToDraft, draftToDto, fromEnum, formatDateTime, parseUtc } from "../api/examContract";
import { getRetakeCandidates, getRetakeGroups, invalidateExamInsights } from "../api/examInsights";

// La bozza in corso sopravvive al refresh (F5) su web grazie a sessionStorage.
const DRAFT_KEY = "examDraft4";
const draftStore = {
  get: () => readUser("session", DRAFT_KEY, null),
  set: (d) => writeUser("session", DRAFT_KEY, d),
  clear: () => removeUser("session", DRAFT_KEY),
};
// Una bozza con id che il server non può riconoscere non si riprende (es. creata con i vecchi dati di prova)
const draftIdsValid = (d) => [d.examId, d.classSubjectId, d.retakeOfExamId, ...(d.students || []).map(st => st.studentId)]
  .every(id => !id || isGuid(id));

// Scheda di un esame: bozze (non ancora pubblicati), attivi (pubblicati), storico (chiusi o annullati)
function tabOf(exam) {
  const st = fromEnum(exam.status, EXAM_STATUS);
  return st === "draft" ? "drafts" : st === "published" ? "active" : "history";
}

// Ordine dal più recente al più vecchio, per scheda: il primo campo presente decide.
// TODO(API): unico punto da cambiare quando ExamSummaryDto esporrà CreatedAt/UpdatedAt. Oggi le bozze
// ripiegano sulla data di inizio disponibilità.
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

// Bozza salvata in sessionStorage, se è ancora da riprendere: un esame salvato con modifiche, o un nuovo esame
// con contenuto. Le altre (lasciate dalle versioni precedenti, che salvavano anche le bozze vuote) si scartano.
function storedDraft() {
  const d = draftStore.get();
  if (d && draftIdsValid(d) && (d.examId || draftHasContent(d))) return d;
  draftStore.clear();
  return null;
}

const subjectNameOf = (cs) => cs?.subjectName || "";
const ACTION_OF = { Pubblica: "publish", Chiudi: "close", Annulla: "cancel", Elimina: "remove" };

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
  const [draft, setDraft] = useState(storedDraft);
  // dirty: c'è qualcosa da salvare sul server (regole in updateDraft); in locale si conserva solo allora
  const [dirty, setDirty] = useState(() => !!draft);
  const dirtyRef = useRef(dirty);
  const [savedAt, setSavedAt] = useState(null);
  const [serverSavedAt, setServerSavedAt] = useState(null); // ultimo "Salva bozza" riuscito sul server
  const [editorNotice, setEditorNotice] = useState(null); // banner dell'editor dopo un salvataggio non completo
  const editing = canWrite && (route.name === "new" || route.name === "edit");
  const [saving, setSaving] = useState(false);
  const [viewing, setViewing] = useState(null);
  const [tab, setTabState] = useState(() => (route.name === "list" && route.tab) || "active"); // "drafts" | "active" | "history"
  const [retakes, setRetakes] = useState(null); // dati della pagina /esami/recuperi: { loading, groups, failed }
  const [classId, setClassId] = useState(() => (route.name === "retakes" ? route.classId : null)); // filtro facoltativo: null = tutte le classi
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

  const loadFailed = useRef(false); // senza elenco non si può dire che un esame non esiste
  const load = useCallback(async () => {
    setLoading(true);
    loadFailed.current = false;
    try {
      const [list, cs] = await Promise.all([api.listExams(), api.getClassSubjects().catch(() => null)]);
      setExams(Array.isArray(list) ? list : []);
      setClassSubjects(cs);
    } catch (e) { loadFailed.current = true; notify("Errore", e.message); setExams([]); }
    finally { setLoading(false); }
  }, [api]);
  useEffect(() => { load(); }, [load]);

  const inClass = (e) => !classId || classSubjects?.find(c => c.id === e.classSubjectId)?.classId === classId;
  // Filtro classe e ricerca valgono per tutte e tre le schede: i contatori dicono cosa si vedrebbe aprendole.
  const filtered = filterBySearch(exams.filter(inClass), search, e => [e.name, e.description, csLabel(e.classSubjectId)]);

  // Bozza locale da riprendere: la card sta in cima alla scheda Bozze solo se c'è davvero qualcosa da
  // riprendere (dirty: un nuovo esame con contenuto, o un esame salvato con modifiche locali). Se riguarda un
  // esame già in lista, la voce resta ed è evidenziata ("Modifiche non salvate"), ma si conta una volta sola.
  const showResume = !!draft && !editing && dirty && inClass(draft);
  const resumeInList = showResume && !!draft.examId && filtered.some(e => e.id === draft.examId);
  const countByTab = { drafts: showResume && !resumeInList ? 1 : 0, active: 0, history: 0 };
  for (const e of filtered) countByTab[tabOf(e)]++;
  const visible = sortExams(filtered.filter(e => tabOf(e) === tab), tab);

  // Elenco di chi deve recuperare: raggruppa per studente le consegne non superate (bocciate o "non superabile")
  // su tutti gli esami pubblicati/chiusi della classe scelta. Richiede una chiamata per esame: va bene per i volumi di un registro di classe.
  function openRetakes() { nav.push({ name: "retakes", classId }); }
  useEffect(() => {
    if (route.name !== "retakes" || loading) return;
    let cancelled = false;
    const inRouteClass = (e) => !route.classId || classSubjects?.find(c => c.id === e.classSubjectId)?.classId === route.classId;
    setRetakes({ loading: true, groups: [], failed: 0 });
    getRetakeGroups(api, exams.filter(inRouteClass))
      .then((result) => { if (!cancelled) setRetakes({ loading: false, ...result }); })
      .catch(() => { if (!cancelled) setRetakes({ loading: false, groups: [], failed: 1 }); });
    return () => { cancelled = true; };
  }, [route.name, route.classId, loading, exams, classSubjects]);

  // dirty: la bozza ha modifiche non ancora inviate al server (false appena caricata da edit()).
  // savedAt: ora dell'ultimo salvataggio locale; solo su web, dove esiste sessionStorage.
  // Sull'app non c'è un archivio locale (AsyncStorage non è tra le dipendenze): lo stato lo dice.
  // keepDirty: la modifica non cambia lo stato rispetto al server (es. solo cambio di step).
  // Regole: un nuovo esame è "da salvare" solo se ha contenuto (draftHasContent: la sola scelta di classe e
  // materia non conta); un esame già salvato solo se è stato modificato. Si conserva in locale (e compare in
  // "Riprendi bozza") solo ciò che è da salvare: una bozza vuota o non modificata non lascia tracce.
  function updateDraft(d, { fromServer = false, keepDirty = false } = {}) {
    const nextDirty = !d ? false : !d.examId ? draftHasContent(d) : fromServer ? false : keepDirty ? dirtyRef.current : true;
    dirtyRef.current = nextDirty;
    setDraft(d);
    setDirty(nextDirty);
    nextDirty ? draftStore.set(d) : draftStore.clear();
    setSavedAt(nextDirty && Platform.OS === "web" ? new Date() : null);
  }
  const hhmm = (t) => `${String(t.getHours()).padStart(2, "0")}:${String(t.getMinutes()).padStart(2, "0")}`;
  // Stato mostrato nell'editor (testo intero su schermi larghi, badge corto su quelli stretti)
  const draftStatusShort = !draft ? null
    : dirty ? (Platform.OS === "web" ? { label: "Solo nel browser", tone: "amber" } : { label: "Non salvata", tone: "amber" })
    : draft.examId ? { label: "Salvata", tone: "green" } : { label: "Vuota", tone: "gray" };
  // Su web la bozza con modifiche si conserva da sola in sessionStorage; sull'app no (AsyncStorage non c'è).
  const unsavedText = Platform.OS === "web"
    ? `Salvata solo in questo browser${savedAt ? ` (${hhmm(savedAt)})` : ""} · non ancora sul server`
    : "Non salvata: le modifiche si perdono se esci";
  const draftStatus = !draft ? ""
    : dirty ? unsavedText
    : draft.examId ? (serverSavedAt ? `Salvata sul server alle ${hhmm(serverSavedAt)}` : "Salvata sul server")
    : "Bozza vuota: si conserva appena scrivi qualcosa";
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
  const backToExam = () => nav.back({ name: "view", id: route.id });
  // Uscita dall'editor: se non c'è niente da salvare la bozza locale si scarta (niente "Riprendi bozza")
  const exitEditor = () => { setEditorNotice(null); if (draft && !dirtyRef.current) updateDraft(null); backToList(); };
  const routeRef = useRef(route);
  routeRef.current = route;

  // "Crea esame" → /esami/nuovo. Classe e materia si scelgono una volta sola, prima dell'editor
  // (finestra sopra la lista); nel form restano fisse. Se il server non offre l'elenco delle
  // classi-materia, l'identificativo si inserisce nel form.
  function startNew() {
    if (draft && dirtyRef.current) return resume();
    if (draft) updateDraft(null);
    nav.push({ name: "new" });
  }
  // Uscendo dall'editor in qualsiasi modo (anche con l'indietro del browser) una bozza senza niente da
  // salvare non resta in memoria: niente card, e "Crea esame" ripropone la scelta di classe e materia.
  useEffect(() => {
    if (!editing && draft && !dirtyRef.current) updateDraft(null);
  }, [editing]);
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
        else openPicker(classId);
      }
      return;
    }
    if (route.name === "submissions" && !loading && !loadFailed.current && !exams.some(e => e.id === route.id)) {
      notify("Esame non trovato", examErrorMessage("load", { status: 404 }));
      nav.replace({ name: "list", tab });
      return;
    }
    if (route.name === "view" && viewing?.id !== route.id) {
      let cancelled = false;
      setBusy(route.id);
      api.getExam(route.id)
        .then((detail) => { if (!cancelled) setViewing(detail); })
        .catch((e) => {
          if (cancelled) return;
          notify(e.status === 404 ? "Esame non trovato" : "Errore", examErrorMessage("load", e));
          nav.replace({ name: "list", tab });
        })
        .finally(() => { if (!cancelled) setBusy(null); });
      return () => { cancelled = true; };
    }
  }, [route, loading]);

  // picking: { classId, classSubjectId, changing } — changing: si arriva da "Cambia" nel form
  const subjectsOf = (cid) => (classSubjects || []).filter(cs => cs.classId === cid);
  function openPicker(cid, { classSubjectId = "", changing = false } = {}) {
    const subjects = cid ? subjectsOf(cid) : [];
    setPicking({ classId: cid || null, classSubjectId: classSubjectId || (subjects.length === 1 ? subjects[0].id : ""), changing });
  }
  function pickClass(cid) {
    const subjects = subjectsOf(cid);
    setPicking(p => ({ ...p, classId: cid, classSubjectId: subjects.length === 1 ? subjects[0].id : "" }));
  }
  function confirmPick() {
    if (!picking?.classId || !picking?.classSubjectId) return;
    if (picking.changing && draft) {
      // Classe e materia nuove: studenti ed esame da recuperare dipendono da loro e si azzerano
      updateDraft({ ...draft, classSubjectId: picking.classSubjectId, students: [], retakeOfExamId: "", wantsRetake: false, retakeSeededFor: undefined });
    } else {
      updateDraft({ ...newTestDraft(), classSubjectId: picking.classSubjectId });
    }
    setPicking(null);
  }
  function cancelPick() {
    if (picking?.changing) return setPicking(null); // si torna al form
    setPicking(null);
    backToList();
  }
  // "Cambia" accanto a classe e materia nel form (solo esami nuovi): con domande già scritte chiede conferma
  function changeClassSubject() {
    const current = classSubjects?.find(c => c.id === draft?.classSubjectId);
    const go = () => openPicker(current?.classId || classId, { classSubjectId: draft?.classSubjectId, changing: true });
    const hasQuestions = (draft?.questions || []).some(q => q.text?.trim() || q.options?.some(o => o.text?.trim()));
    if (hasQuestions) {
      confirmAction("Cambiare classe e materia?", "Le domande restano; gli studenti assegnati e l'esame da recuperare verranno azzerati.", go, "Cambia");
    } else go();
  }
  // "Elimina bozza" (esame nuovo con contenuto): conferma, poi la bozza sparisce e si torna alla lista
  function discardDraft() {
    confirmAction("Eliminare la bozza?", "La bozza di questo nuovo esame verrà eliminata. Non si può annullare.",
      () => { updateDraft(null); backToList(); }, "Elimina bozza", { cancelLabel: "Continua a modificare" });
  }
  // "Annulla modifiche" (esame già salvato con modifiche locali): torna alla versione del server e resta nell'editor
  function revertDraft() {
    const examId = draft.examId;
    const step = draft.step;
    confirmAction("Annullare le modifiche?", "Torni alla versione salvata sul server: le modifiche fatte da allora andranno perse.", async () => {
      setSaving(true);
      try {
        const detail = await api.getExam(examId);
        updateDraft({ ...detailToDraft(detail), step }, { fromServer: true });
        setServerSavedAt(null);
        setEditorNotice(null); // tornati alla versione del server: niente da segnalare
      } catch (e) { notify("Errore", examErrorMessage("load", e)); }
      finally { setSaving(false); }
    }, "Annulla modifiche", { cancelLabel: "Continua a modificare", destructive: true });
  }
  const destroyAction = !draft || !dirty ? null
    : draft.examId ? { label: "Annulla modifiche", onPress: revertDraft }
    : { label: "Elimina bozza", onPress: discardDraft };

  // "Modifica" dal dettaglio: il dettaglio lascia il posto all'editor, così "indietro" torna alla lista
  function edit(exam) {
    if (draft && !draft.examId && dirty) {
      return notify("Bozza in corso", "Hai un nuovo esame non salvato: salvalo o eliminalo prima di modificarne un altro.");
    }
    nav.replace({ name: "edit", id: exam.id });
  }

  // Invia la bozza al server (crea o aggiorna). Restituisce l'id dell'esame.
  async function sendToServer(d) {
    const dto = draftToDto(d);
    if (d.examId) {
      await api.updateExam(d.examId, dto);
      await api.updateExamStudents(d.examId, { students: dto.assignedStudents });
      return d.examId;
    }
    const created = await api.createExam(dto); // il server restituisce l'id (Guid)
    return typeof created === "object" && created ? created.id : created;
  }

  // "Salva bozza": non fallisce mai per un esame incompleto. Se passa i controlli del server lo salva lì e
  // resta nell'editor sullo stesso step; altrimenti resta salvato solo nel browser (bozza locale).
  async function saveDraft() {
    if (!draft) return;
    if (!draft.examId && !draftHasContent(draft)) {
      return notify("Niente da salvare", "Scrivi almeno il titolo, una data o una domanda.");
    }
    if (serverIssue(draft)) {
      // Non passa i controlli del server: resta salvata solo nel browser. Banner informativo (non di errore)
      return setEditorNotice(Platform.OS === "web"
        ? { kind: "info", level: "server", title: "Salvata solo in questo browser. Per salvarla sul server servono ancora" }
        : { kind: "info", level: "server", title: "Non salvata: se esci, le modifiche si perdono. Per salvarla sul server servono ancora" });
    }
    setSaving(true);
    try {
      const examId = await sendToServer(draft);
      const isNew = !draft.examId;
      updateDraft({ ...draft, examId }, { fromServer: true });
      setServerSavedAt(new Date());
      setEditorNotice(null);
      if (isNew) nav.replace({ name: "edit", id: examId, step: draft.step ?? 1 }); // ora l'esame esiste: /esami/{id}/modifica
      load();
    } catch (e) { notify("Salvataggio non riuscito", examErrorMessage("save", e)); }
    finally { setSaving(false); }
  }

  // "Salva e chiudi" (step 3): validazione completa, salvataggio sul server, ritorno alla lista
  async function saveAndClose() {
    if (validateTestDraft(draft)) {
      return setEditorNotice({ kind: "error", level: "close", title: "Da sistemare prima di salvare e chiudere" });
    }
    setSaving(true);
    try {
      await sendToServer(draft);
      updateDraft(null); backToList();
      await load();
      notify("Esame salvato", "È in bozza: pubblicalo dall'elenco o dal dettaglio quando è pronto.");
    } catch (e) { notify("Salvataggio non riuscito", examErrorMessage("save", e)); }
    finally { setSaving(false); }
  }

  // Tocco sulla card → /esami/{id}; il dettaglio si carica nell'effetto delle rotte qui sopra
  function view(exam) { nav.push({ name: "view", id: exam.id }); }
  const viewed = route.name === "view" && viewing?.id === route.id ? viewing : null;

  // Azioni di stato: pubblica, chiudi, annulla, elimina
  // Dal dettaglio: dopo Elimina si torna alla lista, dopo gli altri cambi di stato il dettaglio si ricarica.
  function act(exam, label, message, run, okLabel) {
    confirmAction(label, message, async () => {
      setBusy(exam.id);
      try {
        await run();
        invalidateExamInsights(exam.id);
        await load();
        const r = routeRef.current;
        if (r.name === "view" && r.id === exam.id) {
          if (label === "Elimina") backToList();
          else setViewing(await api.getExam(exam.id));
        }
      }
      catch (e) {
        notify("Errore", examErrorMessage(ACTION_OF[label], e));
        // L'esame è cambiato o sparito nel frattempo: si riallinea quello che si vede
        if (e.status === 404 || e.status === 409) {
          invalidateExamInsights(exam.id);
          await load();
          if (e.status === 404 && routeRef.current.name === "view") backToList();
        }
      }
      finally { setBusy(null); }
    }, okLabel);
  }
  const publish = (e) => act(e, "Pubblica", `Pubblicare “${e.name}”? Gli studenti assegnati potranno svolgerlo e la struttura non sarà più modificabile.`, () => api.publishExam(e.id), "Pubblica");
  const close = (e) => act(e, "Chiudi", `Chiudere “${e.name}”? Nessuno studente potrà più iniziarlo.`, () => api.closeExam(e.id), "Chiudi");
  const cancel = (e) => act(e, "Annulla", `Annullare “${e.name}”? È possibile solo se nessuno lo ha ancora iniziato.`, () => api.cancelExam(e.id), "Annulla esame");
  const remove = (e) => act(e, "Elimina", `Eliminare “${e.name}”? Si può eliminare solo una bozza.`, () => api.deleteExam(e.id), "Elimina");

  // Azioni consentite su un esame, uguali per la card e per il dettaglio (che ha anche "Modifica")
  function actionsFor(exam, { detail = false } = {}) {
    const status = fromEnum(exam.status, EXAM_STATUS);
    return {
      edit: detail && canWrite && status === "draft" ? () => edit(exam) : null,
      submissions: canWrite && (status === "published" || status === "closed") ? () => nav.push({ name: "submissions", id: exam.id }) : null,
      close: canWrite && status === "published" ? () => close(exam) : null,
      publish: canWrite && status === "draft" ? () => publish(exam) : null,
      cancel: canDestroy && status === "published" ? () => cancel(exam) : null,
      remove: canDestroy && status === "draft" ? () => remove(exam) : null,
    };
  }

  // Titolo nell'header di AppNav ("← Esami / …") per dettaglio e scelta di classe e materia
  useEffect(() => {
    if (route.name === "view") setPageHeader({ title: viewing?.id === route.id ? viewing.name : "Esame", onBack: backToList });
    else if (route.name === "retakes") setPageHeader({ title: "Chi deve recuperare", onBack: backToList });
    else if (route.name === "submissions") setPageHeader({ title: "Consegne", onBack: backToExam });
    else if (route.name === "new" && picking) setPageHeader({ title: picking.changing ? "Cambia classe e materia" : "Nuovo esame", onBack: cancelPick });
    else if (!editing && route.name !== "review") setPageHeader(null); // editor e correzione gestiscono il proprio
  }, [route, picking, viewing, tab]);
  useEffect(() => () => setPageHeader(null), []);

  // Tasto indietro di Android sulle pagine della sezione (non più Modal): come "← Esami".
  // L'editor registra il suo (con la conferma) e viene chiamato prima di questo.
  useEffect(() => {
    if (Platform.OS !== "android" || route.name === "list") return;
    const sub = BackHandler.addEventListener("hardwareBackPress", () => {
      if (route.name === "new" && picking) { cancelPick(); return true; }
      if (route.name === "view" || route.name === "retakes") { backToList(); return true; }
      if (route.name === "submissions") { backToExam(); return true; }
      return false;
    });
    return () => sub.remove();
  }, [route, picking]);

  const env = {
    classSubjects: classSubjects && classId ? classSubjectsOfClass : classSubjects,
    classSubjectLabel: csLabel(draft?.classSubjectId),
    onChangeClassSubject: Array.isArray(classSubjects) && draft && !draft.examId ? changeClassSubject : null,
    retakeOptions: exams.filter(inClass).map(e => ({ id: e.id, name: e.name, classSubjectId: e.classSubjectId })),
    getStudents: (classSubjectId) => api.getStudentsOfClassSubject(classSubjectId),
    getRetakeCandidates: (examId) => getRetakeCandidates(api, examId),
  };

  const barBtn = { height: BAR_H, paddingVertical: 0, paddingHorizontal: 18 };
  const newBtn = canWrite && <Btn label="Crea esame" onPress={startNew} style={barBtn} textStyle={s.smBtnText} />;

  // Pagine della sezione, al posto della lista (sidebar e header di AppNav restano visibili)
  let page = null;
  if (route.name === "view") {
    page = viewed ? (
      <TestViewer exam={viewed} contextLabel={csLabel(viewed.classSubjectId)} onClose={backToList}
        busy={busy === viewed.id} actions={actionsFor(viewed, { detail: true })} />
    ) : <Loader />;
  } else if (route.name === "retakes") {
    page = (
      <ExamRetakes loading={retakes?.loading !== false} groups={retakes?.groups || []} failed={retakes?.failed || 0}
        labelOf={(e) => csLabel(e.classSubjectId) || e.name} hrefOf={(id) => examHref({ name: "view", id })}
        onOpenExam={(id) => nav.push({ name: "view", id })} />
    );
  } else if (route.name === "submissions") {
    const exam = exams.find(e => e.id === route.id);
    page = exam ? (
      <ExamSubmissions key={exam.id} exam={exam} subtitle={csLabel(exam.classSubjectId) || exam.name} api={api}
        hrefOf={(submissionId) => examHref({ name: "review", id: exam.id, submissionId })}
        onOpen={(submissionId) => nav.push({ name: "review", id: exam.id, submissionId })} />
    ) : <Loader />;
  } else if (route.name === "review") {
    page = (
      <SubmissionReview key={route.submissionId} submissionId={route.submissionId} api={api}
        onClose={() => nav.back({ name: "submissions", id: route.id })} onChanged={() => invalidateExamInsights(route.id)} />
    );
  } else if (route.name === "new" && picking) {
    page = (
      <ScrollView contentContainerStyle={{ padding: isDesktop ? 40 : 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
        <Card style={{ width: "100%", maxWidth: 640, padding: 24 }}>
          <Text style={{ color: C.text, fontSize: 20, fontWeight: "800" }}>Per quale classe e materia è l'esame?</Text>
          <Text style={[s.itemSub, { marginTop: 4, marginBottom: 16 }]}>
            Servono a scegliere gli studenti e l'esame da recuperare. Dopo il salvataggio non si potranno più cambiare.
          </Text>
          <SelectField label="Classe" value={picking.classId} options={classOptions} getLabel={c => c.name}
            onSelect={pickClass} placeholder="Es. SW-25A" emptyMessage="Nessuna classe disponibile" />
          <SelectField key={picking.classId || "no-class"} label="Materia" value={picking.classSubjectId}
            options={picking.classId ? subjectsOf(picking.classId) : []} getLabel={subjectNameOf}
            onSelect={(classSubjectId) => setPicking(p => ({ ...p, classSubjectId }))}
            placeholder={picking.classId ? "Es. Programmazione" : "Scegli prima la classe"}
            emptyMessage={picking.classId ? "Nessuna materia per questa classe" : "Scegli prima la classe"} />
          <View style={{ flexDirection: "row", justifyContent: "flex-end", gap: 8, marginTop: 4 }}>
            <Btn label="Annulla" variant="ghost" onPress={cancelPick} style={s.smBtn} textStyle={s.smBtnText} />
            <Btn label="Continua →" onPress={confirmPick} disabled={!picking.classId || !picking.classSubjectId}
              style={[s.smBtn, { paddingHorizontal: 20, paddingVertical: 10 }]} textStyle={s.smBtnText} />
          </View>
        </Card>
      </ScrollView>
    );
  } else if (editing) {
    page = (
      <ExamEditorScreen key={`${route.name}:${route.id || ""}`} route={route} nav={nav} api={api}
        draft={draft} dirty={dirty} updateDraft={updateDraft} env={env} draftStatus={draftStatus}
        draftStatusShort={draftStatusShort}
        contextLabel={draft?.examId ? "Modifica della bozza" : "Nuovo esame"}
        onSaveDraft={saveDraft} onSaveAndClose={saveAndClose} destroyAction={destroyAction} saving={saving} onExit={exitEditor}
        notice={editorNotice} onDismissNotice={() => setEditorNotice(null)} />
    );
  }
  if (page) {
    return (
      <View style={{ flex: 1, backgroundColor: C.bg }}>
        {page}
      </View>
    );
  }

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

        {showResume && tab === "drafts" && (
          // Bozza in corso, in cima alla scheda Bozze: sparisce quando viene salvata sul server o eliminata (updateDraft(null)).
          <Card style={{ marginBottom: 12, flexDirection: "row", alignItems: "center", gap: 12, flexWrap: "wrap", borderLeftWidth: 5, borderLeftColor: C.warning }}>
            <View style={{ width: 40, height: 40, borderRadius: 12, alignItems: "center", justifyContent: "center", backgroundColor: "#FEF3C7" }}>
              <Ionicons name="create-outline" size={20} color="#92400E" />
            </View>
            <View style={{ flex: 1, minWidth: 180 }}>
              <Text style={s.itemTitle} numberOfLines={1}>
                {draft.examId ? "Modifica in corso" : "Esame in preparazione"}: {draft.title.trim() || "Senza titolo"}
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
                onPress={() => view(exam)} href={examHref({ name: "view", id: exam.id })}
                unsaved={showResume && draft.examId === exam.id} actions={actionsFor(exam)} />
            );
          }}
        />
      )}
    </View>
  );
}
