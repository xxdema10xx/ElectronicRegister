// src/components/ClassGradesForm.js
// "Voti alla classe": un voto per studente della classe-materia scelta, salvati con UNA richiesta
// (POST /institution/grades/batch, api/gradeBatchService.js).
//  - celle vuote non inviate; validazione in tempo reale (interi 0–30); "Salva" disattivato con errori;
//  - eventId generato all'apertura e riusato nei retry dello stesso payload (doppio clic o risposta persa non
//    duplicano i voti); se il payload cambia dopo un invio (es. correzione dopo un 400) si genera un nuovo eventId,
//    altrimenti il server risponderebbe 409;
//  - 400 GradeBatchInvalid: righe evidenziate con il motivo, nulla è stato salvato;
//  - guardia "modifiche non salvate" come nell'editor esami.
// Props: classSubjects (facoltativo: [{ classSubjectId, classId, label }] già noti, es. le materie del docente
// nella classe aperta) e students (facoltativo, studenti già noti di quella classe); altrimenti si leggono.
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Text, TextInput, View } from "react-native";
import { C, s } from "../constants/theme";
import FormModal from "./FormModal";
import SelectField from "./SelectField";
import DateField from "./DateField";
import Input from "./Input";
import Btn from "./Btn";
import Loader from "./Loader";
import { notify, confirmLeave } from "../utils/dialogs";
import { toISODate } from "../utils/dateUtils";
import { setLeaveGuard, useBeforeUnload } from "../navigation/examRoutes";
import {
  BATCH_MAX_ROWS, batchRowErrors, createGradesBatch, getClassStudents, getGradeClassSubjects, newBatchEventId,
} from "../api/gradeBatchService";
import {
  GRADE_DESCRIPTION_LABEL, GRADE_DESCRIPTION_MAX, GRADE_DESCRIPTION_PLACEHOLDER, GRADE_KEYBOARD, GRADE_PLACEHOLDER, cleanDescription, parseGrade,
} from "../constants/gradeScale";

export const ROW_INVALID = "Intero tra 0 e 30";
// Elenco delle classi-materia non leggibile per il ruolo (403/404): niente tabella, solo l'avviso
export const OPTIONS_UNAVAILABLE = "Il server non consente al tuo ruolo di leggere le classi e le materie: l'inserimento in blocco non è disponibile.";
const LEAVE_MESSAGE = "I voti inseriti non sono stati salvati e andranno persi.";
const NOT_FOUND_MESSAGE = "La classe-materia non esiste più oppure non è tra quelle che insegni.";
const CONFLICT_MESSAGE = "Il blocco potrebbe essere già stato salvato: ho ricaricato i voti. Controlla l'elenco prima di inviare di nuovo.";

const fullName = (st) => `${st.lastName} ${st.firstName}`;

