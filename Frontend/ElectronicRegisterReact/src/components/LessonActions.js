// src/components/LessonActions.js
// Comandi di admin e staff su una lezione programmata: annulla, sposta, sostituisci il docente.
// Regole comuni: motivo obbligatorio (1–500 caratteri, con contatore), eventId stabile per la stessa
// compilazione (useIntentId), pulsante disattivato durante l'invio, rilettura di lezione e settimana dopo
// la receipt; su 409 si rilegge e si avvisa che la lezione è cambiata nel frattempo.
// Le lezioni svolte (Completed) o annullate non hanno comandi. Niente creazione/modifica libera.
import { useState } from "react";
import { Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import { useIntentId } from "../hooks/useIntentId";
import Btn from "./Btn";
import Input from "./Input";
import SelectField from "./SelectField";
import CheckboxField from "./CheckboxField";
import {
  REASON_MAX, canManageLesson, cancelLesson, hhmm, rescheduleError, rescheduleLesson, substituteLesson, validReason,
} from "../api/calendarService";
import { calendarErrorMessage } from "../api/calendarImportService";

export const CHANGED_MESSAGE = "La lezione è stata modificata nel frattempo: ecco i dati aggiornati.";
const STALE_CODES = ["calendar.revision-conflict", "calendar.event-payload-conflict"];

function commandError(e) {
  if (e?.status === 409 && (STALE_CODES.includes(e.code) || !e.code)) return CHANGED_MESSAGE;
  return calendarErrorMessage(e);
}

function ReasonField({ value, onChange }) {
  return (
    <View>
      <Input label="Motivo (obbligatorio, resta nello storico)" value={value} onChangeText={onChange} maxLength={REASON_MAX} multiline
        style={[s.input, { height: 72, textAlignVertical: "top" }]} />
      <Text style={{ color: value.length > REASON_MAX ? C.danger : C.textLight, fontSize: 12, alignSelf: "flex-end", marginTop: -8, marginBottom: 8 }}>
        {value.length}/{REASON_MAX}
      </Text>
    </View>
  );
}

// Un modulo per comando. submit(eventId) invia; payload è ciò che decide se l'eventId resta lo stesso.
function CommandForm({ title, payload, error: formError, submitLabel, variant = "primary", submit, onDone, onClose, children }) {
  const { idFor, reset } = useIntentId();
  const [sending, setSending] = useState(false);
  const [error, setError] = useState(null);

  async function send() {
    if (formError) { setError(formError); return; }
    setSending(true); setError(null);
    try {
      await submit(idFor(payload));
      reset();
      onDone("ok");
    } catch (e) {
      setError(commandError(e));
      if (e?.status === 409) onDone("stale"); // si rilegge la lezione anche se il comando non è passato
    } finally { setSending(false); }
  }

  return (
    <View style={{ marginTop: 12, padding: 12, borderRadius: 10, borderWidth: 1, borderColor: C.border, backgroundColor: C.bg }}>
      <Text style={[s.itemTitle, { marginBottom: 8 }]}>{title}</Text>
      {children}
      {!!error && <Text accessibilityRole="alert" style={{ color: C.danger, fontSize: 13, marginBottom: 8 }}>{error}</Text>}
      <View style={{ flexDirection: "row", gap: 8, flexWrap: "wrap" }}>
        <Btn label={submitLabel} variant={variant} onPress={send} loading={sending} />
        <Btn label="Chiudi" variant="ghost" onPress={onClose} disabled={sending} />
      </View>
    </View>
  );
}

function CancelForm({ lesson, onDone, onClose }) {
  const { token } = useAuth();
  const [reason, setReason] = useState("");
  const [confirmed, setConfirmed] = useState(false);
  const payload = { expectedRevision: lesson.revision, reason: reason.trim() };
  const error = !validReason(reason) ? "Indica il motivo dell'annullamento." : !confirmed ? "Conferma l'annullamento." : null;
  return (
    <CommandForm title="Annulla lezione" submitLabel="Annulla lezione" variant="danger" payload={payload} error={error} onDone={onDone} onClose={onClose}
      submit={(eventId) => cancelLesson(lesson.id, { eventId, ...payload }, token)}>
      <ReasonField value={reason} onChange={setReason} />
      <CheckboxField label="Confermo: la lezione verrà annullata e resterà visibile come annullata" value={confirmed} onChange={setConfirmed} />
    </CommandForm>
  );
}

function RescheduleForm({ lesson, onDone, onClose }) {
  const { token } = useAuth();
  const [date, setDate] = useState(lesson.date);
  const [startsAt, setStartsAt] = useState(hhmm(lesson.startsAt));
  const [endsAt, setEndsAt] = useState(hhmm(lesson.endsAt));
  const [reason, setReason] = useState("");
  const payload = { expectedRevision: lesson.revision, date: date.trim(), startsAt: startsAt.trim(), endsAt: endsAt.trim(), reason: reason.trim() };
  const error = rescheduleError(payload) || (!validReason(reason) ? "Indica il motivo dello spostamento." : null);
  return (
    <CommandForm title="Sposta lezione" submitLabel="Sposta" payload={payload} error={error} onDone={onDone} onClose={onClose}
      submit={(eventId) => rescheduleLesson(lesson.id, { eventId, ...payload }, token)}>
      {lesson.publication === "Published" && (
        <Text style={{ color: C.warning, fontSize: 13, marginBottom: 8 }}>La lezione è pubblicata: il nuovo orario sarà subito visibile a docente e studenti.</Text>
      )}
      <Input label="Data (AAAA-MM-GG)" value={date} onChangeText={setDate} placeholder="2026-12-01" autoCapitalize="none" />
      <View style={{ flexDirection: "row", gap: 8, flexWrap: "wrap" }}>
        <View style={{ flex: 1, minWidth: 120 }}><Input label="Ora inizio (HH:mm)" value={startsAt} onChangeText={setStartsAt} placeholder="09:00" /></View>
        <View style={{ flex: 1, minWidth: 120 }}><Input label="Ora fine (HH:mm)" value={endsAt} onChangeText={setEndsAt} placeholder="11:00" /></View>
      </View>
      {!!rescheduleError(payload) && <Text style={{ color: C.danger, fontSize: 12, marginBottom: 8 }}>{rescheduleError(payload)}</Text>}
      <ReasonField value={reason} onChange={setReason} />
    </CommandForm>
  );
}

function SubstituteForm({ lesson, teachers, onDone, onClose }) {
  const { token } = useAuth();
  const options = teachers
    .filter(t => t.id !== lesson.teacherId)
    .map(t => ({ id: t.id, name: `${t.lastName || ""} ${t.firstName || ""}`.trim() || "Docente" }))
    .sort((a, b) => a.name.localeCompare(b.name));
  const [teacherId, setTeacherId] = useState(null);
  const [reason, setReason] = useState("");
  const payload = { expectedRevision: lesson.revision, teacherId, reason: reason.trim() };
  const error = !teacherId ? "Scegli il docente che farà lezione." : !validReason(reason) ? "Indica il motivo della sostituzione." : null;
  return (
    <CommandForm title="Sostituisci docente" submitLabel="Sostituisci" payload={payload} error={error} onDone={onDone} onClose={onClose}
      submit={(eventId) => substituteLesson(lesson.id, { eventId, ...payload }, token)}>
      <SelectField label="Nuovo docente" value={teacherId} options={options} onSelect={setTeacherId} getLabel={o => o.name}
        placeholder="Scegli il docente" emptyMessage="Nessun altro docente" />
      <ReasonField value={reason} onChange={setReason} />
    </CommandForm>
  );
}

// onChanged(kind): "ok" dopo un comando riuscito, "stale" dopo un 409 (in entrambi i casi si rilegge)
export default function LessonActions({ lesson, teachers = [], onChanged }) {
  const [open, setOpen] = useState(null); // "cancel" | "reschedule" | "substitute"
  const [done, setDone] = useState(null);
  if (!canManageLesson(lesson)) return null;

  const onDone = (kind) => {
    if (kind === "ok") { setDone(open === "cancel" ? "Lezione annullata." : open === "reschedule" ? "Lezione spostata." : "Docente sostituito."); setOpen(null); }
    onChanged?.(kind);
  };
  const choose = (key) => { setDone(null); setOpen(o => (o === key ? null : key)); };

  return (
    <View style={{ marginTop: 12 }}>
      <View style={{ flexDirection: "row", gap: 8, flexWrap: "wrap" }}>
        <Btn label="Annulla lezione" variant="ghost" onPress={() => choose("cancel")} accessibilityLabel="Annulla lezione" style={{ paddingVertical: 10 }} />
        <Btn label="Sposta" variant="ghost" onPress={() => choose("reschedule")} accessibilityLabel="Sposta la lezione" style={{ paddingVertical: 10 }} />
        <Btn label="Sostituisci docente" variant="ghost" onPress={() => choose("substitute")} accessibilityLabel="Sostituisci il docente" style={{ paddingVertical: 10 }} />
      </View>
      {!!done && <Text accessibilityRole="alert" style={{ color: C.success, fontSize: 13, marginTop: 8 }}>{done}</Text>}
      {open === "cancel" && <CancelForm lesson={lesson} onDone={onDone} onClose={() => setOpen(null)} />}
      {open === "reschedule" && <RescheduleForm lesson={lesson} onDone={onDone} onClose={() => setOpen(null)} />}
      {open === "substitute" && <SubstituteForm lesson={lesson} teachers={teachers} onDone={onDone} onClose={() => setOpen(null)} />}
    </View>
  );
}
