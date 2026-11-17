// src/screens/StudentExamsScreen.js
// Esami dello studente: elenco degli esami assegnati, avvio a schermo bloccato, ripresa dopo un refresh
// ed esito. Dati: ExamController (src/api/examApi.js) e stato locale di src/data/studentExams.js.
import { useEffect, useRef, useState } from "react";
import { FlatList, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useExamApi } from "../hooks/useExamApi";
import Card from "../components/Card";
import ListRow, { listProps } from "../components/ListRow";
import Btn from "../components/Btn";
import Loader from "../components/Loader";
import EmptyState from "../components/EmptyState";
import StatusChip from "../components/StatusChip";
import ExamTaker, { MAX_VIOLATIONS, enterFullscreen, exitFullscreen } from "../components/ExamTaker";
import { useStudentExams, markSeen, getRun, saveRun, notifyStudentExamsChanged } from "../data/studentExams";
// Svolgimento: avvisi del browser come prima (non il dialog dell'app), per non cambiare focus, schermo intero e violazioni
import { notifyBrowser as notify } from "../utils/dialogs";
import { examErrorMessage } from "../utils/examErrors";
import {
  EXAM_STATUS, SUBMISSION_STATUS, examDisplayName, fromEnum, formatDate, formatDateTime, parseUtc, resultView, studentQuestionFromDto, isAnswered,
} from "../api/examContract";

const fmt = (n) => (n === null || n === undefined ? "—" : String(Math.round(n * 100) / 100));

