// src/components/ExamTaker.js
// Svolgimento dell'esame a schermo bloccato. Il blocco è "di deterrenza" e non a prova di manomissione:
// su web usa lo schermo intero, e uscire, cambiare scheda/finestra o perdere il focus conta come uscita;
// su nativo si controlla che l'app resti in primo piano. Alla terza uscita, o allo scadere del tempo,
// l'esame viene consegnato automaticamente. Le risposte si salvano da sole sul server; la scadenza è quella
// decisa dal server (deadlineAt), non un timer del dispositivo.
import { useCallback, useEffect, useRef, useState } from "react";
import { AppState, Modal, Platform, ScrollView, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Btn from "./Btn";
import Card from "./Card";
import { answersToDto, isAnswered, parseUtc } from "../api/examContract";
import { saveRun } from "../data/studentExams";

export const MAX_VIOLATIONS = 3;
const SAVE_DELAY_MS = 2500;
const isWeb = Platform.OS === "web";

export function enterFullscreen() {
  if (isWeb) return document.documentElement.requestFullscreen?.().catch(() => {});
}
export function exitFullscreen() {
  if (isWeb && document.fullscreenElement) document.exitFullscreen().catch(() => {});
}

const fmt = (sec) => `${String(Math.floor(sec / 60)).padStart(2, "0")}:${String(sec % 60).padStart(2, "0")}`;

// run: { examId, submissionId, deadlineAt, exam: { name, description, questions }, answers, revision }
export default function ExamTaker({ run, api, onFinish }) {
  const { exam } = run;
  const questions = exam.questions;
  const deadline = parseUtc(run.deadlineAt).getTime();
  const secondsLeft = () => Math.ceil((deadline - Date.now()) / 1000);

  const [answers, setAnswers] = useState(run.answers || {});
  const [remaining, setRemaining] = useState(secondsLeft);
  const [violations, setViolations] = useState(0);
  const [paused, setPaused] = useState(null); // messaggio mostrato mentre l'esame è in pausa per un'uscita
  const [confirming, setConfirming] = useState(false);
  const [missing, setMissing] = useState([]); // domande obbligatorie senza risposta
  const [saveState, setSaveState] = useState(null); // "saving" | "saved" | "error"
  const [submitting, setSubmitting] = useState(false);
  const [failure, setFailure] = useState(null); // consegna automatica non riuscita

  const finishedRef = useRef(false);
  const submittingRef = useRef(false);
  const pausedRef = useRef(false);
  const violationsRef = useRef(0);
  const answersRef = useRef(answers);
  answersRef.current = answers;
  const revisionRef = useRef(run.revision || 0);
  const firstRender = useRef(true);

  // ── Salvataggio ──
  const save = useCallback(async () => {
    if (finishedRef.current || submittingRef.current) return;
    const revision = revisionRef.current + 1;
    setSaveState("saving");
    try {
      const res = await api.saveAnswers(run.submissionId, { clientRevision: revision, answers: answersToDto(questions, answersRef.current) });
      revisionRef.current = Math.max(revision, res?.currentRevision ?? revision);
      saveRun(run.examId, { revision: revisionRef.current });
      setSaveState("saved");
    } catch { setSaveState("error"); }
  }, [api, questions, run.examId, run.submissionId]);

  // Ogni modifica si tiene subito sul dispositivo (ripresa dopo un refresh) e si invia al server dopo una breve pausa
  useEffect(() => {
    if (firstRender.current) { firstRender.current = false; return; }
    saveRun(run.examId, { answers });
    const id = setTimeout(save, SAVE_DELAY_MS);
    return () => clearTimeout(id);
  }, [answers, save, run.examId]);

  // ── Consegna ──
  const submit = useCallback(async (auto, reason) => {
    if (finishedRef.current || submittingRef.current) return;
    submittingRef.current = true;
    setSubmitting(true);
    setFailure(null);
    try {
      const result = await api.submitExam(run.submissionId, { clientRevision: revisionRef.current + 1, answers: answersToDto(questions, answersRef.current) });
      finishedRef.current = true;
      exitFullscreen();
      onFinish({ result, auto, reason, violations: violationsRef.current });
    } catch (e) {
      submittingRef.current = false;
      setSubmitting(false);
      if (auto) setFailure({ message: e.message, reason });
      else { setConfirming(false); setFailure({ message: e.message, reason: null }); }
    }
  }, [api, onFinish, questions, run.submissionId]);

  const registerViolation = useCallback((message) => {
    if (finishedRef.current || submittingRef.current || pausedRef.current) return;
    pausedRef.current = true;
    violationsRef.current += 1;
    setViolations(violationsRef.current);
    if (violationsRef.current >= MAX_VIOLATIONS) return submit(true, "troppe uscite dal test");
    setPaused(message);
  }, [submit]);

  function resume() {
    enterFullscreen();
    pausedRef.current = false;
    setPaused(null);
  }

  function askConfirm() {
    const notDone = questions.map((q, i) => (q.required && !isAnswered(q, answers[q.id]) ? i + 1 : null)).filter(Boolean);
    setMissing(notDone);
    if (notDone.length === 0) { setFailure(null); setConfirming(true); }
  }

  // Timer: segue la scadenza del server e continua anche durante la pausa
  useEffect(() => {
    const id = setInterval(() => setRemaining(secondsLeft()), 1000);
    return () => clearInterval(id);
  }, []);
  const expiredRef = useRef(false);
  useEffect(() => {
    if (remaining <= 0 && !expiredRef.current) { expiredRef.current = true; submit(true, "tempo scaduto"); }
  }, [remaining, submit]);

  // Controlli anti-uscita
  useEffect(() => {
    if (isWeb) {
      const onFullscreen = () => { if (!document.fullscreenElement) registerViolation("Sei uscito dalla modalità a schermo intero."); };
      const onVisibility = () => { if (document.visibilityState === "hidden") registerViolation("Hai cambiato scheda o finestra."); };
      const onBlur = () => registerViolation("La finestra dell'esame ha perso il focus.");
      const stop = (e) => e.preventDefault();
      const onKey = (e) => {
        const k = e.key.toLowerCase();
        if (k === "f5" || k === "f12" || ((e.ctrlKey || e.metaKey) && ["c", "v", "x", "p", "s", "u", "r", "f"].includes(k))) e.preventDefault();
      };
      const onUnload = (e) => { e.preventDefault(); e.returnValue = ""; };
      document.addEventListener("fullscreenchange", onFullscreen);
      document.addEventListener("visibilitychange", onVisibility);
      window.addEventListener("blur", onBlur);
      window.addEventListener("beforeunload", onUnload);
      document.addEventListener("keydown", onKey);
      ["contextmenu", "copy", "cut", "paste", "dragstart"].forEach(ev => document.addEventListener(ev, stop));
      return () => {
        document.removeEventListener("fullscreenchange", onFullscreen);
        document.removeEventListener("visibilitychange", onVisibility);
        window.removeEventListener("blur", onBlur);
        window.removeEventListener("beforeunload", onUnload);
        document.removeEventListener("keydown", onKey);
        ["contextmenu", "copy", "cut", "paste", "dragstart"].forEach(ev => document.removeEventListener(ev, stop));
      };
    }
    const sub = AppState.addEventListener("change", (st) => { if (st !== "active") registerViolation("Hai lasciato l'app durante l'esame."); });
    return () => sub.remove();
  }, [registerViolation]);

  const setAnswer = (qid, value) => { setMissing([]); setAnswers(a => ({ ...a, [qid]: value })); };
  const toggleMulti = (qid, oid) => { setMissing([]); setAnswers(a => {
    const cur = a[qid] || [];
    return { ...a, [qid]: cur.includes(oid) ? cur.filter(x => x !== oid) : [...cur, oid] };
  }); };
  const answered = questions.filter(q => isAnswered(q, answers[q.id])).length;

  return (
    // onRequestClose vuoto: il tasto indietro di Android non chiude l'esame
    <Modal visible animationType="none" statusBarTranslucent onRequestClose={() => {}}>
      <View style={{ flex: 1, backgroundColor: "#EEEBF9" }}>
        <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", backgroundColor: C.primary, paddingHorizontal: 16, paddingVertical: 12 }}>
          <View style={{ flex: 1 }}>
            <Text style={{ color: C.white, fontSize: 16, fontWeight: "700" }} numberOfLines={1}>{exam.name}</Text>
            <Text style={{ color: "#CBD5E1", fontSize: 12 }} numberOfLines={1}>
              {saveState === "saving" ? "Salvataggio…" : saveState === "saved" ? "Risposte salvate" : saveState === "error" ? "Salvataggio non riuscito: riproverò" : " "}
            </Text>
          </View>
          <View style={{ alignItems: "flex-end" }}>
            <Text style={{ color: remaining <= 60 ? "#FCA5A5" : C.white, fontSize: 20, fontWeight: "700" }}>{fmt(Math.max(remaining, 0))}</Text>
            <Text style={{ color: "#CBD5E1", fontSize: 11 }}>Uscite: {violations}/{MAX_VIOLATIONS}</Text>
          </View>
        </View>

        <ScrollView contentContainerStyle={{ padding: 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
          <View style={[{ width: "100%", maxWidth: 760 }, isWeb && { userSelect: "none" }]}>
            {!!exam.description && <Text style={{ color: C.textMuted, marginBottom: 12 }}>{exam.description}</Text>}

            {questions.map((q, i) => (
              <Card key={q.id} style={{ marginBottom: 12, borderLeftWidth: 5, borderLeftColor: missing.includes(i + 1) ? C.danger : C.primary }}>
                <View style={{ flexDirection: "row", justifyContent: "space-between", gap: 10 }}>
                  <Text style={{ flex: 1, color: C.text, fontSize: 15, fontWeight: "600" }}>{i + 1}. {q.text}{q.required ? " *" : ""}</Text>
                  <Text style={{ color: C.textMuted }}>{q.points} pt</Text>
                </View>
                <Text style={{ color: C.textLight, fontSize: 12, marginBottom: 6 }}>
                  {q.type === "single" ? "Una sola risposta" : q.type === "multiple" ? "Una o più risposte" : "Risposta scritta"}
                </Text>

                {q.type === "open" ? (
                  <TextInput style={[s.input, { minHeight: 90, textAlignVertical: "top" }]} multiline placeholder="Scrivi qui la tua risposta"
                    placeholderTextColor={C.textLight} value={answers[q.id] || ""} onChangeText={(v) => setAnswer(q.id, v)} />
                ) : q.options.map(o => {
                  const multi = q.type === "multiple";
                  const on = multi ? (answers[q.id] || []).includes(o.id) : answers[q.id] === o.id;
                  return (
                    <TouchableOpacity key={o.id} activeOpacity={0.7} onPress={() => multi ? toggleMulti(q.id, o.id) : setAnswer(q.id, o.id)}
                      style={{ flexDirection: "row", alignItems: "center", gap: 10, paddingVertical: 6 }}>
                      <Ionicons name={on ? (multi ? "checkbox" : "radio-button-on") : (multi ? "square-outline" : "radio-button-off")}
                        size={24} color={on ? C.primary : C.textLight} />
                      <Text style={{ flex: 1, color: C.text }}>{o.text}</Text>
                    </TouchableOpacity>
                  );
                })}
              </Card>
            ))}

            {missing.length > 0 && (
              <Text style={{ color: C.danger, marginBottom: 12 }}>
                Rispondi alle domande obbligatorie (*): {missing.join(", ")}.
              </Text>
            )}
            {failure && !failure.reason && <Text style={{ color: C.danger, marginBottom: 12 }}>{failure.message}</Text>}

            {confirming ? (
              <Card style={{ marginBottom: 32 }}>
                <Text style={{ color: C.text, fontWeight: "600", marginBottom: 4 }}>Consegnare l'esame?</Text>
                <Text style={{ color: C.textMuted, marginBottom: 12 }}>
                  Hai risposto a {answered} domande su {questions.length}. Dopo la consegna non potrai modificare le risposte.
                </Text>
                <View style={{ gap: 8 }}>
                  <Btn label="Sì, consegna" variant="success" onPress={() => submit(false, null)} loading={submitting} />
                  <Btn label="Torna all'esame" variant="ghost" onPress={() => setConfirming(false)} disabled={submitting} />
                </View>
              </Card>
            ) : (
              <Btn label={`Consegna (${answered}/${questions.length})`} variant="success" onPress={askConfirm} style={{ marginBottom: 32 }} />
            )}
          </View>
        </ScrollView>

        {/* Esame in pausa dopo un'uscita: non si vede né si fa nulla finché non si riprende */}
        {paused && (
          <View style={{ position: "absolute", top: 0, left: 0, right: 0, bottom: 0, backgroundColor: C.primary, alignItems: "center", justifyContent: "center", padding: 24, zIndex: 10 }}>
            <Ionicons name="warning-outline" size={56} color="#FCD34D" />
            <Text style={{ color: C.white, fontSize: 20, fontWeight: "700", marginTop: 12, textAlign: "center" }}>Esame in pausa</Text>
            <Text style={{ color: "#E2E8F0", marginTop: 8, textAlign: "center", maxWidth: 420 }}>{paused}</Text>
            <Text style={{ color: "#FCA5A5", marginTop: 8, textAlign: "center", maxWidth: 420 }}>
              Uscita {violations} di {MAX_VIOLATIONS}: alla {MAX_VIOLATIONS}ª l'esame viene consegnato automaticamente. Il tempo continua a scorrere.
            </Text>
            <Btn label="Riprendi l'esame" onPress={resume} style={{ marginTop: 20, minWidth: 220 }} />
          </View>
        )}

        {/* La consegna automatica non è andata a buon fine (rete, domande obbligatorie mancanti…) */}
        {failure?.reason && (
          <View style={{ position: "absolute", top: 0, left: 0, right: 0, bottom: 0, backgroundColor: C.primary, alignItems: "center", justifyContent: "center", padding: 24, zIndex: 20 }}>
            <Ionicons name="alert-circle-outline" size={56} color="#FCA5A5" />
            <Text style={{ color: C.white, fontSize: 20, fontWeight: "700", marginTop: 12, textAlign: "center" }}>Consegna automatica non riuscita</Text>
            <Text style={{ color: "#E2E8F0", marginTop: 8, textAlign: "center", maxWidth: 420 }}>Motivo: {failure.reason}. {failure.message}</Text>
            <Text style={{ color: "#CBD5E1", marginTop: 8, textAlign: "center", maxWidth: 420 }}>
              Le risposte salvate restano sul server. Riprova: se non riesce, l'esame verrà chiuso dal server alla scadenza.
            </Text>
            <View style={{ gap: 8, marginTop: 20, minWidth: 220 }}>
              <Btn label="Riprova la consegna" onPress={() => submit(true, failure.reason)} loading={submitting} />
              <Btn label="Esci dall'esame" variant="ghost" style={{ backgroundColor: C.white }} onPress={() => { finishedRef.current = true; exitFullscreen(); onFinish({ error: failure.message, auto: true, reason: failure.reason, violations: violationsRef.current }); }} />
            </View>
          </View>
        )}
      </View>
    </Modal>
  );
}
