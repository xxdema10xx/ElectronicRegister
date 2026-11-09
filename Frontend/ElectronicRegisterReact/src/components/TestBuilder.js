// src/components/TestBuilder.js
// Creazione guidata di un esame, a schermo intero:
//   1. Dettagli — titolo, descrizione, classe e materia, finestra di disponibilità, durata, soglia, recupero;
//   2. Domande — a sinistra l'indice delle domande (si selezionano e si riordinano trascinandole),
//      a destra l'editor ampio della domanda scelta. Su schermi stretti i due pannelli si impilano;
//   3. Studenti — chi deve svolgere l'esame, con eventuale tempo aggiuntivo.
// Il passo corrente è salvato nella bozza (draft.step), così si riprende da dove si era rimasti.
import { useEffect, useRef, useState } from "react";
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
import InfoTip from "./InfoTip";
import { filterBySearch } from "../utils/search";
import { RETAKE_LABEL, RETAKE_TONE } from "../api/examInsights";

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
// Multipla: il server vuole almeno 2 risposte corrette (ExamService, "almeno due risposte corrette").
// TODO(API): in attesa di conferma dal backend che la regola sia voluta.
export const MULTIPLE_MIN_CORRECT = 2;

export function questionIssues(q) {
  const out = {};
  if (!q.text.trim()) out.text = "Manca il testo della domanda.";
  if (isClosed(q.type)) {
    const filled = q.options.filter(o => o.text.trim());
    if (filled.length < 2) out.options = "Manca un'opzione: servono almeno due opzioni con testo.";
    const correctFilled = q.correct.filter(id => filled.some(o => o.id === id)).length;
    if (q.type === "multiple" && correctFilled < MULTIPLE_MIN_CORRECT) {
      out.correct = "Una domanda a risposta multipla deve avere almeno 2 risposte corrette. Se ne serve una sola, usa 'Singola'.";
    } else if (q.type !== "multiple" && correctFilled === 0) {
      out.correct = q.correct.length > 0 ? "Manca la risposta corretta: l'opzione segnata come giusta è vuota."
        : "Manca la risposta corretta: tocca il cerchio accanto all'opzione giusta.";
    }
  }
  if (!(Number(q.points) > 0)) out.points = "Punti mancanti: assegna almeno 1 punto alla domanda.";
  return out;
}

const ISSUE_ORDER = ["text", "options", "correct", "points"];

// Primo problema di una domanda (testo da mostrare) oppure null se è a posto.
function questionIssue(q, i) {
  const issues = questionIssues(q);
  const key = ISSUE_ORDER.find(k => issues[k]);
  return key ? `Domanda ${i + 1}: ${issues[key]}` : null;
}

// ── Validazione ─────────────────────────────────────────────────────────────────────────────
// Un errore per voce: { step, field, message, questionIndex?, questionId? }. I livelli:
//   "server"  ciò che il server controlla già in creazione e modifica: senza, "Salva bozza" salva solo nel browser
//   "close"   "Salva e chiudi": tutto quello sopra più il titolo
//   "publish" pubblicazione: in più almeno uno studente assegnato
// Campi che il server richiede (ExamService: EnsureValidSchedule, EnsureValidPassingPercent, EnsureValidQuestions
// e le regole delle opzioni). TODO(API): quando il backend accetterà bozze incomplete, basterà svuotare o
// ridurre questo elenco.
export const SERVER_REQUIRED = new Set([
  "classSubjectId", "availableFrom", "availableUntil", "durationMinutes", "passingPercent",
  "questions", "text", "options", "correct", "points",
]);

