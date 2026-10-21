// src/screens/StudentExamsScreen.js
// Esami dello studente: elenco degli esami assegnati, avvio a schermo bloccato, ripresa dopo un refresh
// ed esito. Dati: ExamController (src/api/examApi.js) e stato locale di src/data/studentExams.js.
import { useEffect, useRef, useState } from "react";
import { FlatList, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useExamApi } from "../hooks/useExamApi";
import Card from "../components/Card";
import Btn from "../components/Btn";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import StatusChip from "../components/StatusChip";
import ExamTaker, { MAX_VIOLATIONS, enterFullscreen, exitFullscreen } from "../components/ExamTaker";
import { useStudentExams, markSeen, getRun, saveRun, notifyStudentExamsChanged, resetStudentDemo } from "../data/studentExams";
import { notify } from "../utils/dialogs";
import {
  EXAM_STATUS, SUBMISSION_STATUS, fromEnum, formatDateTime, parseUtc, resultView, studentQuestionFromDto, isAnswered,
} from "../api/examContract";

const fmt = (n) => (n === null || n === undefined ? "—" : String(Math.round(n * 100) / 100));

// Stato di un esame per lo studente: etichetta, colore e cosa succede toccandolo
function stateOf(a) {
  const now = Date.now();
  const sub = fromEnum(a.submissionStatus, SUBMISSION_STATUS);
  const st = fromEnum(a.status, EXAM_STATUS);
  if (sub === "in_progress") {
    if (a.deadlineAt && parseUtc(a.deadlineAt).getTime() <= now) return { label: "Scaduto", tone: "red", action: null };
    const run = getRun(a.id);
    return { label: "In corso", tone: "amber", action: run && !run.finished && run.exam ? "resume" : null };
  }
  if (sub === "pending_review") return { label: "In correzione", tone: "amber", action: "result" };
  if (sub === "failed_auto") return { label: "Non superato", tone: "red", action: "result" };
  if (sub) return { label: "Valutato", tone: "green", action: "result" };
  if (a.canStart) return { label: "Da svolgere", tone: "blue", action: "intro" };
  if (st === "cancelled") return { label: "Annullato", tone: "red", action: null };
  if (parseUtc(a.availableFrom).getTime() > now) return { label: `Dal ${formatDateTime(a.availableFrom)}`, tone: "gray", action: null };
  return { label: "Non più disponibile", tone: "gray", action: null };
}

