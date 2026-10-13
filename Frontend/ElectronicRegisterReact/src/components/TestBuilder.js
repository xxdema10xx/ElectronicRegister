// src/components/TestBuilder.js
// Creazione guidata di un test, a schermo intero:
//   1. Dettagli — titolo, descrizione e numero di studenti;
//   2. Domande — a sinistra l'indice delle domande (si selezionano e si riordinano trascinandole),
//      a destra l'editor ampio della domanda scelta. Su schermi stretti i due pannelli si impilano.
// Il passo corrente è salvato nella bozza (draft.step), così si riprende da dove si era rimasti.
import { useState } from "react";
import { Modal, ScrollView, Switch, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useLayout } from "../hooks/useLayout";
import { notify } from "../utils/dialogs";
import Btn from "./Btn";
import Card from "./Card";
import DraggableList from "./DraggableList";

export const QUESTION_TYPES = [
  { id: "single", label: "Singola", closed: true, icon: "radio-button-on-outline", hint: "Una sola risposta giusta" },
  { id: "multiple", label: "Multipla", closed: true, icon: "checkbox-outline", hint: "Una o più risposte giuste" },
  { id: "open", label: "Aperta", closed: false, icon: "create-outline", hint: "Risposta scritta" },
];

const uid = () => Math.random().toString(36).slice(2, 9);
const isClosed = (type) => QUESTION_TYPES.find(t => t.id === type)?.closed;

export const newOption = () => ({ id: uid(), text: "" });
export const newQuestion = () => ({
  id: uid(), text: "", type: "single", options: [newOption(), newOption()],
  correct: [], points: "1", required: false,
});
export const newTestDraft = () => ({ step: 1, title: "", description: "", numberOfStudents: "", questions: [newQuestion()] });

// Conteggi da salvare insieme al test.
export function summarize(draft) {
  const qs = draft.questions;
  const closed = qs.filter(q => isClosed(q.type)).length;
  return {
    totalQuestions: qs.length, closedQuestions: closed, openQuestions: qs.length - closed,
    totalPoints: qs.reduce((sum, q) => sum + (Number(q.points) || 0), 0),
  };
}

// Problema di una domanda (testo da mostrare) oppure null se è a posto.
function questionIssue(q, i) {
  if (!q.text.trim()) return `La domanda ${i + 1} non ha il testo.`;
  if (isClosed(q.type)) {
    const filled = q.options.filter(o => o.text.trim());
    if (filled.length < 2) return `La domanda ${i + 1} deve avere almeno due opzioni con testo.`;
    if (!q.correct.some(id => filled.some(o => o.id === id))) return `Nella domanda ${i + 1} scegli la risposta giusta.`;
  }
  return null;
}

// Controlli del primo passo della procedura (dettagli del test).
export function validateDetails(d) {
  if (!d.title.trim()) return "Inserisci il titolo del test.";
  if (!(Number(d.numberOfStudents) > 0)) return "Inserisci il numero di studenti.";
  return null;
}

export function validateTestDraft(d) {
  const detailsIssue = validateDetails(d);
  if (detailsIssue) return detailsIssue;
  if (d.questions.length === 0) return "Aggiungi almeno una domanda.";
  for (let i = 0; i < d.questions.length; i++) {
    const issue = questionIssue(d.questions[i], i);
    if (issue) return issue;
  }
  return null;
}

const field = { backgroundColor: C.bg, borderWidth: 1, borderColor: C.border, borderRadius: 10, paddingHorizontal: 12, paddingVertical: 10, color: C.text, fontSize: 15 };

// Bottone a icona; con caption mostra sotto l'icona una piccola etichetta
function IconBtn({ name, onPress, disabled, color = C.textMuted, label, caption }) {
  return (
    <TouchableOpacity onPress={onPress} disabled={disabled} accessibilityLabel={label}
      style={{ padding: 8, borderRadius: 8, alignItems: "center", opacity: disabled ? 0.3 : 1 }}>
      <Ionicons name={name} size={22} color={color} />
      {!!caption && <Text style={{ color, fontSize: 11, fontWeight: "600", marginTop: 2 }}>{caption}</Text>}
    </TouchableOpacity>
  );
}