// Stato di un esame per lo studente: etichetta, colore e cosa succede toccandolo
function stateOf(a) {
  const now = Date.now();
  const sub = fromEnum(a.submissionStatus, SUBMISSION_STATUS);
  const st = fromEnum(a.status, EXAM_STATUS);
  if (sub === "in_progress") {
    // Scaduto ma non ancora consegnato: si consegnano le risposte salvate (il server lo chiude solo se il worker è attivo)
    if (a.deadlineAt && parseUtc(a.deadlineAt).getTime() <= now) return { label: "Scaduto: tocca per consegnare", tone: "red", action: "expire" };
    // Si riprende sempre dalla sessione sul server (fonte autorevole per risposte, revisione e scadenza)
    return { label: "In corso", tone: "amber", action: a.submissionId || getRun(a.id)?.submissionId ? "resume" : null };
  }
  if (sub === "pending_review") return { label: "In correzione", tone: "amber", action: "result" };
  if (sub === "failed_auto") return { label: "In attesa di valutazione", tone: "amber", action: "result" }; // non è un esito
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
  const [resumeError, setResumeError] = useState(null); // { item, message }: sessione non leggibile, si può riprovare

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
      notify("Impossibile iniziare", examErrorMessage("start", e));
      reload();
    } finally { setStarting(false); }
  }

  // Ripresa: SEMPRE dalla sessione sul server (risposte, currentRevision, scadenza). La copia locale è solo
  // una cache e non sostituisce mai ciò che arriva dal server; se la sessione non si legge si mostra un
  // errore con "Riprova", non la copia locale. Già consegnata o scaduta → esito invece dello svolgimento.
  // L'ordine delle domande arriva già personalizzato dal server (position riscritta): si ordina per position.
  async function resume(item) {
    enterFullscreen(); // va chiamato direttamente dal click, prima di qualunque attesa
    setResumeError(null);
    setStarting(true);
    try {
      const submissionId = item.submissionId || getRun(item.id)?.submissionId;
      const session = await api.getSession(submissionId);
      const status = fromEnum(session.status, SUBMISSION_STATUS);
      if (status && status !== "in_progress") {
        exitFullscreen();
        return openResult({ ...item, submissionId: session.submissionId || submissionId, submissionStatus: session.status, submittedAt: session.submittedAt });
      }
      if (session.deadlineAt && parseUtc(session.deadlineAt).getTime() <= Date.now()) {
        exitFullscreen();
        return expire({ ...item, submissionId: session.submissionId || submissionId });
      }
      const questions = [...session.exam.questions].sort((a, b) => a.position - b.position).map(studentQuestionFromDto);
      // Risposte salvate sul server → forma dello svolgimento ({ [questionId]: optionId | [optionId] | testo })
      const answers = {};
      for (const a of session.answers || []) {
        const q = questions.find(x => x.id === a.questionId);
        if (!q) continue;
        answers[q.id] = q.type === "open" ? (a.openText || "") : q.type === "multiple" ? (a.selectedOptionIds || []) : a.selectedOptionIds?.[0];
      }
      const run = {
        examId: item.id, submissionId: session.submissionId, startedAt: session.startedAt, deadlineAt: session.deadlineAt,
        exam: { name: session.exam.name, description: session.exam.description, questions },
        answers, revision: session.currentRevision || 0, finished: false,
      };
      saveRun(item.id, run);
      setTaking(run);
    } catch (e) {
      exitFullscreen();
      setResumeError({ item, message: examErrorMessage("load", e) });
    } finally { setStarting(false); }
  }

  // Consegna di un esame scaduto con le risposte già salvate sul server
  async function expire(item) {
    try {
      const result = await api.submitExpired(item.submissionId);
      saveRun(item.id, { finished: true, result, meta: { auto: true, reason: "tempo scaduto" } });
      setView({ item, result, meta: { auto: true, reason: "tempo scaduto" } });
    } catch (e) { notify("Consegna non riuscita", e.message); }
    finally { reload(); notifyStudentExamsChanged(); }
  }

  function finish(run, outcome) {
    setTaking(null);
    if (outcome.error) {
      notify("Consegna non riuscita", `${outcome.error} L'esame verrà chiuso dal server alla scadenza.`);
    } else {
      const meta = { auto: outcome.auto, reason: outcome.reason, violations: outcome.violations, notice: outcome.notice || null };
      saveRun(run.examId, { finished: true, result: outcome.result, meta });
      const item = assigned.find(a => a.id === run.examId) || { id: run.examId, name: run.exam.name };
      setView({ item, result: outcome.result || { status: null }, meta });
    }
    notifyStudentExamsChanged();
  }

  async function openResult(item) {
    const run = getRun(item.id);
    const submissionId = item.submissionId || run?.submissionId;
    if (!submissionId) {
      return setView({ item, result: run?.result || { status: item.submissionStatus, submittedAt: item.submittedAt }, meta: run?.meta || null });
    }
    // Si mostra subito l'ultimo esito noto, poi si aggiorna dal server (il voto può arrivare dopo la correzione)
    setView({ item, result: run?.result || { status: item.submissionStatus, submittedAt: item.submittedAt }, meta: run?.meta, loading: true });
    try {
      const result = await api.getResult(submissionId);
      saveRun(item.id, { submissionId, result });
      setView({ item, result, meta: run?.meta });
    } catch (e) { setView({ item, result: run?.result || { status: item.submissionStatus, submittedAt: item.submittedAt }, meta: run?.meta }); }
  }

  function press(item) {
    const st = stateOf(item);
    if (st.action === "intro") setIntro(item);
    else if (st.action === "resume") resume(item);
    else if (st.action === "expire") expire(item);
    else if (st.action === "result") openResult(item);
  }

  // ── Esito ──────────────────────────────────────────────────────────────────────────────────
  if (view) {
    const r = resultView(view.result);
    const done = r.status === "graded" || r.status === "finalized";
    const bad = done && r.passed === false;
    const waiting = r.status === "pending_review" || r.status === "failed_auto";
    const icon = waiting ? ["hourglass-outline", "#D97706"] : bad ? ["close-circle", C.danger]
      : done && r.passed === null ? ["help-circle-outline", C.textMuted] : ["checkmark-circle", C.success];
    return (
      <View style={{ flex: 1, backgroundColor: C.bg, alignItems: "center", justifyContent: "center", padding: 24 }}>
        <Ionicons name={icon[0]} size={88} color={icon[1]} />
        <Text style={{ fontSize: 24, fontWeight: "700", color: C.text, marginTop: 12, textAlign: "center" }}>
          {view.meta ? "Esame consegnato" : r.title}
        </Text>
        <Text style={{ color: C.textMuted, marginTop: 4, textAlign: "center" }}>{examDisplayName(view.item)}</Text>
        {view.loading && <Loader />}
        <Card style={{ marginTop: 20, width: "100%", maxWidth: 420 }}>
          {view.meta && <Text style={[s.itemTitle, { marginBottom: 6 }]}>{r.title}</Text>}
          {!!r.detail && <Text style={[s.itemSub, { marginBottom: 6 }]}>{r.detail}</Text>}
          {view.result?.submittedAt && <Text style={s.itemSub}>Consegnato il {formatDateTime(view.result.submittedAt)}</Text>}
          {done && r.totalScore !== null && <Text style={s.itemSub}>Punteggio: {fmt(r.totalScore)} / {fmt(r.maxScore)}</Text>}
          {r.showGrade && <Text style={{ fontSize: 22, fontWeight: "800", color: bad ? C.danger : C.success, marginTop: 8 }}>Voto: {fmt(r.grade)}</Text>}
          {view.meta?.notice
            ? <Text style={[s.itemSub, { color: C.danger, marginTop: 6 }]}>{view.meta.notice}</Text>
            : view.meta?.auto && <Text style={[s.itemSub, { color: C.danger, marginTop: 6 }]}>Consegna automatica: {view.meta.reason}</Text>}
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
            <Text style={{ fontSize: 22, fontWeight: "700", color: C.text }}>{examDisplayName(intro)}</Text>
            {!!intro.description && <Text style={{ color: C.text, marginTop: 10 }}>{intro.description}</Text>}
            <Text style={{ color: C.text, marginTop: 10 }}>
              {intro.durationMinutes} minuti{intro.extraTimeMinutes ? ` (+${intro.extraTimeMinutes} di tempo aggiuntivo)` : ""} · disponibile dal {formatDate(intro.availableFrom)} fino al {formatDateTime(intro.availableUntil)}
            </Text>

            <View style={{ marginTop: 16, backgroundColor: "#FEF3C7", borderRadius: 10, padding: 12 }}>
              <Text style={{ color: "#92400E", fontWeight: "700", marginBottom: 6 }}>Prima di iniziare</Text>
              <Text style={{ color: "#92400E" }}>• Hai {total} minuti dal momento in cui premi “Inizia”; il tempo non si ferma.</Text>
              <Text style={{ color: "#92400E" }}>• Lo schermo verrà bloccato: potrai fare solo l'esame.</Text>
              <Text style={{ color: "#92400E" }}>• Non uscire dallo schermo intero, non cambiare scheda o finestra.</Text>
              <Text style={{ color: "#92400E" }}>• Copia e incolla sono disattivati.</Text>
              <Text style={{ color: "#92400E" }}>• Alla {MAX_VIOLATIONS}ª uscita sei escluso dall'esame e vengono consegnate le risposte già salvate.</Text>
              <Text style={{ color: "#92400E" }}>• Allo scadere del tempo l'esame viene consegnato automaticamente.</Text>
              <Text style={{ color: "#92400E" }}>• Puoi svolgerlo una sola volta.</Text>
            </View>
            <Btn label="Inizia l'esame" onPress={() => start(intro)} loading={starting} style={{ marginTop: 16 }} />
          </Card>
        </View>
      </View>
    );
  }

  // ── Elenco ─────────────────────────────────────────────────────────────────────────────────
  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <Text style={{ fontSize: 20, fontWeight: "700", color: C.text, marginBottom: 12 }}>I miei esami</Text>
        {!!error && <Text style={{ color: C.danger, marginBottom: 8 }}>{error}</Text>}
        {resumeError && (
          <Card style={{ marginBottom: 12, borderLeftWidth: 5, borderLeftColor: C.danger }}>
            <Text style={s.itemTitle}>Impossibile riprendere {examDisplayName(resumeError.item)}</Text>
            <Text style={[s.itemSub, { marginTop: 4 }]}>{resumeError.message}</Text>
            <View style={{ flexDirection: "row", gap: 8, marginTop: 10 }}>
              <Btn label="Riprova" onPress={() => resume(resumeError.item)} loading={starting} style={s.smBtn} textStyle={s.smBtnText} />
              <Btn label="Chiudi" variant="ghost" onPress={() => setResumeError(null)} style={s.smBtn} textStyle={s.smBtnText} />
            </View>
          </Card>
        )}
      </View>

      {
        !loaded ? <Loader /> : assigned.length === 0 ? <EmptyState message="Nessun esame assegnato" /> : (
          <FlatList
            data={assigned}
            keyExtractor={a => String(a.id)}
            {...listProps()}
            renderItem={({ item }) => {
              const st = stateOf(item);
              const isNew = newOnEntry.current?.has(item.id) && item.canStart;
              return (
                <ListRow onPress={st.action && st.action !== "resume" ? () => press(item) : undefined}>
                    <View style={[s.avatar, { backgroundColor: st.tone === "green" ? "#D1FAE5" : "#FDE68A" }]}>
                      <Ionicons name={st.tone === "green" ? "checkmark" : "document-text-outline"} size={24} color={st.tone === "green" ? "#065F46" : "#92400E"} />
                    </View>
                    <View style={{ flex: 1, marginLeft: 12 }}>
                      <Text style={s.itemTitle}>{examDisplayName(item)}{item.isRetake ? " · Recupero" : ""}</Text>
                      <Text style={s.itemSub}>Dal {formatDate(item.availableFrom)} · fino al {formatDateTime(item.availableUntil)}</Text>
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
                </ListRow>
              );
            }}
          />
        )
      }

      {taking && <ExamTaker run={taking} api={api} onFinish={(outcome) => finish(taking, outcome)} />}
    </View>
  );
}
