// src/screens/StudentExamsScreen.js
// Esami dello studente: elenco dei test da svolgere, avvio a schermo bloccato e riepilogo di consegna.
// Dati di PROVA da src/data/studentExams.js.
import { useEffect, useRef, useState } from "react";
import { FlatList, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Card from "../components/Card";
import Btn from "../components/Btn";
import EmptyState from "../components/EmptyState";
import ExamTaker, { MAX_VIOLATIONS, enterFullscreen } from "../components/ExamTaker";
import { useStudentExams, markSeen, submitExam, resetStudentDemo } from "../data/studentExams";

const fmtDateTime = (iso) => new Date(iso).toLocaleString("it-IT", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
const fmtDate = (d) => d ? new Date(d).toLocaleDateString("it-IT", { day: "2-digit", month: "2-digit", year: "numeric" }) : "";

const isAnswered = (q, a) => q.type === "open" ? !!a?.trim() : Array.isArray(a) ? a.length > 0 : !!a;

export default function StudentExamsScreen() {
  const { items } = useStudentExams();
  const [intro, setIntro] = useState(null); // test scelto, prima dell'inizio
  const [taking, setTaking] = useState(null); // test in svolgimento (schermo bloccato)
  const [delivered, setDelivered] = useState(null); // test appena consegnato o consegnato aperto dall'elenco

  // I nuovi test si evidenziano in questa visita; entrando nella sezione il pallino sparisce
  const newOnEntry = useRef(new Set(items.filter(i => i.isNew && !i.submission).map(i => i.testId)));
  useEffect(() => { markSeen(items.map(i => i.testId)); }, []);

  function start(item) {
    enterFullscreen(); // va chiamato direttamente dal click, altrimenti il browser lo rifiuta
    setIntro(null);
    setTaking(item);
  }

  function finish(item, submission) {
    submitExam(item.testId, submission);
    setTaking(null);
    setDelivered(item.testId);
  }

  // ── Test consegnato ────────────────────────────────────────────────────────
  const deliveredItem = delivered && items.find(i => i.testId === delivered);
  if (deliveredItem?.submission) {
    const { test, exam, submission } = deliveredItem;
    const questions = test.questions || [];
    const answered = questions.filter(q => isAnswered(q, submission.answers[q.id])).length;
    return (
      <View style={{ flex: 1, backgroundColor: C.bg, alignItems: "center", justifyContent: "center", padding: 24 }}>
        <Ionicons name="checkmark-circle" size={88} color={C.success} />
        <Text style={{ fontSize: 24, fontWeight: "700", color: C.text, marginTop: 12 }}>Esame consegnato</Text>
        <Text style={{ color: C.textMuted, marginTop: 4, textAlign: "center" }}>{test.name} · {exam?.name}</Text>
        <Card style={{ marginTop: 20, width: "100%", maxWidth: 420 }}>
          <Text style={s.itemSub}>Consegnato il {fmtDateTime(submission.submittedAt)}</Text>
          <Text style={s.itemSub}>Domande risposte: {answered} su {questions.length}</Text>
          {submission.autoSubmitted && (
            <Text style={[s.itemSub, { color: C.danger }]}>Consegna automatica: {submission.reason}</Text>
          )}
          {submission.violations > 0 && <Text style={s.itemSub}>Uscite dal test registrate: {submission.violations}</Text>}
        </Card>
        <Btn label="Torna agli esami" onPress={() => setDelivered(null)} style={{ marginTop: 20, minWidth: 220 }} />
      </View>
    );
  }

  // ── Regole prima dell'inizio ───────────────────────────────────────────────
  if (intro) {
    const { test, exam, durationMinutes } = intro;
    return (
      <View style={{ flex: 1, backgroundColor: C.bg, padding: 16, alignItems: "center" }}>
        <View style={{ width: "100%", maxWidth: 560 }}>
          <TouchableOpacity onPress={() => setIntro(null)} style={{ flexDirection: "row", alignItems: "center", gap: 6, marginBottom: 12 }}>
            <Ionicons name="arrow-back" size={22} color={C.text} /><Text style={{ color: C.text }}>Indietro</Text>
          </TouchableOpacity>
          <Card>
            <Text style={{ fontSize: 22, fontWeight: "700", color: C.text }}>{test.name}</Text>
            <Text style={{ color: C.textMuted, marginTop: 2 }}>{exam?.name}</Text>
            {!!test.description && <Text style={{ color: C.text, marginTop: 10 }}>{test.description}</Text>}
            <Text style={{ color: C.text, marginTop: 10 }}>
              {test.totalQuestions} domande · {test.totalPoints} punti · {durationMinutes} minuti
            </Text>

            <View style={{ marginTop: 16, backgroundColor: "#FEF3C7", borderRadius: 10, padding: 12 }}>
              <Text style={{ color: "#92400E", fontWeight: "700", marginBottom: 6 }}>Prima di iniziare</Text>
              <Text style={{ color: "#92400E" }}>• Lo schermo verrà bloccato: potrai fare solo il test.</Text>
              <Text style={{ color: "#92400E" }}>• Non uscire dallo schermo intero, non cambiare scheda o finestra.</Text>
              <Text style={{ color: "#92400E" }}>• Copia e incolla sono disattivati.</Text>
              <Text style={{ color: "#92400E" }}>• Alla {MAX_VIOLATIONS}ª uscita, o allo scadere del tempo, il test viene consegnato automaticamente.</Text>
            </View>
            <Btn label="Inizia il test" onPress={() => start(intro)} style={{ marginTop: 16 }} />
          </Card>
        </View>
      </View>
    );
  }

  // ── Elenco ─────────────────────────────────────────────────────────────────
  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={s.screenPad}>
        <Text style={{ fontSize: 20, fontWeight: "700", color: C.text, marginBottom: 12 }}>I miei esami</Text>
      </View>

      {items.length === 0 ? <EmptyState message="Nessun esame da svolgere" /> : (
        <FlatList
          data={items}
          keyExtractor={i => i.testId}
          contentContainerStyle={{ paddingHorizontal: 16, paddingBottom: 32 }}
          ListFooterComponent={
            <TouchableOpacity onPress={resetStudentDemo} style={{ marginTop: 8, alignItems: "center" }}>
              <Text style={{ color: C.textLight, fontSize: 12 }}>Ripristina la prova (solo demo)</Text>
            </TouchableOpacity>
          }
          renderItem={({ item }) => {
            const done = !!item.submission;
            const isNew = newOnEntry.current.has(item.testId) && !done;
            return (
              <TouchableOpacity activeOpacity={0.7} onPress={() => done ? setDelivered(item.testId) : setIntro(item)}>
                <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
                  <View style={[s.avatar, { backgroundColor: done ? "#D1FAE5" : "#FDE68A" }]}>
                    <Ionicons name={done ? "checkmark" : "document-text-outline"} size={24} color={done ? "#065F46" : "#92400E"} />
                  </View>
                  <View style={{ flex: 1, marginLeft: 12 }}>
                    <Text style={s.itemTitle}>{item.test.name}</Text>
                    <Text style={s.itemSub}>{item.exam?.name}{item.exam?.date ? ` · ${fmtDate(item.exam.date)}` : ""}</Text>
                    <Text style={s.itemSub}>{item.test.totalQuestions} domande · {item.durationMinutes} min</Text>
                  </View>
                  <View style={{ alignItems: "flex-end", gap: 4 }}>
                    {isNew && <Text style={{ backgroundColor: C.danger, color: C.white, fontSize: 11, fontWeight: "700", paddingHorizontal: 8, paddingVertical: 2, borderRadius: 10, overflow: "hidden" }}>NUOVO</Text>}
                    <Text style={{ color: done ? C.success : C.primary, fontWeight: "600", fontSize: 13 }}>{done ? "Consegnato" : "Da svolgere"}</Text>
                  </View>
                </Card>
              </TouchableOpacity>
            );
          }}
        />
      )}

      {taking && <ExamTaker item={taking} onFinish={(submission) => finish(taking, submission)} />}
    </View>
  );
}
