// src/components/LessonCalendar.js
// Calendario settimanale delle lezioni: griglia giorno × ora. L'admin può aggiungere, modificare ed
// eliminare le lezioni di una classe (scegliendo la materia, che fissa il docente); docenti e studenti
// lo vedono in sola lettura, filtrato sulle proprie lezioni.
import { useState } from "react";
import { ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import { DAYS, PERIODS, getSubjectsOfClass } from "../api/lessonService";
import { confirmAction, notify } from "../utils/dialogs";
import Btn from "./Btn";
import FormModal from "./FormModal";
import SelectField from "./SelectField";

const CELL_W = 96;

function Cell({ lesson, editable, onPress }) {
  return (
    <TouchableOpacity disabled={!editable && !lesson} onPress={onPress} activeOpacity={lesson || editable ? 0.7 : 1}
      style={{
        width: CELL_W, minHeight: 52, borderRadius: 8, borderWidth: 1, padding: 6, justifyContent: "center",
        borderColor: lesson ? C.primary : C.border, backgroundColor: lesson ? "#E0EEF8" : editable ? C.bg : C.white,
        borderStyle: lesson ? "solid" : "dashed",
      }}>
      {lesson ? (
        <>
          <Text style={{ color: C.text, fontWeight: "700", fontSize: 12 }} numberOfLines={1}>{lesson.subjectName}</Text>
          <Text style={{ color: C.textMuted, fontSize: 11 }} numberOfLines={1}>{lesson.teacherLastName} {lesson.teacherFirstName}</Text>
          {lesson.className && <Text style={{ color: C.textLight, fontSize: 11 }} numberOfLines={1}>{lesson.className}</Text>}
        </>
      ) : editable ? (
        <Ionicons name="add" size={18} color={C.textLight} style={{ alignSelf: "center" }} />
      ) : null}
    </TouchableOpacity>
  );
}

// Form di aggiunta/modifica di una lezione (solo admin): materia della classe scelta, giorno e ora.
function LessonForm({ visible, classId, slot, lesson, onClose, onSave, onDelete }) {
  const subjects = classId ? getSubjectsOfClass(classId) : [];
  const [subjectId, setSubjectId] = useState(lesson?.subjectId || "");

  if (!visible) return null;
  const day = DAYS.find(d => d.id === (lesson?.day ?? slot?.day));
  const period = PERIODS.find(p => p.id === (lesson?.period ?? slot?.period));

  return (
    <FormModal visible title={lesson ? "Modifica lezione" : "Nuova lezione"} onClose={onClose}>
      <View style={{ padding: 16 }}>
        <Text style={[s.itemSub, { marginBottom: 12 }]}>{day?.label} · {period?.label} ({period?.start}–{period?.end})</Text>
        <SelectField label="Materia" value={subjectId} options={subjects} getLabel={sb => `${sb.subjectName} — ${sb.teacherLastName} ${sb.teacherFirstName}`}
          getValue={sb => sb.subjectId} onSelect={setSubjectId} placeholder="Seleziona materia" emptyMessage="Nessuna materia per questa classe" />
        <View style={{ flexDirection: "row", justifyContent: "space-between", gap: 12, marginTop: 8 }}>
          {lesson ? <Btn label="Elimina" variant="ghost" onPress={onDelete} style={{ borderColor: C.danger }} textStyle={{ color: C.danger }} /> : <View />}
          <Btn label="Salva" onPress={() => subjectId ? onSave(subjectId) : notify("Attenzione", "Seleziona una materia.")} />
        </View>
      </View>
    </FormModal>
  );
}

// `lessons`: array con day/period già risolti (vedi lessonService.withDetails).
// `editable`: true solo per l'admin, che deve passare anche `classId` e le funzioni di salvataggio.
export default function LessonCalendar({ lessons, editable, classId, onAdd, onEdit, onDelete, compact }) {
  const [form, setForm] = useState(null); // { slot } oppure { lesson }
  const find = (day, period) => lessons.find(l => l.day === day && l.period === period);

  function openSlot(day, period) {
    const lesson = find(day, period);
    if (!editable && !lesson) return;
    setForm(lesson ? { lesson } : { slot: { day, period } });
  }

  function save(subjectId) {
    try {
      if (form.lesson) onEdit(form.lesson.id, { subjectId, day: form.lesson.day, period: form.lesson.period });
      else onAdd({ classId, subjectId, day: form.slot.day, period: form.slot.period });
      setForm(null);
    } catch (e) { notify("Attenzione", e.message); }
  }

  function remove() {
    confirmAction("Elimina lezione", `Eliminare ${form.lesson.subjectName} di ${DAYS.find(d => d.id === form.lesson.day)?.label}?`, () => {
      onDelete(form.lesson.id);
      setForm(null);
    });
  }

  const periods = compact ? PERIODS.slice(0, 4) : PERIODS;

  return (
    <View>
      <ScrollView horizontal showsHorizontalScrollIndicator={false}>
        <View>
          <View style={{ flexDirection: "row", gap: 6, marginBottom: 6 }}>
            <View style={{ width: 56 }} />
            {DAYS.map(d => (
              <View key={d.id} style={{ width: CELL_W, alignItems: "center" }}>
                <Text style={{ color: C.textMuted, fontWeight: "700", fontSize: 12 }}>{d.short}</Text>
              </View>
            ))}
          </View>
          {periods.map(p => (
            <View key={p.id} style={{ flexDirection: "row", gap: 6, marginBottom: 6 }}>
              <View style={{ width: 56, justifyContent: "center" }}>
                <Text style={{ color: C.textMuted, fontSize: 11, fontWeight: "600" }}>{p.label}</Text>
                <Text style={{ color: C.textLight, fontSize: 10 }}>{p.start}</Text>
              </View>
              {DAYS.map(d => (
                <Cell key={d.id} lesson={find(d.id, p.id)} editable={editable} onPress={() => openSlot(d.id, p.id)} />
              ))}
            </View>
          ))}
        </View>
      </ScrollView>

      <LessonForm visible={!!form} classId={classId} slot={form?.slot} lesson={form?.lesson}
        onClose={() => setForm(null)} onSave={save} onDelete={remove} />
    </View>
  );
}