export function validateAll(d, { level = "close" } = {}) {
  const out = [];
  const add = (step, field, message, extra) => out.push({ step, field, message, ...extra });
  if (!d.title?.trim()) add(1, "title", "Manca il titolo dell'esame.");
  if (!d.classSubjectId) add(1, "classSubjectId", "Mancano la classe e la materia.");
  const from = new Date(d.availableFrom);
  const until = new Date(d.availableUntil);
  const fromOk = !!d.availableFrom && !isNaN(from);
  const untilOk = !!d.availableUntil && !isNaN(until);
  if (!fromOk) add(1, "availableFrom", "Manca l'inizio della disponibilità.");
  if (!untilOk) add(1, "availableUntil", "Manca la fine della disponibilità.");
  if (fromOk && untilOk && !(from < until)) add(1, "availableUntil", "La fine della disponibilità deve venire dopo l'inizio.");
  const duration = Number(d.durationMinutes);
  if (!(duration > 5)) add(1, "durationMinutes", "La durata deve essere maggiore di 5 minuti.");
  else if (fromOk && untilOk && from < until && duration * 60000 > until - from) {
    add(1, "durationMinutes", "La durata non può superare la finestra di disponibilità.");
  }
  const pass = Number(d.passingPercent);
  if (!(pass > 0 && pass < 100)) add(2, "passingPercent", "La soglia di sufficienza deve essere tra 1 e 99%.");
  if (d.questions.length === 0) add(2, "questions", "Aggiungi almeno una domanda.");
  d.questions.forEach((q, i) => {
    const issues = questionIssues(q);
    for (const key of ISSUE_ORDER) {
      if (issues[key]) add(2, key, `Domanda ${i + 1}: ${issues[key]}`, { questionIndex: i, questionId: q.id });
    }
  });
  if (level === "publish" && d.students.length === 0) add(3, "students", "Assegna l'esame ad almeno uno studente.");
  return level === "server" ? out.filter(e => SERVER_REQUIRED.has(e.field)) : out;
}

const firstMessage = (list) => list[0]?.message || null;
// Funzioni di prima (primo messaggio o null), ora basate su validateAll
export const validateDetails = (d) => firstMessage(validateAll(d).filter(e => e.step === 1));
export const validateTestDraft = (d) => firstMessage(validateAll(d));
export const validateForPublish = (d) => firstMessage(validateAll(d, { level: "publish" }));
export const serverIssue = (d) => firstMessage(validateAll(d, { level: "server" }));

// Banner in cima all'editor. kind "error" (rosso, blocca l'azione) o "info" (giallo, "Salva bozza" riuscito
// solo nel browser). Le voci si ricalcolano mentre si corregge; il banner sparisce quando non ne restano.
// Si distinguono le voci che bloccano anche il server da quelle che servono solo per "Salva e chiudi".
function ErrorBanner({ notice, items, onPick, onDismiss }) {
  if (!notice || items.length === 0) return null;
  const info = notice.kind === "info";
  const [bg, border, fg] = info ? ["#FEF3C7", "#FCD34D", "#92400E"] : ["#FEE2E2", "#FCA5A5", "#991B1B"];
  const serverItems = items.filter(e => SERVER_REQUIRED.has(e.field));
  const otherItems = items.filter(e => !SERVER_REQUIRED.has(e.field));
  const groups = info || otherItems.length === 0 || serverItems.length === 0
    ? [{ key: "all", title: null, list: items }]
    : [{ key: "server", title: "Bloccano anche il salvataggio sul server", list: serverItems }, { key: "close", title: "Servono per salvare e chiudere", list: otherItems }];
  return (
    <View testID="editor-banner" accessibilityRole={info ? undefined : "alert"} accessibilityLiveRegion="polite" style={{ backgroundColor: bg, borderBottomWidth: 1, borderBottomColor: border, paddingHorizontal: 16, paddingVertical: 12 }}>
      <View style={{ flexDirection: "row", alignItems: "flex-start", gap: 8 }}>
        <Ionicons name={info ? "information-circle-outline" : "alert-circle-outline"} size={20} color={fg} />
        <View style={{ flex: 1 }}>
          <Text style={{ color: fg, fontWeight: "800" }}>{notice.title} ({items.length})</Text>
          {!!notice.subtitle && <Text style={{ color: fg, marginTop: 2 }}>{notice.subtitle}</Text>}
          {groups.map(g => (
            <View key={g.key} style={{ marginTop: 6 }}>
              {!!g.title && <Text style={{ color: fg, fontSize: 12, fontWeight: "700", textTransform: "uppercase", marginTop: 4 }}>{g.title}</Text>}
              {g.list.map((e, i) => (
                <TouchableOpacity key={`${e.field}-${e.questionId || ""}-${i}`} onPress={() => onPick(e)} accessibilityRole="link"
                  accessibilityLabel={`${e.message} Vai al campo`} style={{ flexDirection: "row", alignItems: "center", gap: 6, paddingVertical: 3 }}>
                  <Text style={{ color: fg }}>•</Text>
                  <Text style={{ color: fg, textDecorationLine: "underline", flexShrink: 1 }}>{e.message}</Text>
                  <Text style={{ color: fg, fontSize: 12 }}>{`(step ${e.step})`}</Text>
                </TouchableOpacity>
              ))}
            </View>
          ))}
        </View>
        <TouchableOpacity onPress={onDismiss} accessibilityRole="button" accessibilityLabel="Chiudi l'avviso" style={{ padding: 2 }}>
          <Ionicons name="close" size={18} color={fg} />
        </TouchableOpacity>
      </View>
    </View>
  );
}

