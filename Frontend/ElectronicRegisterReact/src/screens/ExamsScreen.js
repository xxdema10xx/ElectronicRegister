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
import Btn from "../components/Btn";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import SectionHeader from "../components/SectionHeader";
import SelectField from "../components/SelectField";
import StatusChip, { EXAM_TONE } from "../components/StatusChip";
import TestBuilder, { newTestDraft, validateTestDraft } from "../components/TestBuilder";
import TestViewer from "../components/TestViewer";
import ExamSubmissions from "../components/ExamSubmissions";
import { filterBySearch } from "../utils/search";
import { notify, confirmAction } from "../utils/dialogs";
import { resetMockExams } from "../api/mockExamBackend";
import { EXAM_STATUS, EXAM_STATUS_LABEL, detailToDraft, draftToDto, fromEnum, formatDateTime } from "../api/examContract";
import { getRetakeGroups } from "../api/examInsights";

// La bozza in corso sopravvive al refresh (F5) su web grazie a sessionStorage.
const DRAFT_KEY = "examDraft4";
const draftStore = {
  get() { try { return Platform.OS === "web" ? JSON.parse(window.sessionStorage.getItem(DRAFT_KEY)) : null; } catch { return null; } },
  set(d) { try { if (Platform.OS === "web") window.sessionStorage.setItem(DRAFT_KEY, JSON.stringify(d)); } catch {} },
  clear() { try { if (Platform.OS === "web") window.sessionStorage.removeItem(DRAFT_KEY); } catch {} },
};

