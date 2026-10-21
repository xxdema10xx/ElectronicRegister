// src/components/LessonCalendar.js
// Calendario settimanale delle lezioni: una colonna per giorno, lezioni elencate in ordine di orario
// (orario libero, es. 9:30–12:30, non vincolato a ore tonde). L'admin può aggiungere, modificare ed
// eliminare le lezioni di una classe (scegliendo la materia, che fissa il docente); docenti e studenti
// lo vedono in sola lettura, filtrato sulle proprie lezioni.
import { useState } from "react";
import { ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { DAYS, TIME_OPTIONS, getSubjectsOfClass } from "../api/lessonService";
import { confirmAction, notify } from "../utils/dialogs";
import Btn from "./Btn";
import FormModal from "./FormModal";
import SelectField from "./SelectField";

const COL_W = 190;

// Campo orario: solo ore spaccate o mezz'ore (vedi lessonService.TIME_OPTIONS), mai minuti liberi.
function TimeField({ label, value, onChange }) {
  return (
    <View style={{ flex: 1 }}>
      <SelectField label={label} value={value} options={TIME_OPTIONS} getLabel={t => t} getValue={t => t}
        onSelect={onChange} placeholder="Scegli l'orario" />
    </View>
  );
}

function LessonCard({ lesson, editable, onPress }) {
  return (
    <TouchableOpacity disabled={!editable} onPress={onPress} activeOpacity={editable ? 0.7 : 1}
      style={{ borderRadius: 8, borderWidth: 1, borderColor: C.primary, backgroundColor: "#E0EEF8", padding: 8, marginBottom: 8 }}>
      <Text style={{ color: C.primary, fontWeight: "700", fontSize: 11 }}>{lesson.start}–{lesson.end}</Text>
      <Text style={{ color: C.text, fontWeight: "700", fontSize: 12 }} numberOfLines={1}>{lesson.subjectName}</Text>
      <Text style={{ color: C.textMuted, fontSize: 11 }} numberOfLines={1}>{lesson.teacherLastName} {lesson.teacherFirstName}</Text>
      {!!lesson.className && <Text style={{ color: C.textLight, fontSize: 11 }} numberOfLines={1}>{lesson.className}</Text>}
    </TouchableOpacity>
  );
}

// Form di aggiunta/modifica di una lezione (solo admin): giorno, orario libero e materia della classe scelta.
function LessonForm({ visible, classId, day, lesson, onClose, onSave, onDelete }) {
  const subjects = classId ? getSubjectsOfClass(classId) : [];
  const [subjectId, setSubjectId] = useState(lesson?.subjectId || "");
  const [selDay, setSelDay] = useState(lesson?.day || day);
  const [start, setStart] = useState(lesson?.start || "");
  const [end, setEnd] = useState(lesson?.end || "");

  if (!visible) return null;

  return (
    <FormModal visible title={lesson ? "Modifica lezione" : "Nuova lezione"} onClose={onClose}>
      <View style={{ padding: 16 }}>
        <SelectField label="Giorno" value={selDay} options={DAYS} getLabel={d => d.label} onSelect={setSelDay} />
        <View style={{ flexDirection: "row", gap: 10 }}>
          <TimeField label="Inizio" value={start} onChange={setStart} />
          <TimeField label="Fine" value={end} onChange={setEnd} />
        </View>
        <SelectField label="Materia" value={subjectId} options={subjects} getLabel={sb => `${sb.subjectName} — ${sb.teacherLastName} ${sb.teacherFirstName}`}
          getValue={sb => sb.subjectId} onSelect={setSubjectId} placeholder="Seleziona materia" emptyMessage="Nessuna materia per questa classe" />
        <View style={{ flexDirection: "row", justifyContent: "space-between", gap: 12, marginTop: 8 }}>
          {lesson ? <Btn label="Elimina" variant="ghost" onPress={onDelete} style={{ borderColor: C.danger }} textStyle={{ color: C.danger }} /> : <View />}
          <Btn label="Salva" onPress={() => subjectId ? onSave({ subjectId, day: selDay, start, end }) : notify("Attenzione", "Seleziona una materia.")} />
        </View>
      </View>
    </FormModal>
  );
}

// `lessons`: array con day/start/end già risolti (vedi lessonService.withDetails).
// `editable`: true solo per l'admin, che deve passare anche `classId` e le funzioni di salvataggio.
export default function LessonCalendar({ lessons, editable, classId, onAdd, onEdit, onDelete }) {
  const [form, setForm] = useState(null); // { day } per una nuova lezione, oppure { lesson } per modificarne una

  function save(values) {
    try {
      if (form.lesson) onEdit(form.lesson.id, values);
      else onAdd({ classId, ...values });
      setForm(null);
    } catch (e) { notify("Attenzione", e.message); }
  }

  function remove() {
    confirmAction("Elimina lezione", `Eliminare ${form.lesson.subjectName} di ${DAYS.find(d => d.id === form.lesson.day)?.label}?`, () => {
      onDelete(form.lesson.id);
      setForm(null);
    });
  }

  return (
    <View>
      <ScrollView horizontal showsHorizontalScrollIndicator={false}>
        <View style={{ flexDirection: "row", gap: 8 }}>
          {DAYS.map(d => {
            const dayLessons = lessons.filter(l => l.day === d.id);
            return (
              <View key={d.id} style={{ width: COL_W }}>
                <Text style={{ color: C.textMuted, fontWeight: "700", fontSize: 12, marginBottom: 6, textAlign: "center" }}>{d.short}</Text>
                {dayLessons.length === 0 ? (
                  <Text style={{ color: C.textLight, fontSize: 11, textAlign: "center", marginBottom: 8 }}>Nessuna lezione</Text>
                ) : dayLessons.map(l => (
                  <LessonCard key={l.id} lesson={l} editable={editable} onPress={() => setForm({ lesson: l })} />
                ))}
                {editable && (
                  <TouchableOpacity onPress={() => setForm({ day: d.id })} style={{ flexDirection: "row", alignItems: "center", justifyContent: "center", gap: 4, paddingVertical: 6 }}>
                    <Ionicons name="add-circle-outline" size={16} color={C.primary} />
                    <Text style={{ color: C.primary, fontWeight: "600", fontSize: 12 }}>Aggiungi</Text>
                  </TouchableOpacity>
                )}
              </View>
            );
          })}
        </View>
      </ScrollView>

      <LessonForm visible={!!form} classId={classId} day={form?.day} lesson={form?.lesson}
        onClose={() => setForm(null)} onSave={save} onDelete={remove} />
    </View>
  );
}
