// src/components/TestViewer.js
// Dettaglio in sola lettura di un esame (ExamDetailDto): dati, domande, opzioni con le risposte giuste, punti.
import { ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import StatusChip, { EXAM_TONE } from "./StatusChip";
import { ExamAction } from "./ExamCard";
import Card from "./Card";
import { EXAM_STATUS, EXAM_STATUS_LABEL, fromEnum, formatDateTime, questionFromDto } from "../api/examContract";

const TYPE_LABEL = { single: "Singola", multiple: "Multipla", open: "Aperta" };

// Pagina dell'app (non più un Modal): sta nell'area dei contenuti, con la sidebar visibile.
// onClose non è più usato dalla barra: il ritorno alla lista è nell'header di AppNav (resta per compatibilità).
// actions: { edit, publish, remove, submissions, close, cancel } → funzione o null se non consentita
// (le stesse della card della lista, decise da chi la usa in base a stato e ruolo).
export default function TestViewer({ exam, contextLabel, onClose, actions = {}, busy }) {
  if (!exam) return null;
  const questions = [...(exam.questions || [])].sort((a, b) => a.position - b.position).map(questionFromDto);
  const status = fromEnum(exam.status, EXAM_STATUS);
  const closed = questions.filter(q => q.type !== "open").length;
  const secondary = [
    actions.edit && <ExamAction key="edit" label="Modifica" icon="create-outline" onPress={actions.edit} disabled={busy} />,
    actions.submissions && <ExamAction key="subs" label="Consegne" icon="people-outline" onPress={actions.submissions} disabled={busy} />,
    actions.close && <ExamAction key="close" label="Chiudi" icon="lock-closed-outline" onPress={actions.close} disabled={busy} />,
  ].filter(Boolean);
  const primary = [
    actions.remove && <ExamAction key="remove" label="Elimina" icon="trash-outline" tone="danger" onPress={actions.remove} disabled={busy} />,
    actions.cancel && <ExamAction key="cancel" label="Annulla esame" icon="close-circle-outline" tone="danger" onPress={actions.cancel} disabled={busy} />,
    actions.publish && <ExamAction key="publish" label="Pubblica" icon="paper-plane-outline" tone="success" onPress={actions.publish} disabled={busy} loading={busy} />,
  ].filter(Boolean);

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={{ flex: 1 }}>
        {/* Barra della pagina: il titolo ("← Esami / nome") è nell'header di AppNav; qui classe, stato e
            azioni (come sulla card) */}
        <View style={{ flexDirection: "row", alignItems: "center", flexWrap: "wrap", gap: 10, backgroundColor: C.white, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderBottomColor: C.border }}>
          <View style={{ flex: 1, minWidth: 160 }}>
            {!!contextLabel && <Text style={{ color: C.textMuted, fontSize: 14, fontWeight: "600" }} numberOfLines={2}>{contextLabel}</Text>}
          </View>
          <StatusChip label={EXAM_STATUS_LABEL[status] || status} tone={EXAM_TONE[status]} style={{ alignSelf: "center" }} />
          {(secondary.length > 0 || primary.length > 0) && (
            <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8, marginLeft: "auto" }}>{secondary}{primary}</View>
          )}
        </View>

        <ScrollView contentContainerStyle={{ padding: 16, alignItems: "center" }}>
          <View style={{ width: "100%", maxWidth: 760 }}>
            <Card style={{ marginBottom: 12, borderTopWidth: 8, borderTopColor: C.primary }}>
              <Text style={{ fontSize: 24, fontWeight: "600", color: C.text }}>{exam.name}</Text>
              {!!exam.description && <Text style={{ color: C.textMuted, marginTop: 6 }}>{exam.description}</Text>}
              <Text style={{ color: C.text, marginTop: 12 }}>
                {EXAM_STATUS_LABEL[status]} · {formatDateTime(exam.availableFrom)} → {formatDateTime(exam.availableUntil)}
              </Text>
              <Text style={{ color: C.text, marginTop: 4 }}>
                {exam.durationMinutes} min · soglia {exam.passingPercent}% · {questions.length} domande ({closed} chiuse, {questions.length - closed} aperte) · {exam.totalPoints} punti
                {exam.assignedStudents ? ` · ${exam.assignedStudents.length} studenti` : ""}
              </Text>
            </Card>

            {questions.map((q, i) => (
              <Card key={q.id} style={{ marginBottom: 12, borderLeftWidth: 5, borderLeftColor: C.primary }}>
                <View style={{ flexDirection: "row", justifyContent: "space-between", gap: 10 }}>
                  <Text style={{ flex: 1, color: C.text, fontSize: 15, fontWeight: "600" }}>{i + 1}. {q.text}{q.required ? " *" : ""}</Text>
                  <Text style={{ color: C.textMuted }}>{q.points} pt</Text>
                </View>
                <Text style={{ color: C.textLight, fontSize: 12, marginBottom: 6 }}>{TYPE_LABEL[q.type] || q.type}</Text>

                {q.type === "open" ? (
                  <Text style={{ color: C.textLight, borderBottomWidth: 1, borderBottomColor: C.border, paddingVertical: 8 }}>
                    Risposta scritta dallo studente
                  </Text>
                ) : q.options.map(o => {
                  const correct = q.correct.includes(o.id);
                  const multi = q.type === "multiple";
                  return (
                    <View key={o.id} style={{ flexDirection: "row", alignItems: "center", gap: 8, paddingVertical: 4 }}>
                      <Ionicons name={correct ? (multi ? "checkbox" : "radio-button-on") : (multi ? "square-outline" : "radio-button-off")}
                        size={22} color={correct ? C.success : C.textLight} />
                      <Text style={{ flex: 1, color: C.text }}>{o.text}</Text>
                      {correct && <Text style={{ color: C.success, fontSize: 12 }}>giusta</Text>}
                    </View>
                  );
                })}
              </Card>
            ))}
          </View>
        </ScrollView>
      </View>
    </View>
  );
}