// Editor ampio della domanda selezionata
function QuestionEditor({ q, index, total, onChange, onDuplicate, onDelete, onMove, canDelete }) {
  const set = (patch) => onChange({ ...q, ...patch });
  const closed = isClosed(q.type);
  const multi = q.type === "multiple";
  const [focusId, setFocusId] = useState(null); // opzione appena aggiunta: prende il cursore

  // Passando a "singola" resta al massimo una risposta giusta; passando ad "aperta" si azzerano.
  function changeType(type) {
    set({ type, correct: !isClosed(type) ? [] : type === "single" ? q.correct.slice(0, 1) : q.correct });
  }
  const setOption = (id, text) => set({ options: q.options.map(o => o.id === id ? { ...o, text } : o) });
  const removeOption = (id) => set({ options: q.options.filter(o => o.id !== id), correct: q.correct.filter(c => c !== id) });
  const toggleCorrect = (id) => set({
    correct: multi ? (q.correct.includes(id) ? q.correct.filter(c => c !== id) : [...q.correct, id]) : [id],
  });
  function addOption() {
    const o = newOption();
    setFocusId(o.id);
    set({ options: [...q.options, o] });
  }

  return (
    <Card style={{ padding: 24 }}>
      <Text style={{ color: C.textMuted, fontWeight: "700", fontSize: 12, textTransform: "uppercase", marginBottom: 12 }}>
        Domanda {index + 1} di {total}
      </Text>

      <Text style={s.label}>Testo della domanda</Text>
      <TextInput style={[field, { fontSize: 18, minHeight: 80, textAlignVertical: "top" }]} placeholder="Scrivi la domanda…"
        placeholderTextColor={C.textLight} value={q.text} onChangeText={(text) => set({ text })} multiline />

      <Text style={[s.label, { marginTop: 18 }]}>Tipo di risposta</Text>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {QUESTION_TYPES.map(t => {
          const active = q.type === t.id;
          return (
            <TouchableOpacity key={t.id} onPress={() => changeType(t.id)}
              style={{ flexDirection: "row", alignItems: "center", gap: 8, paddingVertical: 10, paddingHorizontal: 14, borderRadius: 10, borderWidth: 1,
                borderColor: active ? C.primary : C.border, backgroundColor: active ? C.primary : C.white }}>
              <Ionicons name={t.icon} size={18} color={active ? C.white : C.textMuted} />
              <Text style={{ color: active ? C.white : C.text, fontWeight: "600" }}>{t.label}</Text>
            </TouchableOpacity>
          );
        })}
      </View>
      <Text style={[s.itemSub, { marginTop: 6 }]}>{QUESTION_TYPES.find(t => t.id === q.type)?.hint}</Text>

      {closed ? (
        <View style={{ marginTop: 20 }}>
          <Text style={s.label}>Opzioni — {multi ? "spunta tutte le risposte giuste" : "scegli la risposta giusta"}</Text>
          {q.options.map((o, i) => {
            const correct = q.correct.includes(o.id);
            return (
              <View key={o.id} style={{ flexDirection: "row", alignItems: "center", gap: 10, marginBottom: 8 }}>
                <TouchableOpacity onPress={() => toggleCorrect(o.id)} accessibilityLabel="Segna come giusta">
                  <Ionicons name={correct ? (multi ? "checkbox" : "radio-button-on") : (multi ? "square-outline" : "radio-button-off")}
                    size={28} color={correct ? C.success : C.textLight} />
                </TouchableOpacity>
                <TextInput style={[field, { flex: 1 }, correct && { borderColor: C.success, backgroundColor: "#ECFDF5" }]} placeholder={`Opzione ${i + 1}`}
                  placeholderTextColor={C.textLight} value={o.text} onChangeText={(text) => setOption(o.id, text)}
                  autoFocus={o.id === focusId} onSubmitEditing={addOption} blurOnSubmit={false} />
                {correct && <Text style={{ color: C.success, fontSize: 12, fontWeight: "700", width: 44 }}>GIUSTA</Text>}
                <IconBtn name="close" onPress={() => removeOption(o.id)} disabled={q.options.length <= 2} label="Rimuovi opzione" />
              </View>
            );
          })}
          <TouchableOpacity onPress={addOption} style={{ flexDirection: "row", alignItems: "center", gap: 8, marginTop: 6 }}>
            <Ionicons name="add-circle-outline" size={24} color={C.primary} />
            <Text style={{ color: C.primary, fontWeight: "600" }}>Aggiungi opzione</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <View style={{ marginTop: 20, padding: 16, borderRadius: 10, borderWidth: 1, borderStyle: "dashed", borderColor: C.border }}>
          <Text style={{ color: C.textMuted }}>Lo studente scriverà la risposta in un campo di testo libero. Va corretta a mano.</Text>
        </View>
      )}

      <View style={{ flexDirection: "row", alignItems: "center", marginTop: 24, paddingTop: 16, borderTopWidth: 1, borderTopColor: C.border, gap: 16, flexWrap: "wrap" }}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
          <TextInput style={[field, { width: 72, textAlign: "center" }]} keyboardType="number-pad" value={String(q.points)}
            onChangeText={(v) => set({ points: v.replace(/[^0-9]/g, "") })} />
          <Text style={{ color: C.text }}>punti</Text>
        </View>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
          <Switch value={q.required} onValueChange={(required) => set({ required })} />
          <Text style={{ color: C.text }}>Obbligatoria</Text>
        </View>
        <View style={{ flexDirection: "row", alignItems: "flex-start", marginLeft: "auto" }}>
          <IconBtn name="arrow-up" onPress={() => onMove(-1)} disabled={index === 0} label="Sposta su" caption="Su" />
          <IconBtn name="arrow-down" onPress={() => onMove(1)} disabled={index === total - 1} label="Sposta giù" caption="Giù" />
          <IconBtn name="copy-outline" onPress={onDuplicate} label="Duplica" caption="Duplica" />
          <IconBtn name="trash-outline" onPress={onDelete} disabled={!canDelete} label="Elimina domanda" color={C.danger} />
        </View>
      </View>
    </Card>
  );
}