export default function ClassGradesForm({ visible, onClose, onSaved, token, teacherId, classSubjects: fixedOptions, students: fixedStudents }) {
  const [options, setOptions] = useState(fixedOptions || null); // null = in caricamento o non disponibili
  const [optionsError, setOptionsError] = useState(null);
  const [classSubjectId, setClassSubjectId] = useState(fixedOptions?.length === 1 ? fixedOptions[0].classSubjectId : "");
  const [date, setDate] = useState(toISODate(new Date()));
  const [description, setDescription] = useState("");
  const [students, setStudents] = useState(null);
  const [values, setValues] = useState({}); // { [studentId]: testo }
  const [serverErrors, setServerErrors] = useState({}); // { [studentId]: motivo dal 400 }
  const [saving, setSaving] = useState(false);
  // eventId generato una sola volta all'apertura (non a ogni render)
  const eventIdRef = useRef(null);
  if (eventIdRef.current === null) eventIdRef.current = newBatchEventId();
  const lastSentRef = useRef(null); // payload (senza eventId) dell'ultimo invio

  // Classi-materia tra cui scegliere (se non passate dal chiamante)
  useEffect(() => {
    if (!visible || fixedOptions) return;
    let alive = true;
    getGradeClassSubjects(token, { teacherId })
      .then((list) => { if (!alive) return; if (list === null) setOptionsError(OPTIONS_UNAVAILABLE); else setOptions(list); })
      .catch((e) => { if (alive) setOptionsError(e.message); });
    return () => { alive = false; };
  }, [visible, token, teacherId, fixedOptions]);

  const chosen = (options || []).find(o => o.classSubjectId === classSubjectId) || null;

  // Studenti della classe della classe-materia scelta
  useEffect(() => {
    if (!chosen) { setStudents(null); return; }
    if (fixedStudents) { setStudents(fixedStudents.slice().sort((a, b) => fullName(a).localeCompare(fullName(b)))); return; }
    let alive = true;
    setStudents(null);
    getClassStudents(chosen.classId, token)
      .then((list) => { if (alive) setStudents(list); })
      .catch((e) => { if (alive) { setStudents([]); notify("Errore", e.message); } });
    return () => { alive = false; };
  }, [chosen?.classSubjectId, token, fixedStudents]);

  // Righe compilate e validazione per riga
  const rows = useMemo(() => (students || []).map(st => {
    const text = values[st.id] ?? "";
    const filled = String(text).trim() !== "";
    const value = filled ? parseGrade(text) : null;
    const error = serverErrors[st.id] || (filled && value === null ? ROW_INVALID : null);
    return { student: st, text, filled, value, error };
  }), [students, values, serverErrors]);
  const toSend = rows.filter(r => r.filled && !r.error);
  const hasErrors = rows.some(r => r.error);
  const tooMany = toSend.length > BATCH_MAX_ROWS;
  const canSave = !!chosen && !!date && toSend.length > 0 && !hasErrors && !tooMany && !saving;

  const dirty = visible && (rows.some(r => r.filled) || !!description.trim());
  useBeforeUnload(dirty);
  const closeNow = useCallback(() => { setLeaveGuard(null); onClose(); }, [onClose]);
  useEffect(() => {
    if (!visible) return;
    setLeaveGuard(dirty ? (proceed) => confirmLeave(proceed, { message: LEAVE_MESSAGE }) : null);
    return () => setLeaveGuard(null);
  }, [visible, dirty]);

  function requestClose() {
    if (dirty) confirmLeave(closeNow, { message: LEAVE_MESSAGE });
    else closeNow();
  }

  function setValue(studentId, text) {
    setValues(v => ({ ...v, [studentId]: text }));
    // La riga corretta perde l'errore del server; le altre lo tengono finché non cambiano
    if (serverErrors[studentId]) setServerErrors(({ [studentId]: _, ...rest }) => rest);
  }

  function chooseClassSubject(id) {
    if (id === classSubjectId) return;
    setClassSubjectId(id);
    setValues({});
    setServerErrors({});
  }

  async function save() {
    if (!canSave) return;
    const grades = toSend.map(r => ({ studentId: r.student.id, value: r.value }));
    const payload = { classSubjectId, date, description: cleanDescription(description), grades };
    const key = JSON.stringify(payload);
    // Payload cambiato rispetto all'ultimo invio → nuovo eventId; stesso payload (retry) → stesso eventId
    if (lastSentRef.current !== null && lastSentRef.current !== key) eventIdRef.current = newBatchEventId();
    lastSentRef.current = key;
    setSaving(true);
    try {
      const res = await createGradesBatch({ eventId: eventIdRef.current, ...payload }, token);
      const count = Array.isArray(res?.created) ? res.created.length : grades.length;
      notify("Voti salvati", `Salvati ${count} ${count === 1 ? "voto" : "voti"}.`);
      onSaved?.(count);
      closeNow();
    } catch (e) {
      const byRow = batchRowErrors(e, grades);
      if (byRow) {
        setServerErrors(byRow);
        notify("Voti non salvati", "Nessun voto è stato salvato: correggi le righe evidenziate e salva di nuovo.");
      } else if (e.status === 404) {
        notify("Voti non salvati", NOT_FOUND_MESSAGE);
      } else if (e.status === 409) {
        onSaved?.(0);
        notify("Attenzione", CONFLICT_MESSAGE);
      } else {
        notify("Voti non salvati", e.message);
      }
    } finally { setSaving(false); }
  }

  return (
    <FormModal wide visible={visible} title="Voti alla classe" onClose={requestClose}>
      {optionsError ? <Text style={{ color: C.danger, marginBottom: 12 }}>{optionsError}</Text> : !options ? <Loader /> : (
        <View>
          <SelectField label="Classe e materia" value={classSubjectId} options={options} getLabel={o => o.label} getValue={o => o.classSubjectId}
            onSelect={chooseClassSubject} placeholder="Scegli classe e materia…" emptyMessage="Nessuna classe-materia disponibile" />
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 16 }}>
            <View style={{ flex: 1, minWidth: 180 }}>
              <DateField label="Data" value={date} onChange={setDate} />
            </View>
            <View style={{ flex: 2, minWidth: 220 }}>
              <Input label={GRADE_DESCRIPTION_LABEL} value={description} onChangeText={setDescription}
                placeholder={GRADE_DESCRIPTION_PLACEHOLDER} maxLength={GRADE_DESCRIPTION_MAX} />
            </View>
          </View>

          {chosen && (students === null ? <Loader /> : students.length === 0 ? (
            <Text style={[s.emptyText, { paddingVertical: 16 }]}>Nessuno studente in questa classe</Text>
          ) : (
            <View style={{ borderWidth: 1, borderColor: C.border, borderRadius: 10, marginBottom: 12 }}>
              {rows.map((r, i) => (
                <View key={r.student.id} testID={`batch-row-${r.student.id}`}
                  style={{ flexDirection: "row", alignItems: "center", gap: 10, paddingVertical: 8, paddingHorizontal: 12,
                    borderTopWidth: i ? 1 : 0, borderColor: C.border, backgroundColor: r.error ? "#FEF2F2" : "transparent" }}>
                  <View style={{ flex: 1 }}>
                    <Text style={s.itemTitle}>{fullName(r.student)}</Text>
                    {!!r.error && <Text style={{ color: C.danger, fontSize: 13 }}>{r.error}</Text>}
                  </View>
                  <TextInput style={[s.input, { width: 80, textAlign: "center", marginBottom: 0 }, r.error && s.inputError]}
                    value={r.text} onChangeText={(t) => setValue(r.student.id, t)} keyboardType={GRADE_KEYBOARD} maxLength={2}
                    placeholder={GRADE_PLACEHOLDER} placeholderTextColor={C.textLight} accessibilityLabel={`Voto di ${fullName(r.student)}`} />
                </View>
              ))}
            </View>
          ))}

          {tooMany && <Text style={{ color: C.danger, marginBottom: 8 }}>Al massimo {BATCH_MAX_ROWS} voti per volta.</Text>}
          <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 10 }}>
            <Text style={s.itemSub}>{toSend.length} {toSend.length === 1 ? "voto" : "voti"} da salvare</Text>
            <View style={{ flexDirection: "row", gap: 10 }}>
              <Btn label="Annulla" variant="ghost" onPress={requestClose} style={{ paddingHorizontal: 22 }} />
              <Btn label="Salva" onPress={save} loading={saving} disabled={!canSave} style={{ paddingHorizontal: 28 }} />
            </View>
          </View>
        </View>
      )}
    </FormModal>
  );
}
