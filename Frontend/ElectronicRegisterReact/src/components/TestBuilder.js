// src/components/TestBuilder.js
// Editor di quiz a schermo intero, ispirato a Google Forms: titolo e descrizione,
// domande (singola, multipla, aperta), opzioni con risposta giusta, punti, obbligatoria, duplica/elimina.
import { Modal, ScrollView, Switch, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Btn from "./Btn";
import Card from "./Card";
import SelectField from "./SelectField";

export const QUESTION_TYPES = [
  { id: "single", label: "Singola", closed: true },     // una sola risposta giusta
  { id: "multiple", label: "Multipla", closed: true },  // una o più risposte giuste
  { id: "open", label: "Aperta", closed: false },       // risposta scritta
];

const uid = () => Math.random().toString(36).slice(2, 9);
const isClosed = (type) => QUESTION_TYPES.find(t => t.id === type)?.closed;

export const newOption = () => ({ id: uid(), text: "" });
export const newQuestion = () => ({
  id: uid(), text: "", type: "single", options: [newOption(), newOption()],
  correct: [], points: "1", required: false,
});
export const newTestDraft = () => ({ title: "", description: "", numberOfStudents: "", questions: [newQuestion()] });

// Conteggi da salvare insieme al test.
export function summarize(draft) {
  const qs = draft.questions;
  const closed = qs.filter(q => isClosed(q.type)).length;
  return {
    totalQuestions: qs.length, closedQuestions: closed, openQuestions: qs.length - closed,
    totalPoints: qs.reduce((sum, q) => sum + (Number(q.points) || 0), 0),
  };
}

export function validateTestDraft(d) {
  if (!d.title.trim()) return "Inserisci il titolo del test.";
  if (!(Number(d.numberOfStudents) > 0)) return "Inserisci il numero di studenti.";
  if (d.questions.length === 0) return "Aggiungi almeno una domanda.";
  for (let i = 0; i < d.questions.length; i++) {
    const q = d.questions[i];
    if (!q.text.trim()) return `La domanda ${i + 1} non ha il testo.`;
    if (isClosed(q.type)) {
      const filled = q.options.filter(o => o.text.trim());
      if (filled.length < 2) return `La domanda ${i + 1} deve avere almeno due opzioni con testo.`;
      if (!q.correct.some(id => filled.some(o => o.id === id))) return `Nella domanda ${i + 1} scegli la risposta giusta.`;
    }
  }
  return null;
}

const underlined = { borderBottomWidth: 1, borderBottomColor: C.border, paddingVertical: 8, color: C.text, fontSize: 15 };

function QuestionCard({ q, index, onChange, onDuplicate, onDelete, canDelete }) {
  const set = (patch) => onChange({ ...q, ...patch });
  const closed = isClosed(q.type);
  const multi = q.type === "multiple";

  // Passando a "singola" resta al massimo una risposta giusta; passando ad "aperta" si azzerano.
  function changeType(type) {
    set({ type, correct: !isClosed(type) ? [] : type === "single" ? q.correct.slice(0, 1) : q.correct });
  }
  const setOption = (id, text) => set({ options: q.options.map(o => o.id === id ? { ...o, text } : o) });
  const removeOption = (id) => set({ options: q.options.filter(o => o.id !== id), correct: q.correct.filter(c => c !== id) });
  const toggleCorrect = (id) => set({
    correct: multi ? (q.correct.includes(id) ? q.correct.filter(c => c !== id) : [...q.correct, id]) : [id],
  });

  return (
    <Card style={{ marginBottom: 12, borderLeftWidth: 5, borderLeftColor: C.primary }}>
      <View style={{ flexDirection: "row", gap: 12, alignItems: "flex-start", flexWrap: "wrap" }}>
        <View style={{ flex: 1, minWidth: 220 }}>
          <TextInput style={[underlined, { backgroundColor: C.bg, paddingHorizontal: 10 }]} placeholder={`Domanda ${index + 1}`}
            placeholderTextColor={C.textLight} value={q.text} onChangeText={(text) => set({ text })} multiline />
        </View>
        <View style={{ width: 200 }}>
          <SelectField value={q.type} options={QUESTION_TYPES} onSelect={changeType} />
        </View>
      </View>

      {closed ? (
        <View style={{ marginTop: 4 }}>
          <Text style={{ color: C.textMuted, fontSize: 12, marginBottom: 4 }}>
            {multi ? "Tocca le caselle per segnare le risposte giuste" : "Tocca il cerchio per segnare la risposta giusta"}
          </Text>
          {q.options.map((o, i) => {
            const correct = q.correct.includes(o.id);
            return (
              <View key={o.id} style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
                <TouchableOpacity onPress={() => toggleCorrect(o.id)}>
                  <Ionicons name={correct ? (multi ? "checkbox" : "radio-button-on") : (multi ? "square-outline" : "radio-button-off")}
                    size={26} color={correct ? C.success : C.textLight} />
                </TouchableOpacity>
                <TextInput style={[underlined, { flex: 1 }, correct && { borderBottomColor: C.success }]} placeholder={`Opzione ${i + 1}`}
                  placeholderTextColor={C.textLight} value={o.text} onChangeText={(text) => setOption(o.id, text)} />
                {correct && <Text style={{ color: C.success, fontSize: 12 }}>giusta</Text>}
                {q.options.length > 1 && (
                  <TouchableOpacity onPress={() => removeOption(o.id)}><Ionicons name="close" size={22} color={C.textMuted} /></TouchableOpacity>
                )}
              </View>
            );
          })}
          <TouchableOpacity onPress={() => set({ options: [...q.options, newOption()] })} style={{ flexDirection: "row", alignItems: "center", gap: 8, marginTop: 10 }}>
            <Ionicons name="add-circle-outline" size={24} color={C.primary} />
            <Text style={{ color: C.primary }}>Aggiungi opzione</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <Text style={{ color: C.textLight, borderBottomWidth: 1, borderBottomColor: C.border, paddingVertical: 8, marginTop: 4 }}>
          Risposta scritta dallo studente
        </Text>
      )}

      <View style={{ flexDirection: "row", alignItems: "center", marginTop: 14, paddingTop: 10, borderTopWidth: 1, borderTopColor: C.border, gap: 14, flexWrap: "wrap" }}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flex: 1 }}>
          <TextInput style={[s.input, { width: 70, textAlign: "center" }]} keyboardType="number-pad" value={String(q.points)}
            onChangeText={(v) => set({ points: v.replace(/[^0-9]/g, "") })} />
          <Text style={{ color: C.text }}>punti</Text>
        </View>
        <TouchableOpacity onPress={onDuplicate}><Ionicons name="copy-outline" size={24} color={C.textMuted} /></TouchableOpacity>
        <TouchableOpacity onPress={onDelete} disabled={!canDelete}><Ionicons name="trash-outline" size={24} color={canDelete ? C.textMuted : C.border} /></TouchableOpacity>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
          <Text style={{ color: C.text }}>Obbligatoria</Text>
          <Switch value={q.required} onValueChange={(required) => set({ required })} />
        </View>
      </View>
    </Card>
  );
}