// Mette il focus sul campo indicato (focus = { field, n }) quando cambia n: usato dal banner
function useFocusField(focus, refs) {
  useEffect(() => {
    if (!focus) return;
    const target = refs[focus.field]?.current;
    if (target?.focus) setTimeout(() => target.focus(), 50);
  }, [focus?.n]);
}

export function passingPointsLabel(totalPoints, percent) {
  const p = Number(percent);
  if (!(totalPoints > 0) || !(p > 0 && p < 100)) return null;
  const n = Math.round(totalPoints * p) / 100; // punti esatti
  const shown = Math.round(n * 10) / 10;
  return `= ${String(shown).replace(".", ",")} punti su ${String(totalPoints).replace(".", ",")}`;
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

// Esito di uno studente nell'esame originale: stesse etichette di "Chi deve recuperare" e della Home
const RETAKE_REASON = Object.fromEntries(Object.keys(RETAKE_LABEL).map(k => [k, { label: RETAKE_LABEL[k], tone: RETAKE_TONE[k] }]));

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
// reveal: mostra subito tutti gli errori (c'è il banner); focus: { field, n } dal banner
function QuestionEditor({ q, index, total, onChange, onDuplicate, onDelete, onMove, canDelete, reveal, focus }) {
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
  const err = (key) => (reveal || touched[key] || (key === "correct" && touched.options && !issues.options) ? issues[key] : null);
  const refs = { text: useRef(null), options: useRef(null), correct: useRef(null), points: useRef(null) };
  useFocusField(focus, refs);
  // Per "manca un'opzione" il focus va sulla prima opzione vuota
  const firstEmptyOptionId = q.options.find(o => !o.text.trim())?.id ?? q.options[0]?.id;

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
      <TextInput ref={refs.text} style={[field, { fontSize: 18, minHeight: 80, textAlignVertical: "top" }, err("text") && s.inputError]} placeholder="Es. Quale metodo HTTP si usa per creare una nuova risorsa?"
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
                <TouchableOpacity ref={i === 0 ? refs.correct : undefined} onPress={() => { toggleCorrect(o.id); touch("correct"); }} accessibilityLabel="Segna come giusta">
                  <Ionicons name={correct ? (multi ? "checkbox" : "radio-button-on") : (multi ? "square-outline" : "radio-button-off")}
                    size={28} color={correct ? C.success : C.textLight} />
                </TouchableOpacity>
                <TextInput ref={o.id === firstEmptyOptionId ? refs.options : undefined} style={[field, { flex: 1 }, correct && { borderColor: C.success, backgroundColor: "#ECFDF5" }, emptyError && s.inputError]} placeholder={`Opzione ${i + 1} — es. ${OPTION_EXAMPLES[i] || "un'altra risposta"}`}
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
          <TextInput ref={refs.points} style={[field, { width: 72, textAlign: "center" }, err("points") && s.inputError]} keyboardType="number-pad" value={String(q.points)} placeholder="Es. 2" placeholderTextColor={C.textLight}
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
const BAR_BTN_WIDTH = 150; // pulsanti della barra dell'editor su desktop
const BAR_WIDE_MIN = 720;

function Stepper({ step, onStep, compact }) {
  return (
    <View style={{ flexDirection: "row", flexWrap: "wrap", justifyContent: "center", alignItems: "center", gap: 8, maxWidth: "100%" }}>
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
              {(!compact || active) && <Text style={{ color: active ? C.text : C.textMuted, fontWeight: active ? "700" : "500" }}>{label}</Text>}
            </TouchableOpacity>
          </View>
        );
      })}
    </View>
  );
}

const twoCols = (isDesktop) => ({ flexDirection: isDesktop ? "row" : "column", gap: isDesktop ? 16 : 0 });

