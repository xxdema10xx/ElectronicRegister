// src/components/TestBuilder.js
// Creazione guidata di un esame, a schermo intero:
//   1. Dettagli — titolo, descrizione, classe e materia, finestra di disponibilità, durata, soglia, recupero;
//   2. Domande — a sinistra l'indice delle domande (si selezionano e si riordinano trascinandole),
//      a destra l'editor ampio della domanda scelta. Su schermi stretti i due pannelli si impilano;
//   3. Studenti — chi deve svolgere l'esame, con eventuale tempo aggiuntivo.
// Il passo corrente è salvato nella bozza (draft.step), così si riprende da dove si era rimasti.
import { useEffect, useState } from "react";
import { ScrollView, Switch, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { useLayout } from "../hooks/useLayout";
import { notify } from "../utils/dialogs";
import Btn from "./Btn";
import Card from "./Card";
import DraggableList from "./DraggableList";
import Input from "./Input";
import SelectField from "./SelectField";
import CheckboxField from "./CheckboxField";
import DateTimeField from "./DateTimeField";
import Loader from "./Loader";
import StatusChip from "./StatusChip";
import { filterBySearch } from "../utils/search";

export const QUESTION_TYPES = [
  { id: "single", label: "Singola", closed: true, icon: "radio-button-on-outline", hint: "Una sola risposta giusta" },
  { id: "multiple", label: "Multipla", closed: true, icon: "checkbox-outline", hint: "Una o più risposte giuste" },
  { id: "open", label: "Aperta", closed: false, icon: "create-outline", hint: "Risposta scritta" },
];

const uid = () => Math.random().toString(36).slice(2, 9);
// Esempi nei segnaposto delle opzioni, coerenti con l'esempio del testo della domanda
const OPTION_EXAMPLES = ["POST", "GET", "PUT", "DELETE"];
const isClosed = (type) => QUESTION_TYPES.find(t => t.id === type)?.closed;

export const newOption = () => ({ id: uid(), text: "" });
export const newQuestion = () => ({
  id: uid(), text: "", type: "single", options: [newOption(), newOption()],
  correct: [], points: "1", required: false,
});
export const newTestDraft = () => ({
  step: 1, title: "", description: "", classSubjectId: "", availableFrom: "", availableUntil: "",
  durationMinutes: "30", passingPercent: "60", retakeOfExamId: "", students: [], questions: [newQuestion()],
});

// La bozza ha qualcosa da salvare o da riprendere? Classe e materia (scelte prima dell'editor), durata,
// soglia e le due opzioni vuote predefinite non contano: solo ciò che il docente ha scritto o impostato.
export function draftHasContent(d) {
  if (!d) return false;
  if (d.title?.trim() || d.description?.trim() || d.availableFrom || d.availableUntil) return true;
  return (d.questions || []).some(q => q.text?.trim() || q.options?.some(o => o.text?.trim()));
}

// Conteggi da salvare insieme al test.
export function summarize(draft) {
  const qs = draft.questions;
  const closed = qs.filter(q => isClosed(q.type)).length;
  return {
    totalQuestions: qs.length, closedQuestions: closed, openQuestions: qs.length - closed,
    totalPoints: qs.reduce((sum, q) => sum + (Number(q.points) || 0), 0),
  };
}

// Problemi di una domanda, un messaggio per campo (mostrati inline nell'editor):
//   text    → manca il testo della domanda
//   options → mancano le opzioni di risposta (servono almeno due con testo)
//   correct → manca la risposta corretta (o quella segnata è vuota)
//   points  → punteggio non valido
// Restituisce {} se la domanda è a posto.
export function questionIssues(q) {
  const out = {};
  if (!q.text.trim()) out.text = "Manca il testo della domanda.";
  if (isClosed(q.type)) {
    const filled = q.options.filter(o => o.text.trim());
    if (filled.length < 2) {
      out.options = filled.length === 0 ? "Mancano le risposte: scrivi almeno due opzioni." : "Manca una risposta: servono almeno due opzioni con testo.";
    }
    if (!q.correct.some(id => filled.some(o => o.id === id))) {
      out.correct = q.correct.length > 0 ? "L'opzione segnata come giusta è vuota: scrivi il testo o segnane un'altra."
        : q.type === "multiple" ? "Manca la risposta corretta: spunta almeno un'opzione giusta." : "Manca la risposta corretta: tocca il cerchio accanto all'opzione giusta.";
    }
  }
  if (!(Number(q.points) > 0)) out.points = "Il punteggio deve essere maggiore di zero.";
  return out;
}

const ISSUE_ORDER = ["text", "options", "correct", "points"];
const ISSUE_SUMMARY = {
  text: (n) => `Nella domanda ${n} manca il testo.`,
  options: (n) => `Nella domanda ${n} mancano delle risposte: servono almeno due opzioni con testo.`,
  correct: (n) => `Nella domanda ${n} manca la risposta corretta.`,
  points: (n) => `Nella domanda ${n} il punteggio deve essere maggiore di zero.`,
};

// Primo problema di una domanda (testo da mostrare) oppure null se è a posto.
function questionIssue(q, i) {
  const issues = questionIssues(q);
  const key = ISSUE_ORDER.find(k => issues[k]);
  return key ? ISSUE_SUMMARY[key](i + 1) : null;
}

// Controlli del primo passo della procedura (dettagli dell'esame).
export function validateDetails(d) {
  if (!d.title.trim()) return "Inserisci il titolo dell'esame.";
  if (!d.classSubjectId) return "Seleziona la classe e la materia.";
  const from = new Date(d.availableFrom);
  const until = new Date(d.availableUntil);
  if (!d.availableFrom || isNaN(from)) return "Imposta l'inizio della disponibilità.";
  if (!d.availableUntil || isNaN(until)) return "Imposta la fine della disponibilità.";
  if (!(from < until)) return "L'inizio della disponibilità deve precedere la fine.";
  const duration = Number(d.durationMinutes);
  if (!(duration > 5)) return "La durata deve essere maggiore di 5 minuti.";
  if (duration * 60000 > until - from) return "La durata non può superare la finestra di disponibilità.";
  const pass = Number(d.passingPercent);
  if (!(pass > 0 && pass < 100)) return "La soglia di sufficienza deve essere maggiore di 0 e minore di 100.";
  return null;
}

// Per pubblicare servono anche gli studenti assegnati
export function validateForPublish(d) {
  return validateTestDraft(d) || (d.students.length === 0 ? "Assegna l'esame ad almeno uno studente." : null);
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

// Gruppo di campi con titolo e testo d'aiuto: separa concetti diversi (es. disponibilità e durata)
function FieldGroup({ icon, title, hint, children }) {
  return (
    <View style={{ borderWidth: 1, borderColor: C.border, borderRadius: 12, padding: 14, paddingBottom: 0, marginBottom: 14 }}>
      <View style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
        <Ionicons name={icon} size={18} color={C.primary} />
        <Text style={{ color: C.text, fontWeight: "700", fontSize: 15 }}>{title}</Text>
      </View>
      {!!hint && <Text style={[s.itemSub, { marginTop: 4, marginBottom: 12 }]}>{hint}</Text>}
      {children}
    </View>
  );
}

// Minuti tra due valori "AAAA-MM-GGTHH:mm" (null se non validi)
function windowMinutes(from, until) {
  const a = new Date(from), b = new Date(until);
  return from && until && !isNaN(a) && !isNaN(b) && b > a ? Math.round((b - a) / 60000) : null;
}
const formatMinutes = (m) => m >= 60 ? `${Math.floor(m / 60)} h${m % 60 ? ` ${m % 60} min` : ""}` : `${m} min`;

// Motivo per cui uno studente è (o non è) proposto per il recupero
const RETAKE_REASON = {
  failed: { label: "Non superato", tone: "red" },
  missing: { label: "Non consegnato", tone: "amber" },
  pending: { label: "In correzione", tone: "gray" },
  open: { label: "Può ancora consegnare", tone: "gray" },
};

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
  // Gli errori compaiono su un campo dopo averlo lasciato; riaprendo una domanda già iniziata, subito.
  // Una domanda appena aggiunta e ancora vuota non mostra errori.
  const pristine = !q.text.trim() && q.options.every(o => !o.text.trim()) && q.correct.length === 0;
  const [touched, setTouched] = useState(() => (pristine ? {} : { text: true, options: true, correct: true, points: true }));
  const touch = (key) => setTouched(t => (t[key] ? t : { ...t, [key]: true }));
  const issues = questionIssues(q);
  // La risposta corretta si segnala appena le opzioni sono a posto, senza aspettare un tocco sul cerchio.
  const err = (key) => (touched[key] || (key === "correct" && touched.options && !issues.options) ? issues[key] : null);

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
      <TextInput style={[field, { fontSize: 18, minHeight: 80, textAlignVertical: "top" }, err("text") && s.inputError]} placeholder="Es. Quale metodo HTTP si usa per creare una nuova risorsa?"
        placeholderTextColor={C.textLight} value={q.text} onChangeText={(text) => set({ text })} onBlur={() => touch("text")} multiline />
      {!!err("text") && <Text style={s.errorText}>{err("text")}</Text>}

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
          {!!err("correct") && (
            <View style={{ flexDirection: "row", alignItems: "center", gap: 6, marginBottom: 8 }}>
              <Ionicons name="alert-circle" size={16} color={C.danger} />
              <Text style={[s.errorText, { marginTop: 0, flex: 1 }]}>{err("correct")}</Text>
            </View>
          )}
          {q.options.map((o, i) => {
            const correct = q.correct.includes(o.id);
            const emptyError = !!err("options") && !o.text.trim();
            return (
              <View key={o.id} style={{ flexDirection: "row", alignItems: "center", gap: 10, marginBottom: 8 }}>
                <TouchableOpacity onPress={() => { toggleCorrect(o.id); touch("correct"); }} accessibilityLabel="Segna come giusta">
                  <Ionicons name={correct ? (multi ? "checkbox" : "radio-button-on") : (multi ? "square-outline" : "radio-button-off")}
                    size={28} color={correct ? C.success : C.textLight} />
                </TouchableOpacity>
                <TextInput style={[field, { flex: 1 }, correct && { borderColor: C.success, backgroundColor: "#ECFDF5" }, emptyError && s.inputError]} placeholder={`Opzione ${i + 1} — es. ${OPTION_EXAMPLES[i] || "un'altra risposta"}`}
                  placeholderTextColor={C.textLight} value={o.text} onChangeText={(text) => setOption(o.id, text)} onBlur={() => touch("options")}
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
          {!!err("options") && <Text style={s.errorText}>{err("options")}</Text>}
        </View>
      ) : (
        <View style={{ marginTop: 20, padding: 16, borderRadius: 10, borderWidth: 1, borderStyle: "dashed", borderColor: C.border }}>
          <Text style={{ color: C.textMuted }}>Lo studente scriverà la risposta in un campo di testo libero. Va corretta a mano.</Text>
        </View>
      )}

      <View style={{ flexDirection: "row", alignItems: "center", marginTop: 24, paddingTop: 16, borderTopWidth: 1, borderTopColor: C.border, gap: 16, flexWrap: "wrap" }}>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 8 }}>
          <TextInput style={[field, { width: 72, textAlign: "center" }, err("points") && s.inputError]} keyboardType="number-pad" value={String(q.points)} placeholder="Es. 2" placeholderTextColor={C.textLight}
            onChangeText={(v) => set({ points: v.replace(/[^0-9]/g, "") })} onBlur={() => touch("points")} />
          <Text style={{ color: C.text }}>punti</Text>
          {!!err("points") && <Text style={[s.errorText, { marginTop: 0 }]}>{err("points")}</Text>}
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

const STEPS = ["Dettagli", "Domande", "Studenti"];

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

const twoCols = (isDesktop) => ({ flexDirection: isDesktop ? "row" : "column", gap: isDesktop ? 16 : 0 });

// Passo 1: dettagli dell'esame
function DetailsStep({ draft, onChange, onNext, onDiscard, env, isDesktop }) {
  const set = (patch) => onChange({ ...draft, ...patch });
  const locked = !!draft.examId; // la classe-materia di un esame esistente non si può cambiare
  const classSubjects = env?.classSubjects;
  const hasList = Array.isArray(classSubjects) && classSubjects.length > 0;
  const isRetake = !!draft.retakeOfExamId || !!draft.wantsRetake;
  const retakeOptions = (env?.retakeOptions || []).filter(e => e.id !== draft.examId && (!e.classSubjectId || e.classSubjectId === draft.classSubjectId));

  return (
    <Card style={{ padding: 28, gap: 6 }}>
      <View style={{ marginBottom: 12 }}>
        <Text style={{ color: C.text, fontSize: 22, fontWeight: "800" }}>{draft.examId ? "Modifica esame" : "Nuovo esame"}</Text>
        <Text style={[s.itemSub, { marginTop: 4 }]}>Inizia dai dettagli: potrai modificarli finché l'esame è in bozza.</Text>
      </View>

      <Input label="Titolo dell'esame" value={draft.title} onChangeText={(title) => set({ title })} placeholder="Es. Programmazione Web – Verifica modulo 2" autoFocus />
      <View>
        <Text style={s.label}>Descrizione</Text>
        <TextInput style={[field, { minHeight: 90, textAlignVertical: "top", marginBottom: 14 }]} placeholder="Facoltativa. Es. Rispondi a tutte le domande; per le aperte bastano 3-4 righe. Non è consentito il materiale." placeholderTextColor={C.textLight}
          value={draft.description} onChangeText={(description) => set({ description })} multiline />
      </View>

      {hasList && !draft.classSubjectId ? (
        // Solo per bozze vecchie, create prima che la scelta avvenisse fuori dall'editor
        <SelectField label="Classe e materia" value={draft.classSubjectId} options={classSubjects} getLabel={(c) => c.label}
          onSelect={(classSubjectId) => set({ classSubjectId, students: [], retakeOfExamId: "" })}
          placeholder="Es. SW-25A · Programmazione" emptyMessage="Nessuna classe disponibile" />
      ) : hasList ? (
        // Scelta prima di aprire l'editor (ExamsScreen): qui un'etichetta fissa, con "Cambia" che riporta
        // alla scelta (solo per un esame nuovo: per uno già salvato il server non permette di cambiarla).
        <View style={{ marginBottom: 14 }}>
          <Text style={s.label}>Classe e materia</Text>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 8, backgroundColor: C.bg, borderRadius: 10, paddingHorizontal: 12, paddingVertical: 10 }}>
            <Ionicons name="school-outline" size={18} color={C.textMuted} />
            <Text style={{ color: C.text, fontSize: 15, fontWeight: "600", flex: 1 }} numberOfLines={1}>{env?.classSubjectLabel || "—"}</Text>
            {!locked && !!env?.onChangeClassSubject && (
              <TouchableOpacity onPress={env.onChangeClassSubject} accessibilityRole="button" accessibilityLabel="Cambia classe e materia">
                <Text style={{ color: C.primary, fontWeight: "700" }}>Cambia</Text>
              </TouchableOpacity>
            )}
          </View>
        </View>
      ) : (
        <View>
          <Input label="ID classe-materia" value={draft.classSubjectId} editable={!locked} autoCapitalize="none"
            onChangeText={(classSubjectId) => set({ classSubjectId: classSubjectId.trim(), students: [] })}
            placeholder="Es. 3f2b8c1e-4a7d-4e9b-9c21-5d6f7a8b9c0d" />
          <Text style={[s.itemSub, { marginTop: -8, marginBottom: 14 }]}>
            Il server non offre ancora l'elenco delle classi-materia: inserisci l'identificativo a mano.
          </Text>
        </View>
      )}

      <FieldGroup icon="calendar-outline" title="Disponibilità"
        hint="Quando lo studente può aprire l'esame. Fuori da questa finestra non può iniziarlo.">
      <View style={twoCols(isDesktop)}>
        <View style={{ flex: 1 }}><DateTimeField label="Disponibile da" value={draft.availableFrom} onChange={(availableFrom) => set({ availableFrom })} /></View>
        <View style={{ flex: 1 }}><DateTimeField label="Disponibile fino a" value={draft.availableUntil} onChange={(availableUntil) => set({ availableUntil })} /></View>
      </View>
      </FieldGroup>

      <FieldGroup icon="time-outline" title="Durata"
        hint={`Minuti a disposizione dello studente da quando apre l'esame: allo scadere viene consegnato in automatico.${
          windowMinutes(draft.availableFrom, draft.availableUntil) ? ` Deve stare nella finestra di disponibilità (${formatMinutes(windowMinutes(draft.availableFrom, draft.availableUntil))}).` : ""
        } Il tempo aggiuntivo per singolo studente si imposta nel passo Studenti.`}>
        <Input label="Durata (minuti)" value={String(draft.durationMinutes)} keyboardType="number-pad" placeholder="Es. 45"
          onChangeText={(v) => set({ durationMinutes: v.replace(/[^0-9]/g, "") })} />
      </FieldGroup>

      <Input label="Soglia di sufficienza (%)" value={String(draft.passingPercent)} keyboardType="number-pad" placeholder="Es. 60"
        onChangeText={(v) => set({ passingPercent: v.replace(/[^0-9]/g, "") })} />

      <CheckboxField label="Esame di recupero" value={isRetake} onChange={(v) => set({ wantsRetake: v, retakeOfExamId: v ? draft.retakeOfExamId : "" })} />
      {isRetake && (
        <View>
          <SelectField label="Recupero di" value={draft.retakeOfExamId} options={retakeOptions} getLabel={(e) => e.name}
            onSelect={(retakeOfExamId) => set({ retakeOfExamId })} placeholder="Seleziona l'esame originale"
            emptyMessage="Nessun esame della stessa classe e materia" />
          <Text style={[s.itemSub, { marginTop: -8, marginBottom: 14 }]}>
            Nel passo Studenti ti proporremo già spuntati gli studenti che non hanno superato o non hanno consegnato l'esame originale. Potrai togliere o aggiungere chi vuoi.
          </Text>
        </View>
      )}

      <View style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginTop: 6, gap: 12 }}>
        <Btn label="Elimina bozza" variant="ghost" onPress={onDiscard} style={[s.smBtn, { borderColor: C.danger, backgroundColor: "transparent" }]} textStyle={[s.smBtnText, { color: C.danger }]} />
        <Btn label="Avanti: domande →" onPress={onNext} style={[s.smBtn, { paddingHorizontal: 20, paddingVertical: 10 }]} textStyle={s.smBtnText} />
      </View>
    </Card>
  );
}