const STEPS = ["Dettagli", "Domande"];

function Stepper({ step, onStep, compact }) {
  return (
    <View style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
      {STEPS.map((label, i) => {
        const n = i + 1;
        const active = n === step;
        const done = n < step;
        return (
          <View key={label} style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
            {i > 0 && <View style={{ width: compact ? 12 : 28, height: 2, backgroundColor: done || active ? C.primary : C.border }} />}
            <TouchableOpacity onPress={() => onStep(n)} disabled={active} style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
              <View style={{ width: 26, height: 26, borderRadius: 13, alignItems: "center", justifyContent: "center",
                backgroundColor: active || done ? C.primary : C.border }}>
                {done ? <Ionicons name="checkmark" size={16} color={C.white} />
                  : <Text style={{ color: active ? C.white : C.textMuted, fontWeight: "700", fontSize: 12 }}>{n}</Text>}
              </View>
              {!compact && <Text style={{ color: active ? C.text : C.textMuted, fontWeight: active ? "700" : "500" }}>{label}</Text>}
            </TouchableOpacity>
          </View>
        );
      })}
    </View>
  );
}

// Passo 1: titolo, descrizione e numero di studenti
function DetailsStep({ draft, onChange, onNext, onDiscard }) {
  return (
    <Card style={{ padding: 28, gap: 18 }}>
      <View>
        <Text style={{ color: C.text, fontSize: 22, fontWeight: "800" }}>Nuovo test</Text>
        <Text style={[s.itemSub, { marginTop: 4 }]}>Inizia dai dettagli: potrai modificarli in qualsiasi momento.</Text>
      </View>
      <View>
        <Text style={s.label}>Titolo del test</Text>
        <TextInput style={[field, { fontSize: 18, fontWeight: "700" }]} placeholder="Es. Verifica di storia — Unità 3" placeholderTextColor={C.textLight}
          value={draft.title} onChangeText={(title) => onChange({ ...draft, title })} autoFocus onSubmitEditing={onNext} />
      </View>
      <View>
        <Text style={s.label}>Descrizione</Text>
        <TextInput style={[field, { minHeight: 110, textAlignVertical: "top" }]} placeholder="Istruzioni o note per gli studenti (facoltativa)" placeholderTextColor={C.textLight}
          value={draft.description} onChangeText={(description) => onChange({ ...draft, description })} multiline />
      </View>
      <View>
        <Text style={s.label}>Numero di studenti</Text>
        <TextInput style={[field, { width: 120 }]} keyboardType="number-pad" value={String(draft.numberOfStudents)}
          onChangeText={(v) => onChange({ ...draft, numberOfStudents: v.replace(/[^0-9]/g, "") })} onSubmitEditing={onNext} />
      </View>
      <View style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginTop: 6, gap: 12 }}>
        <Btn label="Elimina bozza" variant="ghost" onPress={onDiscard} style={[s.smBtn, { borderColor: C.danger, backgroundColor: "transparent" }]} textStyle={[s.smBtnText, { color: C.danger }]} />
        <Btn label="Avanti: domande →" onPress={onNext} style={[s.smBtn, { paddingHorizontal: 20, paddingVertical: 10 }]} textStyle={s.smBtnText} />
      </View>
    </Card>
  );
}