// Passo 1: dettagli dell'esame
// reveal: mostra gli errori dei dettagli sotto i campi (c'è il banner); focus: { field, n } dal banner
function DetailsStep({ draft, onChange, env, isDesktop, reveal, focus }) {
  const errors = {};
  if (reveal) for (const e of validateAll(draft).filter(x => x.step === 1)) errors[e.field] = errors[e.field] || e.message;
  const refs = { title: useRef(null), availableFrom: useRef(null), availableUntil: useRef(null), durationMinutes: useRef(null) };
  useFocusField(focus, refs);
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

      <Input ref={refs.title} label="Titolo dell'esame" value={draft.title} onChangeText={(title) => set({ title })} placeholder="Es. Programmazione Web – Verifica modulo 2" autoFocus error={errors.title} />
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
        <View style={{ flex: 1 }}><DateTimeField ref={refs.availableFrom} label="Disponibile da" value={draft.availableFrom} onChange={(availableFrom) => set({ availableFrom })} error={errors.availableFrom} /></View>
        <View style={{ flex: 1 }}><DateTimeField ref={refs.availableUntil} label="Disponibile fino a" value={draft.availableUntil} onChange={(availableUntil) => set({ availableUntil })} error={errors.availableUntil} /></View>
      </View>
      </FieldGroup>

      <FieldGroup icon="time-outline" title="Durata"
        hint={`Minuti a disposizione dello studente da quando apre l'esame: allo scadere viene consegnato in automatico.${
          windowMinutes(draft.availableFrom, draft.availableUntil) ? ` Deve stare nella finestra di disponibilità (${formatMinutes(windowMinutes(draft.availableFrom, draft.availableUntil))}).` : ""
        } Il tempo aggiuntivo per singolo studente si imposta nel passo Studenti.`}>
        <Input ref={refs.durationMinutes} error={errors.durationMinutes} label="Durata (minuti)" value={String(draft.durationMinutes)} keyboardType="number-pad" placeholder="Es. 45"
          onChangeText={(v) => set({ durationMinutes: v.replace(/[^0-9]/g, "") })} />
      </FieldGroup>

      <CheckboxField label="Esame di recupero" value={isRetake} onChange={(v) => set({ wantsRetake: v, retakeOfExamId: v ? draft.retakeOfExamId : "" })} />
      {isRetake && (
        <View>
          <SelectField label="Recupero di" value={draft.retakeOfExamId} options={retakeOptions} getLabel={(e) => e.name}
            onSelect={(retakeOfExamId) => set({ retakeOfExamId })} placeholder="Seleziona l'esame originale"
            emptyMessage="Nessun esame della stessa classe e materia" />
          {!!draft.retakeOfExamId && (
            <Text style={[s.itemSub, { marginTop: -8, marginBottom: 14 }]}>
              Nello step Studenti trovi già selezionati gli studenti che devono recuperare. Potrai togliere o aggiungere chi vuoi.
            </Text>
          )}
        </View>
      )}

    </Card>
  );
}