export default function StudentExamsScreen() {
  const api = useExamApi();
  const { assigned, loaded, error, reload, isSeen } = useStudentExams();
  const [intro, setIntro] = useState(null); // esame scelto, prima dell'inizio
  const [starting, setStarting] = useState(false);
  const [taking, setTaking] = useState(null); // consegna in svolgimento (schermo bloccato)
  const [view, setView] = useState(null); // esito: { item, result, meta, loading }
  const [tab, setTab] = useState("list"); // "list" | "booklet"

  // Gli esami nuovi si evidenziano in questa visita; entrando nella sezione il pallino sparisce
  const newOnEntry = useRef(null);
  useEffect(() => {
    if (!loaded || newOnEntry.current) return;
    newOnEntry.current = new Set(assigned.filter(a => a.canStart && !isSeen(a.id)).map(a => a.id));
    markSeen(assigned.map(a => a.id));
  }, [loaded, assigned]);

  async function start(item) {
    enterFullscreen(); // va chiamato direttamente dal click, altrimenti il browser lo rifiuta
    setStarting(true);
    try {
      const data = await api.startExam(item.id);
      const run = {
        examId: item.id, submissionId: data.submissionId, startedAt: data.startedAt, deadlineAt: data.deadlineAt,
        exam: { name: data.exam.name, description: data.exam.description, questions: [...data.exam.questions].sort((a, b) => a.position - b.position).map(studentQuestionFromDto) },
        answers: {}, revision: 0, finished: false,
      };
      saveRun(item.id, run);
      setIntro(null);
      setTaking(run);
      notifyStudentExamsChanged();
    } catch (e) {
      exitFullscreen();
      notify("Impossibile iniziare", e.message);
      reload();
    } finally { setStarting(false); }
  }

  function resume(item) {
    const run = getRun(item.id);
    enterFullscreen();
    setTaking(run);
  }

  function finish(run, outcome) {
    setTaking(null);
    if (outcome.error) {
      notify("Consegna non riuscita", `${outcome.error} L'esame verrà chiuso dal server alla scadenza.`);
    } else {
      const meta = { auto: outcome.auto, reason: outcome.reason, violations: outcome.violations };
      saveRun(run.examId, { finished: true, result: outcome.result, meta });
      setView({ item: assigned.find(a => a.id === run.examId) || { id: run.examId, name: run.exam.name }, result: outcome.result, meta });
    }
    notifyStudentExamsChanged();
  }

  async function openResult(item) {
    const run = getRun(item.id);
    if (run?.result) return setView({ item, result: run.result, meta: run.meta, loading: !!run.submissionId });
    if (!run?.submissionId) {
      // il server non indica l'id della consegna: si mostra solo lo stato
      return setView({ item, result: { status: item.submissionStatus, submittedAt: item.submittedAt }, meta: null });
    }
    setView({ item, result: { status: item.submissionStatus, submittedAt: item.submittedAt }, loading: true });
    try {
      const result = await api.getResult(run.submissionId);
      saveRun(item.id, { result });
      setView({ item, result, meta: run.meta });
    } catch (e) { setView({ item, result: { status: item.submissionStatus, submittedAt: item.submittedAt }, meta: run.meta }); }
  }

  function press(item) {
    const st = stateOf(item);
    if (st.action === "intro") setIntro(item);
    else if (st.action === "resume") resume(item);
    else if (st.action === "result") openResult(item);
  }

  // ── Esito ──────────────────────────────────────────────────────────────────────────────────
  if (view) {
    const r = resultView(view.result);
    const done = r.status === "graded" || r.status === "finalized";
    const bad = r.status === "failed_auto" || (done && r.passed === false);
    const icon = r.status === "pending_review" ? ["hourglass-outline", "#D97706"] : bad ? ["close-circle", C.danger] : ["checkmark-circle", C.success];
    return (
      <View style={{ flex: 1, backgroundColor: C.bg, alignItems: "center", justifyContent: "center", padding: 24 }}>
        <Ionicons name={icon[0]} size={88} color={icon[1]} />
        <Text style={{ fontSize: 24, fontWeight: "700", color: C.text, marginTop: 12, textAlign: "center" }}>
          {view.meta ? "Esame consegnato" : r.title}
        </Text>
        <Text style={{ color: C.textMuted, marginTop: 4, textAlign: "center" }}>{view.item?.name}</Text>
        {view.loading && <Loader />}
        <Card style={{ marginTop: 20, width: "100%", maxWidth: 420 }}>
          {view.meta && <Text style={[s.itemTitle, { marginBottom: 6 }]}>{r.title}</Text>}
          {!!r.detail && <Text style={[s.itemSub, { marginBottom: 6 }]}>{r.detail}</Text>}
          {view.result?.submittedAt && <Text style={s.itemSub}>Consegnato il {formatDateTime(view.result.submittedAt)}</Text>}
          {done && r.totalScore !== null && <Text style={s.itemSub}>Punteggio: {fmt(r.totalScore)} / {fmt(r.maxScore)}</Text>}
          {r.showGrade && <Text style={{ fontSize: 22, fontWeight: "800", color: bad ? C.danger : C.success, marginTop: 8 }}>Voto: {fmt(r.grade)}</Text>}
          {view.meta?.auto && <Text style={[s.itemSub, { color: C.danger, marginTop: 6 }]}>Consegna automatica: {view.meta.reason}</Text>}
          {view.meta?.violations > 0 && <Text style={s.itemSub}>Uscite dall'esame registrate: {view.meta.violations}</Text>}
        </Card>
        <Btn label="Torna agli esami" onPress={() => { setView(null); reload(); }} style={{ marginTop: 20, minWidth: 220 }} />
      </View>
    );
  }

  // ── Regole prima dell'inizio ───────────────────────────────────────────────────────────────
  if (intro) {
    const total = intro.durationMinutes + (intro.extraTimeMinutes || 0);
    return (
      <View style={{ flex: 1, backgroundColor: C.bg, padding: 16, alignItems: "center" }}>
        <View style={{ width: "100%", maxWidth: 560 }}>
          <TouchableOpacity onPress={() => setIntro(null)} style={{ flexDirection: "row", alignItems: "center", gap: 6, marginBottom: 12 }}>
            <Ionicons name="arrow-back" size={22} color={C.text} /><Text style={{ color: C.text }}>Indietro</Text>
          </TouchableOpacity>
          <Card>
            <Text style={{ fontSize: 22, fontWeight: "700", color: C.text }}>{intro.name}</Text>
            {!!intro.description && <Text style={{ color: C.text, marginTop: 10 }}>{intro.description}</Text>}
            <Text style={{ color: C.text, marginTop: 10 }}>
              {intro.durationMinutes} minuti{intro.extraTimeMinutes ? ` (+${intro.extraTimeMinutes} di tempo aggiuntivo)` : ""} · disponibile fino al {formatDateTime(intro.availableUntil)}
            </Text>

            <View style={{ marginTop: 16, backgroundColor: "#FEF3C7", borderRadius: 10, padding: 12 }}>
              <Text style={{ color: "#92400E", fontWeight: "700", marginBottom: 6 }}>Prima di iniziare</Text>
              <Text style={{ color: "#92400E" }}>• Hai {total} minuti dal momento in cui premi “Inizia”; il tempo non si ferma.</Text>
              <Text style={{ color: "#92400E" }}>• Lo schermo verrà bloccato: potrai fare solo l'esame.</Text>
              <Text style={{ color: "#92400E" }}>• Non uscire dallo schermo intero, non cambiare scheda o finestra.</Text>
              <Text style={{ color: "#92400E" }}>• Copia e incolla sono disattivati.</Text>
              <Text style={{ color: "#92400E" }}>• Alla {MAX_VIOLATIONS}ª uscita, o allo scadere del tempo, l'esame viene consegnato automaticamente.</Text>
              <Text style={{ color: "#92400E" }}>• Puoi svolgerlo una sola volta.</Text>
            </View>
            <Btn label="Inizia l'esame" onPress={() => start(intro)} loading={starting} style={{ marginTop: 16 }} />
          </Card>
        </View>
      </View>
    );
  }

  // ── Libretto: un esame per riga, voto/esito a destra, più recenti in alto ────────────────────
  const booklet = assigned
    .filter(a => !!a.submissionStatus)
    .map(a => {
      const run = getRun(a.id);
      const r = resultView(run?.result || { status: a.submissionStatus });
      return { item: a, r, when: a.submittedAt || a.availableFrom };
    })
    .sort((x, y) => new Date(y.when) - new Date(x.when));

  // ── Elenco ─────────────────────────────────────────────────────────────────────────────────
  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <Text style={{ fontSize: 20, fontWeight: "700", color: C.text, marginBottom: 12 }}>I miei esami</Text>
        {api.isMock() && (
          <View style={{ backgroundColor: "#FEF3C7", borderRadius: 10, padding: 10, marginBottom: 8 }}>
            <Text style={{ color: "#92400E" }}>Modalità prova: il server non espone ancora gli esami (/Exam), i dati sono locali a questo browser.</Text>
          </View>
        )}
        {!!error && <Text style={{ color: C.danger, marginBottom: 8 }}>{error}</Text>}
        <View style={{ flexDirection: "row", gap: 8, marginBottom: 12 }}>
          {[["list", "Esami"], ["booklet", "Libretto"]].map(([key, label]) => (
            <TouchableOpacity key={key} onPress={() => setTab(key)}
              style={{ paddingVertical: 7, paddingHorizontal: 16, borderRadius: 18, borderWidth: 1, borderColor: tab === key ? C.primary : C.border, backgroundColor: tab === key ? C.primary : C.white }}>
              <Text style={{ color: tab === key ? C.white : C.text, fontWeight: "600" }}>{label}</Text>
            </TouchableOpacity>
          ))}
        </View>
      </View>

      {tab === "booklet" ? (
        !loaded ? <Loader /> : booklet.length === 0 ? <EmptyState message="Nessun esame svolto" /> : (
          <FlatList
            data={booklet}
            keyExtractor={x => String(x.item.id)}
            contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
            renderItem={({ item: { item, r } }) => (
              <TouchableOpacity activeOpacity={0.7} onPress={() => openResult(item)}>
                <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
                  <View style={{ flex: 1 }}>
                    <Text style={s.itemTitle}>{item.name}{item.isRetake ? " · Recupero" : ""}</Text>
                    <Text style={s.itemSub}>{formatDateTime(item.submittedAt) || formatDateTime(item.availableFrom)}</Text>
                  </View>
                  {r.showGrade ? (
                    <Text style={{ fontSize: 20, fontWeight: "800", color: r.passed === false ? C.danger : C.success }}>{fmt(r.grade)}</Text>
                  ) : (
                    <StatusChip label={r.title} tone={r.status === "pending_review" ? "amber" : r.status === "failed_auto" ? "red" : "gray"} />
                  )}
                </Card>
              </TouchableOpacity>
            )}
          />
        )
      ) : (
        !loaded ? <Loader /> : assigned.length === 0 ? <EmptyState message="Nessun esame assegnato" /> : (
          <FlatList
            data={assigned}
            keyExtractor={a => String(a.id)}
            contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
            ListFooterComponent={api.isMock() ? (
              <TouchableOpacity onPress={() => { resetStudentDemo(); reload(); }} style={{ marginTop: 8, alignItems: "center" }}>
                <Text style={{ color: C.textLight, fontSize: 12 }}>Ripristina la prova (solo demo)</Text>
              </TouchableOpacity>
            ) : null}
            renderItem={({ item }) => {
              const st = stateOf(item);
              const isNew = newOnEntry.current?.has(item.id) && item.canStart;
              return (
                <TouchableOpacity activeOpacity={st.action && st.action !== "resume" ? 0.7 : 1} onPress={() => st.action !== "resume" && press(item)}>
                  <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
                    <View style={[s.avatar, { backgroundColor: st.tone === "green" ? "#D1FAE5" : "#FDE68A" }]}>
                      <Ionicons name={st.tone === "green" ? "checkmark" : "document-text-outline"} size={24} color={st.tone === "green" ? "#065F46" : "#92400E"} />
                    </View>
                    <View style={{ flex: 1, marginLeft: 12 }}>
                      <Text style={s.itemTitle}>{item.name}{item.isRetake ? " · Recupero" : ""}</Text>
                      <Text style={s.itemSub}>Fino al {formatDateTime(item.availableUntil)}</Text>
                      <Text style={s.itemSub}>{item.durationMinutes}{item.extraTimeMinutes ? ` +${item.extraTimeMinutes}` : ""} min</Text>
                    </View>
                    {st.action === "resume" ? (
                      <Btn label="Riprendi l'esame" onPress={() => press(item)} style={{ paddingHorizontal: 16, paddingVertical: 10 }} />
                    ) : (
                      <View style={{ alignItems: "flex-end", gap: 4 }}>
                        {isNew && <Text style={{ backgroundColor: C.danger, color: C.white, fontSize: 11, fontWeight: "700", paddingHorizontal: 8, paddingVertical: 2, borderRadius: 10, overflow: "hidden" }}>NUOVO</Text>}
                        <StatusChip label={st.label} tone={st.tone} />
                      </View>
                    )}
                  </Card>
                </TouchableOpacity>
              );
            }}
          />
        )
      )}

      {taking && <ExamTaker run={taking} api={api} onFinish={(outcome) => finish(taking, outcome)} />}
    </View>
  );
}