export default function ExamsScreen() {
  const { user } = useAuth();
  const role = user?.role?.toLowerCase();
  const canWrite = role === "admin" || role === "teacher";
  const { isDesktop } = useLayout();
  const api = useExamApi();

  const [exams, setExams] = useState([]);
  const [classSubjects, setClassSubjects] = useState(null); // null: il server non offre l'elenco
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState("");
  const [busy, setBusy] = useState(null); // id dell'esame su cui si sta operando

  const [draft, setDraft] = useState(() => draftStore.get());
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [viewing, setViewing] = useState(null);
  const [submissionsFor, setSubmissionsFor] = useState(null);
  const [tab, setTab] = useState("active"); // "active" | "history"
  const [retakes, setRetakes] = useState(null); // null: chiuso, altrimenti { loading, groups }
  const [classId, setClassId] = useState(null); // classe scelta per prima: un docente insegna in più classi

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

  const inHistory = (e) => { const st = fromEnum(e.status, EXAM_STATUS); return st === "closed" || st === "cancelled"; };
  const inClass = (e) => !classId || classSubjects?.find(c => c.id === e.classSubjectId)?.classId === classId;
  const scoped = exams.filter(e => (tab === "history") === inHistory(e) && inClass(e));
  const visible = filterBySearch(scoped, search, e => [e.name, e.description, csLabel(e.classSubjectId)]);

  // Elenco di chi deve recuperare: raggruppa per studente le consegne non superate (bocciate o "non superabile")
  // su tutti gli esami pubblicati/chiusi della classe scelta. Richiede una chiamata per esame: va bene per i volumi di un registro di classe.
  async function openRetakes() {
    setRetakes({ loading: true, groups: [] });
    try { setRetakes({ loading: false, groups: await getRetakeGroups(api, exams.filter(inClass)) }); }
    catch (e) { notify("Errore", e.message); setRetakes(null); }
  }

  function updateDraft(d) { setDraft(d); d ? draftStore.set(d) : draftStore.clear(); }
  function startNew() {
    if (classOptions.length > 0 && !classId) return notify("Seleziona una classe", "Scegli prima la classe per cui creare l'esame.");
    if (!draft) updateDraft(newTestDraft());
    setEditing(true);
  }
  function discardDraft() {
    confirmAction("Conferma", "Eliminare la bozza dell'esame?", () => { updateDraft(null); setEditing(false); }, "Elimina");
  }

  async function edit(exam) {
    if (draft && !draft.examId) {
      return notify("Bozza in corso", "Hai un nuovo esame non salvato: salvalo o eliminalo prima di modificarne un altro.");
    }
    setBusy(exam.id);
    try {
      const detail = await api.getExam(exam.id);
      updateDraft({ ...detailToDraft(detail), step: 1 });
      setEditing(true);
    } catch (e) { notify("Errore", e.message); }
    finally { setBusy(null); }
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
      updateDraft(null); setEditing(false);
      await load();
      notify("Esame salvato", "È in bozza: pubblicalo dall'elenco quando è pronto.");
    } catch (e) { notify("Errore", e.message); }
    finally { setSaving(false); }
  }

  async function view(exam) {
    setBusy(exam.id);
    try { setViewing(await api.getExam(exam.id)); }
    catch (e) { notify("Errore", e.message); }
    finally { setBusy(null); }
  }

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
    retakeOptions: exams.filter(inClass).map(e => ({ id: e.id, name: e.name, classSubjectId: e.classSubjectId })),
    getStudents: (classSubjectId) => api.getStudentsOfClassSubject(classSubjectId),
  };

  const needsClassFirst = classOptions.length > 0 && !classId;
  const newBtn = canWrite && (
    <Btn label="Crea esame" onPress={startNew} disabled={needsClassFirst}
      style={isDesktop ? { height: 48, paddingHorizontal: 24 } : s.smBtn} textStyle={isDesktop ? undefined : s.smBtnText} />
  );

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        {classOptions.length > 0 && (
          <SelectField label="Classe" value={classId} options={classOptions} getLabel={c => c.name}
            onSelect={setClassId} placeholder="Seleziona la classe su cui lavorare" emptyMessage="Nessuna classe disponibile" />
        )}
        {!needsClassFirst && (isDesktop ? (
          <View style={{ flexDirection: "row", alignItems: "center", gap: 12, marginTop: 8, marginBottom: 12 }}>
            <TextInput style={[s.input, { flex: 1 }]} placeholder="Cerca per nome…" placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
            {canWrite && <Btn label="Chi deve recuperare" variant="ghost" onPress={openRetakes} style={s.retakeBtn} textStyle={s.smBtnText} />}
            {newBtn}
          </View>
        ) : (<>
          <SectionHeader title="Esami" action={newBtn} />
          <TextInput style={[s.input, { marginBottom: 12 }]} placeholder="Cerca per nome…" placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
          {canWrite && <Btn label="Chi deve recuperare" variant="ghost" onPress={openRetakes} style={[s.retakeBtn, { marginBottom: 12, alignSelf: "flex-start" }]} textStyle={s.smBtnText} />}
        </>))}

        {!needsClassFirst && (
          <View style={{ flexDirection: "row", gap: 8, marginBottom: 12 }}>
            {[["active", "Attivi"], ["history", "Storico"]].map(([key, label]) => (
              <TouchableOpacity key={key} onPress={() => setTab(key)}
                style={{ paddingVertical: 7, paddingHorizontal: 16, borderRadius: 18, borderWidth: 1, borderColor: tab === key ? C.primary : C.border, backgroundColor: tab === key ? C.primary : C.white }}>
                <Text style={{ color: tab === key ? C.white : C.text, fontWeight: "600" }}>{label}</Text>
              </TouchableOpacity>
            ))}
          </View>
        )}

        {api.isMock() && (
          <View style={{ backgroundColor: "#FEF3C7", borderRadius: 10, padding: 10, marginBottom: 8, flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
            <Text style={{ color: "#92400E", flex: 1, minWidth: 200 }}>
              Modalità prova: il server non espone ancora gli esami (/Exam), i dati sono locali a questo browser.
            </Text>
            <TouchableOpacity onPress={() => { resetMockExams(); load(); }}><Text style={{ color: "#92400E", fontWeight: "700" }}>Ripristina dati</Text></TouchableOpacity>
          </View>
        )}
        {draft && !editing && (
          <TouchableOpacity onPress={() => setEditing(true)} style={{ marginBottom: 8 }}>
            <Text style={{ color: C.danger }}>Esame in preparazione: “{draft.title || "senza titolo"}” — tocca per riprenderlo</Text>
          </TouchableOpacity>
        )}
      </View>

      {needsClassFirst ? <EmptyState message="Seleziona prima una classe per vedere o creare i suoi esami" /> :
      loading ? <Loader /> : visible.length === 0 ? <EmptyState message={tab === "history" ? "Nessun esame nello storico" : "Nessun esame trovato"} /> : (
        <FlatList
          data={visible}
          keyExtractor={e => String(e.id)}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          renderItem={({ item: exam }) => {
            const status = fromEnum(exam.status, EXAM_STATUS);
            const working = busy === exam.id;
            const btn = (label, onPress, variant = "ghost") => (
              <Btn key={label} label={label} variant={variant} onPress={onPress} disabled={working} style={[s.smBtn, { paddingHorizontal: 14 }]} textStyle={s.smBtnText} />
            );
            return (
              <Card style={{ marginBottom: 10 }}>
                <View style={{ flexDirection: "row", alignItems: "center" }}>
                  <View style={[s.avatar, { backgroundColor: "#FDE68A" }]}><Ionicons name="document-text-outline" size={24} color="#92400E" /></View>
                  <View style={{ flex: 1, marginLeft: 12 }}>
                    <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                      <Text style={s.itemTitle}>{exam.name}</Text>
                      <StatusChip label={EXAM_STATUS_LABEL[status]} tone={EXAM_TONE[status]} />
                      {exam.isRetake && <StatusChip label="Recupero" tone="amber" />}
                    </View>
                    {!!(csLabel(exam.classSubjectId)) && <Text style={s.itemSub}>{csLabel(exam.classSubjectId)}</Text>}
                    <Text style={s.itemSub}>{formatDateTime(exam.availableFrom)} → {formatDateTime(exam.availableUntil)}</Text>
                    <Text style={s.itemSub}>
                      {exam.durationMinutes} min · soglia {exam.passingPercent}% · {exam.totalPoints} punti · {exam.assignedStudentsCount} studenti
                    </Text>
                  </View>
                </View>

                <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8, marginTop: 12 }}>
                  {btn("Vedi", () => view(exam))}
                  {canWrite && status === "draft" && btn("Modifica", () => edit(exam))}
                  {canWrite && status === "draft" && btn("Pubblica", () => publish(exam), "success")}
                  {canWrite && (status === "published" || status === "closed") && btn("Consegne", () => setSubmissionsFor(exam), "secondary")}
                  {canWrite && status === "published" && btn("Chiudi", () => close(exam))}
                  {canWrite && status === "published" && btn("Annulla", () => cancel(exam), "danger")}
                  {canWrite && status === "draft" && btn("Elimina", () => remove(exam), "danger")}
                </View>
              </Card>
            );
          }}
        />
      )}

      <TestViewer exam={viewing} contextLabel={csLabel(viewing?.classSubjectId)} onClose={() => setViewing(null)} />
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
      <TestBuilder visible={editing && !!draft} contextLabel={draft?.examId ? "Modifica della bozza" : "Nuovo esame"} draft={draft} env={env}
        onChange={updateDraft} onSave={save} saving={saving} onClose={() => setEditing(false)} onDiscard={draft?.examId ? () => { updateDraft(null); setEditing(false); } : discardDraft} />
    </View>
  );
}