// Vero anche rispetto al server: la scadenza è inizio + durata + minuti aggiuntivi (ExamManager.StartAsync)
const EXTRA_TIME_INFO = "Il tempo aggiuntivo (in minuti) si somma alla durata dell'esame solo per gli studenti che ne hanno diritto. Esempio: durata 30 min + 10 aggiuntivi = 40 minuti per quello studente.";

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

  // Recupero: esiti dell'esame originale (examInsights.getRetakeCandidates, con cache). Si preselezionano
  // "Deve recuperare" e "Non ha consegnato" solo se l'originale è chiuso e ha consegne, una volta sola e solo
  // se nessuno è ancora assegnato; poi la scelta è del docente. retakeKnownIds: chi era già noto (proposto o
  // assegnato) quando si è aperto lo step, per proporre in seguito solo i NUOVI da recuperare.
  const retakeOf = draft.retakeOfExamId;
  const [candidates, setCandidates] = useState(null); // { closed, hasSubmissions, byStudent: Map }
  const [candidatesError, setCandidatesError] = useState(null);
  useEffect(() => {
    let cancelled = false;
    setCandidates(null);
    setCandidatesError(null);
    if (!retakeOf || !env.getRetakeCandidates) return;
    env.getRetakeCandidates(retakeOf)
      .then((res) => { if (!cancelled) setCandidates(res); })
      .catch((e) => { if (!cancelled) setCandidatesError(e.message); });
    return () => { cancelled = true; };
  }, [retakeOf]);

  const byStudent = candidates?.byStudent || new Map();
  const proposedIds = [...byStudent].filter(([, c]) => c.proposed).map(([id]) => id);
  useEffect(() => {
    if (!candidates || loading || draft.retakeSeededFor === retakeOf) return;
    const fresh = draft.students.length === 0;
    const students = fresh
      ? list.filter(st => proposedIds.includes(st.id)).map(st => ({ studentId: st.id, firstName: st.firstName, lastName: st.lastName, extraTimeMinutes: 0 }))
      : draft.students;
    const known = fresh ? proposedIds : draft.students.map(x => x.studentId);
    onChange({ ...draft, students, retakeSeededFor: retakeOf, retakeKnownIds: known });
  }, [candidates, loading]);
  const selected = new Map(draft.students.map(st => [st.studentId, st]));
  const visible = filterBySearch(list, search, st => [st.firstName, st.lastName]);
  const setStudents = (students) => onChange({ ...draft, students });
  const asEntry = (st) => selected.get(st.id) || { studentId: st.id, firstName: st.firstName, lastName: st.lastName, extraTimeMinutes: 0 };
  const toggle = (st) => setStudents(selected.has(st.id) ? draft.students.filter(x => x.studentId !== st.id) : [...draft.students, asEntry(st)]);
  const setExtra = (id, v) => setStudents(draft.students.map(x => x.studentId === id ? { ...x, extraTimeMinutes: v.replace(/[^0-9]/g, "") } : x));

  // Riepilogo: "5 selezionati: 3 da recuperare, 2 assenti" (+ altri aggiunti a mano)
  const reasonOf = (id) => byStudent.get(id)?.reason;
  const selectedIds = draft.students.map(x => x.studentId);
  const nFailed = selectedIds.filter(id => reasonOf(id) === "failed").length;
  const nMissing = selectedIds.filter(id => reasonOf(id) === "missing").length;
  const nOthers = selectedIds.length - nFailed - nMissing;
  const nPending = [...byStudent.values()].filter(c => c.reason === "pending").length;
  // Nuovi da recuperare (es. correzioni finite dopo): proposti, non assegnati e non già noti. Si propongono, non si applicano.
  const known = new Set(draft.retakeKnownIds || []);
  const newIds = draft.retakeSeededFor === retakeOf ? proposedIds.filter(id => !selected.has(id) && !known.has(id)) : [];
  const addNew = () => onChange({ ...draft, students: [...draft.students, ...list.filter(st => newIds.includes(st.id)).map(asEntry)],
    retakeKnownIds: [...known, ...newIds] });

  return (
    <Card style={{ padding: 28, gap: 12 }}>
      <View>
        <Text style={{ color: C.text, fontSize: 22, fontWeight: "800" }}>Studenti</Text>
        <Text style={[s.itemSub, { marginTop: 4 }]}>
          {draft.students.length} assegnati.
        </Text>
      </View>
      <View style={{ flexDirection: "row", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
        <TextInput style={[s.input, { flex: 1, minWidth: 180 }]} placeholder="Cerca per nome…" placeholderTextColor={C.textLight} value={search} onChangeText={setSearch} />
        {retakeOf && proposedIds.length > 0 && (
          <Btn label="Solo chi deve recuperare" variant="ghost" style={s.smBtn} textStyle={s.smBtnText}
            onPress={() => setStudents(list.filter(st => proposedIds.includes(st.id)).map(asEntry))} />
        )}
        <Btn label="Nessuno" variant="ghost" onPress={() => setStudents([])} style={s.smBtn} textStyle={s.smBtnText} />
        <Btn label="Seleziona tutti" variant="ghost" onPress={() => setStudents(list.map(asEntry))} style={s.smBtn} textStyle={s.smBtnText} />
      </View>

      {!!retakeOf && (
        <View testID="retake-summary" style={{ backgroundColor: "#FEF3C7", borderRadius: 10, padding: 12, gap: 4 }}>
          {candidatesError ? (
            <Text style={{ color: "#92400E" }}>Impossibile caricare le consegne dell'esame originale: {candidatesError} Seleziona gli studenti a mano.</Text>
          ) : !candidates ? (
            <Text style={{ color: "#92400E" }}>Sto leggendo gli esiti dell'esame originale…</Text>
          ) : !candidates.closed ? (
            <Text style={{ color: "#92400E" }}>L'esame originale non è ancora chiuso: chi deve recuperare si saprà dopo la chiusura. Per ora nessuno è preselezionato; puoi scegliere gli studenti a mano.</Text>
          ) : !candidates.hasSubmissions ? (
            <Text style={{ color: "#92400E" }}>L'esame originale non ha consegne: nessuno è preselezionato. Puoi scegliere gli studenti a mano.</Text>
          ) : (<>
            <Text style={{ color: "#92400E", fontWeight: "700" }}>
              {selectedIds.length} {selectedIds.length === 1 ? "selezionato" : "selezionati"}: {nFailed} da recuperare, {nMissing} {nMissing === 1 ? "assente" : "assenti"}{nOthers ? `, ${nOthers} ${nOthers === 1 ? "altro" : "altri"}` : ""}
            </Text>
            {nPending > 0 && (
              <Text style={{ color: "#92400E" }}>{nPending} in correzione: dopo la correzione {nPending === 1 ? "potrebbe" : "potrebbero"} dover recuperare.</Text>
            )}
            {newIds.length > 0 && (
              <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
                <Text style={{ color: "#92400E" }}>
                  {newIds.length === 1 ? "C'è 1 nuovo studente da recuperare" : `Ci sono ${newIds.length} nuovi studenti da recuperare`}:
                </Text>
                <Btn label={newIds.length === 1 ? "Aggiungi" : "Aggiungili"} variant="ghost" onPress={addNew} style={s.smBtn} textStyle={s.smBtnText} />
              </View>
            )}
            <Text style={{ color: "#92400E", fontSize: 12 }}>Puoi togliere o aggiungere chiunque della classe.</Text>
          </>)}
        </View>
      )}

      {!loading && !error && visible.length > 0 && (
        <View style={{ flexDirection: "row", justifyContent: "flex-end", alignItems: "center", gap: 4 }}>
          <Text style={[s.label, { marginBottom: 0 }]}>Tempo aggiuntivo</Text>
          <InfoTip text={EXTRA_TIME_INFO} />
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
              {!!byStudent.get(st.id) && (
                <StatusChip label={RETAKE_REASON[byStudent.get(st.id).reason].label} tone={RETAKE_REASON[byStudent.get(st.id).reason].tone} style={{ alignSelf: "center" }} />
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

// Azioni della barra (D1 = a: "Avanti" non salva sul server):
//   onSaveDraft     "Salva bozza" (secondaria): salva e resta nell'editor
//   onSaveAndClose  "Salva e chiudi" (principale allo step 3): validazione completa, poi torna alla lista
//   destroyAction   { label, onPress } per "Elimina bozza" / "Annulla modifiche", oppure null se non c'è niente
//                   da eliminare o annullare. Sempre con conferma; su schermi larghi sta in riga con Indietro e
//                   Avanti, su quelli stretti nel menu "⋯".
// draftStatusShort: { label, tone } dello stato per schermi stretti (es. "Non salvata").
// onClose non è più usato dalla barra: il ritorno alla lista è nell'header di AppNav (resta per compatibilità).
// notice: { kind: "error" | "info", level, title, subtitle? } per il banner (da ExamsScreen, dopo un salvataggio);
// onDismissNotice lo chiude. Le voci del banner si calcolano qui con validateAll(draft, { level }).
export default function TestBuilder({ visible = true, contextLabel, draft, onChange, onSaveDraft, onSaveAndClose, destroyAction, onClose, saving, env, draftStatus, draftStatusShort, notice, onDismissNotice }) {
  const { isDesktop, isWide, width } = useLayout();
  const wideBar = isDesktop && width >= BAR_WIDE_MIN; // sotto questa larghezza, con la sidebar, la barra usa la forma compatta
  const [selectedId, setSelectedId] = useState(null);
  const [moreOpen, setMoreOpen] = useState(false); // menu "⋯" su schermi stretti
  const [stepNotice, setStepNotice] = useState(null); // "Avanti" dallo step 1 con dettagli da completare
  const [focus, setFocus] = useState(null); // { field, questionId?, n }: campo su cui mettere il focus
  const passingRef = useRef(null);
  useFocusField(focus && focus.field === "passingPercent" ? focus : null, { passingPercent: passingRef });
  if (!draft || !visible) return null;

  const step = draft.step ?? 1;
  const { totalPoints, totalQuestions, closedQuestions, openQuestions } = summarize(draft);
  const questions = draft.questions;
  const selectedIndex = Math.max(0, questions.findIndex(q => q.id === selectedId));
  const selected = questions[selectedIndex];

  // Si passa alle domande solo con i dettagli a posto; tornare indietro è sempre possibile.
  // Con dettagli da completare: banner (solo voci dello step 1), errori sotto i campi, focus sul primo.
  function goTo(n) {
    if (n > 1) {
      const errs = validateAll(draft).filter(e => e.step === 1);
      if (errs.length) {
        setStepNotice({ kind: "error", level: "close", onlyStep: 1, title: "Completa i dettagli per andare avanti" });
        return pick(errs[0]);
      }
    }
    setStepNotice(null);
    onChange({ ...draft, step: n });
  }

  // Banner attivo: quello di ExamsScreen (salvataggio) o quello di "Avanti"; voci ricalcolate dalla bozza
  const activeNotice = notice || stepNotice;
  const noticeItems = activeNotice
    ? validateAll(draft, { level: activeNotice.level }).filter(e => !activeNotice.onlyStep || e.step === activeNotice.onlyStep)
    : [];
  const reveal = !!activeNotice;
  const passingError = reveal ? validateAll(draft).find(e => e.field === "passingPercent")?.message : null;
  // Voce del banner → step giusto (l'URL diventa ?step=N), domanda selezionata, focus sul campo
  function pick(e) {
    if ((draft.step ?? 1) !== e.step) onChange({ ...draft, step: e.step });
    if (e.questionId) setSelectedId(e.questionId);
    setFocus(f => ({ field: e.field, questionId: e.questionId, n: (f?.n || 0) + 1 }));
  }
  function dismissNotice() { setStepNotice(null); onDismissNotice?.(); }

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

      {/* Soglia di sufficienza: qui il docente conosce già il totale dei punti */}
      <View style={{ borderRadius: 10, backgroundColor: C.bg, padding: 12, borderWidth: 1, borderColor: passingError ? C.danger : C.bg }}>
        <Text style={[s.label, { marginBottom: 6 }]}>Soglia di sufficienza</Text>
        <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap" }}>
          <TextInput ref={passingRef} style={[field, { width: 64, textAlign: "center", backgroundColor: C.white }, passingError && s.inputError]}
            keyboardType="number-pad" value={String(draft.passingPercent)} placeholder="Es. 60" placeholderTextColor={C.textLight}
            onChangeText={(v) => onChange({ ...draft, passingPercent: v.replace(/[^0-9]/g, "") })} accessibilityLabel="Soglia di sufficienza in percentuale" />
          <Text style={{ color: C.text, fontWeight: "700" }}>%</Text>
          <Text style={{ color: C.textMuted, flexShrink: 1 }}>{passingPointsLabel(totalPoints, draft.passingPercent) || (totalPoints > 0 ? "" : "assegna dei punti alle domande")}</Text>
        </View>
        {!!passingError && <Text style={s.errorText}>{passingError}</Text>}
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

    </View>
  );

  const editor = selected ? (
    <QuestionEditor key={selected.id} q={selected} index={selectedIndex} total={questions.length} canDelete={questions.length > 1}
      reveal={reveal} focus={focus?.questionId === selected.id ? focus : null}
      onChange={(next) => setQuestion(selected.id, next)}
      onDuplicate={() => duplicate(selected)}
      onDelete={() => remove(selected.id)}
      onMove={(dir) => move(selected.id, dir)} />
  ) : null;

  const actionBtn = {
    style: [s.smBtn, { flex: 1, height: 40, paddingVertical: 0, paddingHorizontal: 8, alignItems: "center", justifyContent: "center" }],
    textStyle: [s.smBtnText, { textAlign: "center" }],
  };
  const barButtons = (destroyAction ? 1 : 0) + (step > 1 ? 1 : 0) + 1;

  return (
    // Pagina dell'app (non più un Modal): sta nell'area dei contenuti, con la sidebar visibile.
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={{ flex: 1 }}>
        {/* Barra superiore */}
        {/* Il titolo ("← Esami / Nuovo esame") è nell'header di AppNav: qui stato, Stepper e pulsanti */}
        <View style={{ backgroundColor: C.white, paddingHorizontal: wideBar ? 28 : 16, paddingVertical: 10, borderBottomWidth: 1, borderBottomColor: C.border, gap: 10, zIndex: 10 }}>
          <View style={{ flexDirection: "row", flexWrap: "wrap", alignItems: "center", justifyContent: "space-between", gap: 12 }}>
          <View style={{ flexDirection: "row", alignItems: "center", gap: 8, flexWrap: "wrap", minWidth: wideBar ? 200 : 0, flexGrow: 1, flexShrink: 1, flexBasis: wideBar ? 0 : "100%" }}>
            {/* Stato reale della bozza con accanto l'azione che lo cambia. Su schermi larghi va a capo se serve,
                su quelli stretti diventa un badge corto ("Non salvata") per non essere tagliato. */}
            {wideBar ? (
              <Text style={{ color: C.textMuted, fontSize: 13, flexShrink: 1 }} numberOfLines={2}>
                {[contextLabel, draftStatus].filter(Boolean).join(" · ")}
              </Text>
            ) : !!draftStatusShort && (
              <StatusChip label={draftStatusShort.label} tone={draftStatusShort.tone} style={{ alignSelf: "center" }} />
            )}
            {/* Secondaria: salva e resta qui */}
            <TouchableOpacity onPress={onSaveDraft} disabled={saving} accessibilityRole="button" accessibilityLabel="Salva bozza"
              style={{ flexDirection: "row", alignItems: "center", gap: 4, paddingVertical: 6, paddingHorizontal: 10, borderRadius: 8, borderWidth: 1, borderColor: C.primary, opacity: saving ? 0.6 : 1 }}>
              <Ionicons name="cloud-upload-outline" size={14} color={C.primary} />
              <Text style={{ color: C.primary, fontSize: 13, fontWeight: "700" }}>{saving ? "Salvataggio…" : "Salva bozza"}</Text>
            </TouchableOpacity>
            {!wideBar && !!destroyAction && (
              <TouchableOpacity onPress={() => setMoreOpen(o => !o)} accessibilityRole="button" accessibilityLabel="Altre azioni" accessibilityState={{ expanded: moreOpen }}
                style={{ marginLeft: "auto", paddingVertical: 4, paddingHorizontal: 10, borderRadius: 8, borderWidth: 1, borderColor: C.border }}>
                <Text style={{ color: C.text, fontSize: 16, fontWeight: "800", lineHeight: 18 }}>⋯</Text>
              </TouchableOpacity>
            )}
          </View>
          {/* Stessa larghezza, altezza e carattere per tutti: la riga ha larghezza fissa su desktop e i pulsanti se la dividono */}
          <View style={{ flexDirection: "row", alignItems: "center", gap: 8, marginLeft: "auto", maxWidth: "100%", width: wideBar ? barButtons * BAR_BTN_WIDTH + (barButtons - 1) * 8 : "100%" }}>
            {wideBar && !!destroyAction && <Btn label={destroyAction.label} variant="ghost" onPress={destroyAction.onPress} disabled={saving}
              style={[actionBtn.style, { borderColor: C.danger }]} textStyle={[actionBtn.textStyle, { color: C.danger }]} />}
            {/* Indietro: secondario, a sinistra dell'azione principale */}
            {step > 1 && <Btn label="← Indietro" variant="ghost" onPress={() => goTo(step - 1)} {...actionBtn} />}
            {/* Principale: Avanti convalida e passa allo step dopo, senza salvare sul server; allo step 3 "Salva e chiudi" */}
            {step < 3
              ? <Btn label="Avanti →" onPress={() => goTo(step + 1)} {...actionBtn} />
              : <Btn label="Salva e chiudi" onPress={onSaveAndClose} loading={saving} {...actionBtn} />}
          </View>
          </View>
          {/* Menu "⋯": una riga normale della barra, così non copre mai gli altri pulsanti */}
          {!wideBar && moreOpen && !!destroyAction && (
            <Btn label={destroyAction.label} variant="ghost" disabled={saving} onPress={() => { setMoreOpen(false); destroyAction.onPress(); }}
              style={[s.smBtn, { height: 40, paddingVertical: 0, borderColor: C.danger }]} textStyle={[s.smBtnText, { color: C.danger }]} />
          )}
          {/* Riga a parte per Dettagli / Domande / Studenti */}
          <View style={{ alignItems: "center" }}><Stepper step={step} onStep={goTo} compact={!wideBar} /></View>
        </View>
        <ErrorBanner notice={activeNotice} items={noticeItems} onPick={pick} onDismiss={dismissNotice} />

        {step === 1 ? (
          <ScrollView contentContainerStyle={{ padding: isDesktop ? 40 : 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
            <View style={{ width: "100%", maxWidth: 640 }}>
              <DetailsStep draft={draft} onChange={onChange} env={env} isDesktop={isDesktop} reveal={reveal} focus={focus && !focus.questionId ? focus : null} />
            </View>
          </ScrollView>
        ) : step === 3 ? (
          <ScrollView contentContainerStyle={{ padding: isDesktop ? 40 : 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
            <View style={{ width: "100%", maxWidth: 720 }}>
              <StudentsStep draft={draft} onChange={onChange} env={env} />
            </View>
          </ScrollView>
        ) : isWide ? (
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

