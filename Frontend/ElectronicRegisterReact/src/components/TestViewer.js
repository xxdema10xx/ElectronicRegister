// src/components/TestViewer.js
// Vista in sola lettura di un esame (ExamDetailDto): dati, domande, opzioni con le risposte giuste, punti.
import { Modal, ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C } from "../constants/theme";
import Card from "./Card";
import { EXAM_STATUS, EXAM_STATUS_LABEL, fromEnum, formatDateTime, questionFromDto } from "../api/examContract";

const TYPE_LABEL = { single: "Singola", multiple: "Multipla", open: "Aperta" };

export default function TestViewer({ exam, contextLabel, onClose }) {
  if (!exam) return null;
  const questions = [...(exam.questions || [])].sort((a, b) => a.position - b.position).map(questionFromDto);
  const status = fromEnum(exam.status, EXAM_STATUS);
  const closed = questions.filter(q => q.type !== "open").length;

  return (
    <Modal visible animationType="slide" onRequestClose={onClose}>
      <View style={{ flex: 1, backgroundColor: "#EEEBF9" }}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 10, backgroundColor: C.white, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderBottomColor: C.border }}>
          <TouchableOpacity onPress={onClose}><Ionicons name="close" size={28} color={C.text} /></TouchableOpacity>
          <View style={{ flex: 1 }}>
            <Text style={{ color: C.text, fontSize: 16, fontWeight: "600" }} numberOfLines={1}>{exam.name}</Text>
            {!!contextLabel && <Text style={{ color: C.textMuted, fontSize: 12 }} numberOfLines={1}>{contextLabel}</Text>}
          </View>
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
    </Modal>
  );
}