export default function TestBuilder({ visible, contextLabel, draft, onChange, onSave, onClose, onDiscard, saving }) {
  const { isDesktop } = useLayout();
  const [selectedId, setSelectedId] = useState(null);
  if (!draft) return null;

  const step = draft.step ?? 1;
  const { totalPoints, totalQuestions, closedQuestions, openQuestions } = summarize(draft);
  const questions = draft.questions;
  const selectedIndex = Math.max(0, questions.findIndex(q => q.id === selectedId));
  const selected = questions[selectedIndex];

  // Si passa alle domande solo con i dettagli a posto; tornare indietro è sempre possibile.
  function goTo(n) {
    if (n > 1) {
      const err = validateDetails(draft);
      if (err) return notify("Attenzione", err);
    }
    onChange({ ...draft, step: n });
  }

  const setQuestion = (id, next) => onChange({ ...draft, questions: questions.map(q => q.id === id ? next : q) });
  const addQuestion = (type = "single") => {
    const q = { ...newQuestion(), type };
    onChange({ ...draft, questions: [...questions, q] });
    setSelectedId(q.id);
  };
  const duplicate = (q) => {
    const opts = q.options.map(o => ({ ...o, id: uid() }));
    const correct = q.correct.map(c => opts[q.options.findIndex(o => o.id === c)]?.id).filter(Boolean);
    const at = questions.findIndex(x => x.id === q.id) + 1;
    const copy = { ...q, id: uid(), options: opts, correct };
    onChange({ ...draft, questions: [...questions.slice(0, at), copy, ...questions.slice(at)] });
    setSelectedId(copy.id);
  };
  const remove = (id) => {
    const i = questions.findIndex(q => q.id === id);
    const rest = questions.filter(q => q.id !== id);
    onChange({ ...draft, questions: rest });
    setSelectedId(rest[Math.min(i, rest.length - 1)]?.id ?? null);
  };
  const reorder = (from, to) => {
    const next = questions.slice();
    next.splice(to, 0, ...next.splice(from, 1));
    onChange({ ...draft, questions: next });
  };
  const move = (id, dir) => {
    const i = questions.findIndex(q => q.id === id);
    const j = i + dir;
    if (j >= 0 && j < questions.length) reorder(i, j);
  };

  const questionsPanel = (
    <View style={{ padding: 20, gap: 14 }}>
      <TouchableOpacity onPress={() => goTo(1)} accessibilityLabel="Modifica dettagli"
        style={{ padding: 12, borderRadius: 10, backgroundColor: C.bg, flexDirection: "row", alignItems: "flex-start", gap: 10 }}>
        <View style={{ flex: 1 }}>
          <Text style={{ color: C.text, fontWeight: "800", fontSize: 16 }} numberOfLines={2}>{draft.title}</Text>
          {!!draft.description.trim() && <Text style={[s.itemSub, { marginTop: 2 }]} numberOfLines={2}>{draft.description}</Text>}
          <Text style={[s.itemSub, { marginTop: 2 }]}>{draft.numberOfStudents} studenti</Text>
        </View>
        <Ionicons name="create-outline" size={20} color={C.primary} />
      </TouchableOpacity>

      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {[[totalQuestions, "domande"], [closedQuestions, "chiuse"], [openQuestions, "aperte"], [totalPoints, "punti"]].map(([n, l]) => (
          <View key={l} style={{ flexGrow: 1, alignItems: "center", paddingVertical: 8, borderRadius: 10, backgroundColor: C.bg }}>
            <Text style={{ fontSize: 18, fontWeight: "800", color: C.text }}>{n}</Text>
            <Text style={s.itemSub}>{l}</Text>
          </View>
        ))}
      </View>

      <View>
        <Text style={[s.label, { marginTop: 6, marginBottom: 2 }]}>Domande</Text>
        <Text style={s.itemSub}>Trascina la maniglia a sinistra per cambiare l'ordine</Text>
      </View>
      <DraggableList data={questions} keyExtractor={q => q.id} onReorder={reorder}
        renderItem={({ item: q, index: i, dragHandleProps, dragging }) => {
          const active = q.id === selected?.id;
          const issue = questionIssue(q, i);
          const type = QUESTION_TYPES.find(t => t.id === q.type);
          return (
            <View style={{ flexDirection: "row", alignItems: "center", borderRadius: 10, borderWidth: 1,
              borderColor: active || dragging ? C.primary : C.border, backgroundColor: active ? "#E0EEF8" : C.white }}>
              <View {...dragHandleProps} accessibilityLabel="Trascina per riordinare"
                style={{ alignSelf: "stretch", justifyContent: "center", paddingLeft: 8, paddingRight: 2, cursor: dragging ? "grabbing" : "grab" }}>
                <Ionicons name="reorder-three" size={22} color={C.textLight} />
              </View>
              <TouchableOpacity onPress={() => setSelectedId(q.id)} style={{ flex: 1, flexDirection: "row", alignItems: "center", gap: 10, padding: 10, paddingLeft: 6 }}>
                <View style={{ width: 26, height: 26, borderRadius: 13, alignItems: "center", justifyContent: "center", backgroundColor: active ? C.primary : C.border }}>
                  <Text style={{ color: active ? C.white : C.textMuted, fontWeight: "700", fontSize: 12 }}>{i + 1}</Text>
                </View>
                <View style={{ flex: 1 }}>
                  <Text style={{ color: q.text.trim() ? C.text : C.textLight, fontWeight: "600" }} numberOfLines={1}>{q.text.trim() || "Senza testo"}</Text>
                  <Text style={s.itemSub}>{type?.label} · {Number(q.points) || 0} pt{q.required ? " · obbligatoria" : ""}</Text>
                </View>
                {issue && <Ionicons name="alert-circle" size={18} color={C.warning} />}
              </TouchableOpacity>
            </View>
          );
        }} />

      <Text style={[s.label, { marginTop: 6, marginBottom: 0 }]}>Aggiungi domanda</Text>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        {QUESTION_TYPES.map(t => (
          <TouchableOpacity key={t.id} onPress={() => addQuestion(t.id)}
            style={{ flexDirection: "row", alignItems: "center", gap: 6, paddingVertical: 8, paddingHorizontal: 12, borderRadius: 10, borderWidth: 1, borderColor: C.primary }}>
            <Ionicons name="add" size={16} color={C.primary} />
            <Text style={{ color: C.primary, fontWeight: "600" }}>{t.label}</Text>
          </TouchableOpacity>
        ))}
      </View>

      <Btn label="Elimina bozza" variant="ghost" onPress={onDiscard} style={{ marginTop: 10, borderColor: C.danger }} textStyle={{ color: C.danger }} />
    </View>
  );

  const editor = selected ? (
    <QuestionEditor key={selected.id} q={selected} index={selectedIndex} total={questions.length} canDelete={questions.length > 1}
      onChange={(next) => setQuestion(selected.id, next)}
      onDuplicate={() => duplicate(selected)}
      onDelete={() => remove(selected.id)}
      onMove={(dir) => move(selected.id, dir)} />
  ) : null;

  const actionBtn = { style: [s.smBtn, { paddingHorizontal: 20, paddingVertical: 10 }], textStyle: s.smBtnText };

  return (
    <Modal visible={visible} animationType="fade" onRequestClose={onClose}>
      <View style={{ flex: 1, backgroundColor: C.bg }}>
        {/* Barra superiore */}
        <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", backgroundColor: C.white, paddingHorizontal: isDesktop ? 28 : 16, height: 64, borderBottomWidth: 1, borderBottomColor: C.border, gap: 12 }}>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 12, flex: 1 }}>
            <TouchableOpacity onPress={onClose} accessibilityLabel="Chiudi"><Ionicons name="arrow-back" size={26} color={C.text} /></TouchableOpacity>
            <View style={{ flex: 1 }}>
              <Text style={{ color: C.text, fontSize: 18, fontWeight: "800" }} numberOfLines={1}>{draft.title || "Nuovo test"}</Text>
              {!!contextLabel && <Text style={{ color: C.textMuted, fontSize: 12 }} numberOfLines={1}>{contextLabel} · bozza salvata automaticamente</Text>}
            </View>
          </View>
          <Stepper step={step} onStep={goTo} compact={!isDesktop} />
          <View style={{ flex: isDesktop ? 1 : 0, alignItems: "flex-end" }}>
            {step === 1
              ? <Btn label="Avanti" onPress={() => goTo(2)} {...actionBtn} />
              : <Btn label="Salva test" onPress={onSave} loading={saving} {...actionBtn} />}
          </View>
        </View>

        {step === 1 ? (
          <ScrollView contentContainerStyle={{ padding: isDesktop ? 40 : 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
            <View style={{ width: "100%", maxWidth: 640 }}>
              <DetailsStep draft={draft} onChange={onChange} onNext={() => goTo(2)} onDiscard={onDiscard} />
            </View>
          </ScrollView>
        ) : isDesktop ? (
          <View style={{ flex: 1, flexDirection: "row" }}>
            <ScrollView style={{ width: 380, flexGrow: 0, backgroundColor: C.white, borderRightWidth: 1, borderRightColor: C.border }} keyboardShouldPersistTaps="handled">
              {questionsPanel}
            </ScrollView>
            <ScrollView style={{ flex: 1 }} contentContainerStyle={{ padding: 32, alignItems: "center" }} keyboardShouldPersistTaps="handled">
              <View style={{ width: "100%", maxWidth: 860 }}>{editor}</View>
            </ScrollView>
          </View>
        ) : (
          <ScrollView keyboardShouldPersistTaps="handled" contentContainerStyle={{ paddingBottom: 32 }}>
            <View style={{ backgroundColor: C.white, borderBottomWidth: 1, borderBottomColor: C.border }}>{questionsPanel}</View>
            <View style={{ padding: 16 }}>{editor}</View>
          </ScrollView>
        )}
      </View>
    </Modal>
  );
}