export default function TestBuilder({ visible, contextLabel, draft, onChange, onSave, onClose, onDiscard, saving }) {
  if (!draft) return null;
  const { totalPoints } = summarize(draft);
  const setQuestion = (id, next) => onChange({ ...draft, questions: draft.questions.map(q => q.id === id ? next : q) });
  const duplicate = (q) => {
    const opts = q.options.map(o => ({ ...o, id: uid() }));
    const correct = q.correct.map(c => opts[q.options.findIndex(o => o.id === c)]?.id).filter(Boolean);
    const at = draft.questions.findIndex(x => x.id === q.id) + 1;
    const copy = { ...q, id: uid(), options: opts, correct };
    onChange({ ...draft, questions: [...draft.questions.slice(0, at), copy, ...draft.questions.slice(at)] });
  };

  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <View style={{ flex: 1, backgroundColor: "#EEEBF9" }}>
        <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", backgroundColor: C.white, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderBottomColor: C.border }}>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 10, flex: 1 }}>
            <TouchableOpacity onPress={onClose}><Ionicons name="close" size={28} color={C.text} /></TouchableOpacity>
            <View style={{ flex: 1 }}>
              <Text style={{ color: C.text, fontSize: 16, fontWeight: "600" }} numberOfLines={1}>{draft.title || "Test senza titolo"}</Text>
              {!!contextLabel && <Text style={{ color: C.textMuted, fontSize: 12 }} numberOfLines={1}>{contextLabel}</Text>}
            </View>
          </View>
          <Btn label="Salva test" onPress={onSave} loading={saving} style={s.smBtn} textStyle={s.smBtnText} />
        </View>

        <ScrollView contentContainerStyle={{ padding: 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
          <View style={{ width: "100%", maxWidth: 760 }}>
            <Card style={{ marginBottom: 12, borderTopWidth: 8, borderTopColor: C.primary }}>
              <TextInput style={[underlined, { fontSize: 28, fontWeight: "600", borderBottomColor: C.primary }]} placeholder="Test senza titolo"
                placeholderTextColor={C.textLight} value={draft.title} onChangeText={(title) => onChange({ ...draft, title })} />
              <TextInput style={[underlined, { marginTop: 8 }]} placeholder="Descrizione del test" placeholderTextColor={C.textLight}
                value={draft.description} onChangeText={(description) => onChange({ ...draft, description })} multiline />
              <View style={{ flexDirection: "row", alignItems: "center", gap: 10, marginTop: 14 }}>
                <Text style={{ color: C.text }}>Numero di studenti</Text>
                <TextInput style={[s.input, { width: 90, textAlign: "center" }]} keyboardType="number-pad" value={String(draft.numberOfStudents)}
                  onChangeText={(v) => onChange({ ...draft, numberOfStudents: v.replace(/[^0-9]/g, "") })} />
                <Text style={{ color: C.textMuted, marginLeft: "auto" }}>Totale punti: {totalPoints}</Text>
              </View>
            </Card>

            {draft.questions.map((q, i) => (
              <QuestionCard key={q.id} q={q} index={i} canDelete={draft.questions.length > 1}
                onChange={(next) => setQuestion(q.id, next)}
                onDuplicate={() => duplicate(q)}
                onDelete={() => onChange({ ...draft, questions: draft.questions.filter(x => x.id !== q.id) })} />
            ))}

            <Btn label="+ Aggiungi domanda" variant="ghost" onPress={() => onChange({ ...draft, questions: [...draft.questions, newQuestion()] })} style={{ backgroundColor: C.white, marginBottom: 12 }} />
            <Btn label="Elimina bozza" variant="danger" onPress={onDiscard} style={{ marginBottom: 32 }} />
          </View>
        </ScrollView>
      </View>
    </Modal>
  );
}