// Passo 3: studenti assegnati, con tempo aggiuntivo facoltativo
function StudentsStep({ draft, onChange, env }) {
  const [list, setList] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [search, setSearch] = useState("");

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    env.getStudents(draft.classSubjectId)
      .then((data) => { if (!cancelled) setList(Array.isArray(data) ? data : []); })
      .catch((e) => { if (!cancelled) setError(e.message); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [draft.classSubjectId]);

  // Recupero: chi non ha superato o non ha consegnato l'originale. Alla prima apertura del passo, se
  // nessuno è ancora assegnato, i candidati vengono spuntati; restano comunque modificabili a mano.
  const retakeOf = draft.retakeOfExamId;
  const [candidates, setCandidates] = useState(null); // Map studentId → { reason, proposed }
  const [candidatesError, setCandidatesError] = useState(null);
  useEffect(() => {
    let cancelled = false;
    setCandidates(null);
    setCandidatesError(null);
    if (!retakeOf || !env.getRetakeCandidates) return;
    env.getRetakeCandidates(retakeOf)
      .then((map) => { if (!cancelled) setCandidates(map); })
      .catch((e) => { if (!cancelled) setCandidatesError(e.message); });
    return () => { cancelled = true; };
  }, [retakeOf]);

  const proposedIds = candidates ? [...candidates].filter(([, c]) => c.proposed).map(([id]) => id) : [];
  useEffect(() => {
    if (!candidates || loading || draft.retakeSeededFor === retakeOf) return;
    const students = draft.students.length > 0 ? draft.students
      : list.filter(st => proposedIds.includes(st.id)).map(st => ({ studentId: st.id, firstName: st.firstName, lastName: st.lastName, extraTimeMinutes: 0 }));
    onChange({ ...draft, students, retakeSeededFor: retakeOf });
  }, [candidates, loading]);

  const selected = new Map(draft.students.map(st => [st.studentId, st]));
  const visible = filterBySearch(list, search, st => [st.firstName, st.lastName]);
  const setStudents = (students) => onChange({ ...draft, students });
  const asEntry = (st) => selected.get(st.id) || { studentId: st.id, firstName: st.firstName, lastName: st.lastName, extraTimeMinutes: 0 };
  const toggle = (st) => setStudents(selected.has(st.id) ? draft.students.filter(x => x.studentId !== st.id) : [...draft.students, asEntry(st)]);
  const setExtra = (id, v) => setStudents(draft.students.map(x => x.studentId === id ? { ...x, extraTimeMinutes: v.replace(/[^0-9]/g, "") } : x));

  return (
    <Card style={{ padding: 28, gap: 12 }}>
      <View>
        <Text style={{ color: C.text, fontSize: 22, fontWeight: "800" }}>Studenti</Text>
        <Text style={[s.itemSub, { marginTop: 4 }]}>
          {draft.students.length} assegnati. Il tempo aggiuntivo (in minuti) si somma alla durata per chi ne ha diritto.
        </Text>
      </View>
      <View style={{ flexDirection: "row", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
        <TextInput style={[s.input, { flex: 1, minWidth: 180 }]} placeholder="Cerca per nome…" placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
        <Btn label="Seleziona tutti" variant="ghost" onPress={() => setStudents(list.map(asEntry))} style={s.smBtn} textStyle={s.smBtnText} />
        <Btn label="Nessuno" variant="ghost" onPress={() => setStudents([])} style={s.smBtn} textStyle={s.smBtnText} />
        {proposedIds.length > 0 && (
          <Btn label={`Solo chi deve recuperare (${proposedIds.length})`} variant="ghost" style={s.smBtn} textStyle={s.smBtnText}
            onPress={() => setStudents(list.filter(st => proposedIds.includes(st.id)).map(asEntry))} />
        )}
      </View>

      {!!retakeOf && (
        <View style={{ backgroundColor: "#FEF3C7", borderRadius: 10, padding: 12 }}>
          <Text style={{ color: "#92400E" }}>
            {candidatesError ? `Non è stato possibile leggere gli esiti dell'esame originale: ${candidatesError}. Seleziona gli studenti a mano.`
              : !candidates ? "Sto leggendo gli esiti dell'esame originale…"
              : proposedIds.length === 0 ? "Esame di recupero: nessuno studente risulta da recuperare. Puoi comunque scegliere chi deve svolgerlo."
              : `Esame di recupero: ${proposedIds.length} ${proposedIds.length === 1 ? "studente non ha" : "studenti non hanno"} superato o consegnato l'esame originale e ${proposedIds.length === 1 ? "è proposto" : "sono proposti"} qui sotto. Puoi togliere o aggiungere chiunque della classe.`}
          </Text>
        </View>
      )}

      {loading ? <Loader /> : error ? <Text style={{ color: C.danger }}>{error}</Text> : visible.length === 0 ? (
        <Text style={{ color: C.textMuted }}>Nessuno studente trovato.</Text>
      ) : visible.map(st => {
        const on = selected.get(st.id);
        return (
          <View key={st.id} style={{ flexDirection: "row", alignItems: "center", gap: 10, paddingVertical: 6, borderBottomWidth: 1, borderBottomColor: C.border }}>
            <TouchableOpacity onPress={() => toggle(st)} style={{ flexDirection: "row", alignItems: "center", gap: 10, flex: 1 }}>
              <Ionicons name={on ? "checkbox" : "square-outline"} size={26} color={on ? C.primary : C.textLight} />
              <Text style={{ color: C.text, fontSize: 15 }}>{st.lastName} {st.firstName}</Text>
              {!!candidates?.get(st.id) && (
                <StatusChip label={RETAKE_REASON[candidates.get(st.id).reason].label} tone={RETAKE_REASON[candidates.get(st.id).reason].tone} style={{ alignSelf: "center" }} />
              )}
            </TouchableOpacity>
            {on && (
              <View style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
                <Text style={s.itemSub}>+</Text>
                <TextInput style={[field, { width: 64, textAlign: "center", paddingVertical: 6 }]} keyboardType="number-pad"
                  value={String(on.extraTimeMinutes || 0)} onChangeText={(v) => setExtra(st.id, v)} />
                <Text style={s.itemSub}>min</Text>
              </View>
            )}
          </View>
        );
      })}
    </Card>
  );
}

// draftStatusShort: { label, tone } dello stato per schermi stretti (es. "Non salvata").
// onClose non è più usato dalla barra: il ritorno alla lista è nell'header di AppNav (resta per compatibilità).
export default function TestBuilder({ visible = true, contextLabel, draft, onChange, onSave, onClose, onDiscard, saving, env, draftStatus, draftStatusShort }) {
  const { isDesktop } = useLayout();
  const [selectedId, setSelectedId] = useState(null);
  if (!draft || !visible) return null;

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
          <Text style={[s.itemSub, { marginTop: 2 }]}>{draft.students.length} studenti assegnati · {draft.durationMinutes} min</Text>
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
    // Pagina dell'app (non più un Modal): sta nell'area dei contenuti, con la sidebar visibile.
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={{ flex: 1 }}>
        {/* Barra superiore */}
        {/* Il titolo ("← Esami / Nuovo esame") è nell'header di AppNav: qui stato, Stepper e pulsanti */}
        <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", backgroundColor: C.white, paddingHorizontal: isDesktop ? 28 : 16, paddingVertical: 10, minHeight: 64, borderBottomWidth: 1, borderBottomColor: C.border, gap: 12 }}>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flex: 1, flexWrap: "wrap", minWidth: 0 }}>
            {/* Stato reale della bozza con accanto l'azione che lo cambia. Su schermi larghi va a capo se serve,
                su quelli stretti diventa un badge corto ("Non salvata") per non essere tagliato. */}
            {isDesktop ? (
              <Text style={{ color: C.textMuted, fontSize: 13, flexShrink: 1 }} numberOfLines={2}>
                {[contextLabel, draftStatus].filter(Boolean).join(" · ")}
              </Text>
            ) : !!draftStatusShort && (
              <StatusChip label={draftStatusShort.label} tone={draftStatusShort.tone} style={{ alignSelf: "center" }} />
            )}
            <TouchableOpacity onPress={onSave} disabled={saving} accessibilityRole="button" accessibilityLabel="Salva bozza"
              style={{ flexDirection: "row", alignItems: "center", gap: 4, paddingVertical: 4, paddingHorizontal: 8, borderRadius: 8, borderWidth: 1, borderColor: C.primary, opacity: saving ? 0.6 : 1 }}>
              <Ionicons name="cloud-upload-outline" size={14} color={C.primary} />
              <Text style={{ color: C.primary, fontSize: 12, fontWeight: "700" }}>{saving ? "Salvataggio…" : "Salva bozza"}</Text>
            </TouchableOpacity>
          </View>
          <Stepper step={step} onStep={goTo} compact={!isDesktop} />
          <View style={{ flex: isDesktop ? 1 : 0, alignItems: "flex-end" }}>
            {step === 1 && <Btn label="Avanti" onPress={() => goTo(2)} {...actionBtn} />}
            {step === 2 && (
              <View style={{ flexDirection: "row", gap: 8 }}>
                <Btn label="Salva" variant="ghost" onPress={onSave} loading={saving} {...actionBtn} />
                <Btn label="Avanti" onPress={() => goTo(3)} {...actionBtn} />
              </View>
            )}
            {step === 3 && <Btn label="Salva" onPress={onSave} loading={saving} {...actionBtn} />}
          </View>
        </View>

        {step === 1 ? (
          <ScrollView contentContainerStyle={{ padding: isDesktop ? 40 : 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
            <View style={{ width: "100%", maxWidth: 640 }}>
              <DetailsStep draft={draft} onChange={onChange} onNext={() => goTo(2)} onDiscard={onDiscard} env={env} isDesktop={isDesktop} />
            </View>
          </ScrollView>
        ) : step === 3 ? (
          <ScrollView contentContainerStyle={{ padding: isDesktop ? 40 : 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
            <View style={{ width: "100%", maxWidth: 720 }}>
              <StudentsStep draft={draft} onChange={onChange} env={env} />
              <Btn label="Elimina bozza" variant="ghost" onPress={onDiscard} style={{ marginTop: 14, borderColor: C.danger }} textStyle={{ color: C.danger }} />
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
    </View>
  );
}

