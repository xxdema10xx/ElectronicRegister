// src/components/ExamTaker.js
// Svolgimento del test a schermo bloccato. Il blocco è "di deterrenza" e non a prova di manomissione:
// su web usa lo schermo intero, e uscire, cambiare scheda/finestra o perdere il focus conta come violazione;
// su nativo si controlla che l'app resti in primo piano. Alla terza violazione, o allo scadere del tempo,
// il test viene consegnato automaticamente.
import { useCallback, useEffect, useRef, useState } from "react";
import { AppState, Modal, Platform, ScrollView, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Btn from "./Btn";
import Card from "./Card";

export const MAX_VIOLATIONS = 3;
const isWeb = Platform.OS === "web";

export function enterFullscreen() {
  if (isWeb) return document.documentElement.requestFullscreen?.().catch(() => {});
}

const isAnswered = (q, a) => q.type === "open" ? !!a?.trim() : Array.isArray(a) ? a.length > 0 : !!a;
const fmt = (sec) => `${String(Math.floor(sec / 60)).padStart(2, "0")}:${String(sec % 60).padStart(2, "0")}`;

export default function ExamTaker({ item, onFinish }) {
  const { test, exam, durationMinutes } = item;
  const questions = test.questions || [];

  const [answers, setAnswers] = useState({});
  const [remaining, setRemaining] = useState(durationMinutes * 60);
  const [violations, setViolations] = useState(0);
  const [paused, setPaused] = useState(null); // messaggio mostrato mentre il test è in pausa per una violazione
  const [confirming, setConfirming] = useState(false);

  const finishedRef = useRef(false);
  const pausedRef = useRef(false);
  const violationsRef = useRef(0);
  const answersRef = useRef(answers);
  answersRef.current = answers;

  const finish = useCallback((autoSubmitted, reason) => {
    if (finishedRef.current) return;
    finishedRef.current = true;
    if (isWeb && document.fullscreenElement) document.exitFullscreen().catch(() => {});
    onFinish({ answers: answersRef.current, submittedAt: new Date().toISOString(), violations: violationsRef.current, autoSubmitted, reason });
  }, [onFinish]);

  const registerViolation = useCallback((message) => {
    if (finishedRef.current || pausedRef.current) return;
    pausedRef.current = true;
    violationsRef.current += 1;
    setViolations(violationsRef.current);
    if (violationsRef.current >= MAX_VIOLATIONS) return finish(true, "troppe uscite dal test");
    setPaused(message);
  }, [finish]);

  function resume() {
    enterFullscreen();
    pausedRef.current = false;
    setPaused(null);
  }

  // Timer: continua anche durante la pausa
  useEffect(() => {
    const id = setInterval(() => setRemaining(r => r - 1), 1000);
    return () => clearInterval(id);
  }, []);
  useEffect(() => { if (remaining <= 0) finish(true, "tempo scaduto"); }, [remaining, finish]);

  // Controlli anti-uscita
  useEffect(() => {
    if (isWeb) {
      const onFullscreen = () => { if (!document.fullscreenElement) registerViolation("Sei uscito dalla modalità a schermo intero."); };
      const onVisibility = () => { if (document.visibilityState === "hidden") registerViolation("Hai cambiato scheda o finestra."); };
      const onBlur = () => registerViolation("La finestra del test ha perso il focus.");
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
    const sub = AppState.addEventListener("change", (st) => { if (st !== "active") registerViolation("Hai lasciato l'app durante il test."); });
    return () => sub.remove();
  }, [registerViolation]);

  const setAnswer = (qid, value) => setAnswers(a => ({ ...a, [qid]: value }));
  const toggleMulti = (qid, oid) => setAnswers(a => {
    const cur = a[qid] || [];
    return { ...a, [qid]: cur.includes(oid) ? cur.filter(x => x !== oid) : [...cur, oid] };
  });
  const answered = questions.filter(q => isAnswered(q, answers[q.id])).length;

  return (
    // onRequestClose vuoto: il tasto indietro di Android non chiude il test
    <Modal visible animationType="none" statusBarTranslucent onRequestClose={() => {}}>
      <View style={{ flex: 1, backgroundColor: "#EEEBF9" }}>
        <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", backgroundColor: C.primary, paddingHorizontal: 16, paddingVertical: 12 }}>
          <View style={{ flex: 1 }}>
            <Text style={{ color: C.white, fontSize: 16, fontWeight: "700" }} numberOfLines={1}>{test.name}</Text>
            <Text style={{ color: "#CBD5E1", fontSize: 12 }} numberOfLines={1}>{exam?.name}</Text>
          </View>
          <View style={{ alignItems: "flex-end" }}>
            <Text style={{ color: remaining <= 60 ? "#FCA5A5" : C.white, fontSize: 20, fontWeight: "700" }}>{fmt(Math.max(remaining, 0))}</Text>
            <Text style={{ color: "#CBD5E1", fontSize: 11 }}>Uscite: {violations}/{MAX_VIOLATIONS}</Text>
          </View>
        </View>

        <ScrollView contentContainerStyle={{ padding: 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
          <View style={[{ width: "100%", maxWidth: 760 }, isWeb && { userSelect: "none" }]}>
            {questions.map((q, i) => (
              <Card key={q.id} style={{ marginBottom: 12, borderLeftWidth: 5, borderLeftColor: C.primary }}>
                <View style={{ flexDirection: "row", justifyContent: "space-between", gap: 10 }}>
                  <Text style={{ flex: 1, color: C.text, fontSize: 15, fontWeight: "600" }}>{i + 1}. {q.text}</Text>
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

            {confirming ? (
              <Card style={{ marginBottom: 32 }}>
                <Text style={{ color: C.text, fontWeight: "600", marginBottom: 4 }}>Consegnare il test?</Text>
                <Text style={{ color: C.textMuted, marginBottom: 12 }}>
                  Hai risposto a {answered} domande su {questions.length}. Dopo la consegna non potrai modificare le risposte.
                </Text>
                <View style={{ gap: 8 }}>
                  <Btn label="Sì, consegna" variant="success" onPress={() => finish(false, null)} />
                  <Btn label="Torna al test" variant="ghost" onPress={() => setConfirming(false)} />
                </View>
              </Card>
            ) : (
              <Btn label={`Consegna (${answered}/${questions.length})`} variant="success" onPress={() => setConfirming(true)} style={{ marginBottom: 32 }} />
            )}
          </View>
        </ScrollView>

        {/* Test in pausa dopo un'uscita: non si vede né si fa nulla finché non si riprende */}
        {paused && (
          <View style={{ position: "absolute", top: 0, left: 0, right: 0, bottom: 0, backgroundColor: C.primary, alignItems: "center", justifyContent: "center", padding: 24, zIndex: 10 }}>
            <Ionicons name="warning-outline" size={56} color="#FCD34D" />
            <Text style={{ color: C.white, fontSize: 20, fontWeight: "700", marginTop: 12, textAlign: "center" }}>Test in pausa</Text>
            <Text style={{ color: "#E2E8F0", marginTop: 8, textAlign: "center", maxWidth: 420 }}>{paused}</Text>
            <Text style={{ color: "#FCA5A5", marginTop: 8, textAlign: "center", maxWidth: 420 }}>
              Uscita {violations} di {MAX_VIOLATIONS}: alla {MAX_VIOLATIONS}ª il test viene consegnato automaticamente. Il tempo continua a scorrere.
            </Text>
            <Btn label="Riprendi il test" onPress={resume} style={{ marginTop: 20, minWidth: 220 }} />
          </View>
        )}
      </View>
    </Modal>
  );
}
