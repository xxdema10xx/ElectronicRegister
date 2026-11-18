// src/components/LessonCalendar.js
// Orario settimanale: lezioni in ordine di orario, raggruppate per giorno.
//  - spazio sufficiente: una colonna per giorno, che si allarga fino a riempire il riquadro (niente
//    scorrimento orizzontale nascosto: prima i giorni da giovedì in poi si vedevano solo scorrendo);
//  - spazio stretto (telefono, riquadro piccolo): i giorni uno sotto l'altro.
// Lo stato di ogni lezione (Annullata, Svolta, Supplenza, Recupero, Bozza) è sempre un'etichetta di testo,
// non solo un colore. Toccando una lezione si apre il dettaglio (onPressLesson).
import { useState } from "react";
import { Text, TouchableOpacity, View, useWindowDimensions } from "react-native";
import { C } from "../constants/theme";
import { DAYS, lessonLabels, parseYmd } from "../api/lessonService";
import StatusChip from "./StatusChip";

export const MIN_COL_W = 150; // larghezza minima di una colonna leggibile
const GAP = 8;

// Colonne solo se tutti i giorni ci stanno con almeno MIN_COL_W ciascuno
export const calendarLayout = (width, dayCount) => (width >= dayCount * MIN_COL_W + (dayCount - 1) * GAP ? "columns" : "stacked");

function LessonCard({ lesson, showDraft, onPress }) {
  const cancelled = lesson.status === "Cancelled";
  const draft = lesson.publication === "Draft";
  const labels = lessonLabels(lesson, { showDraft });
  const body = (
    <View style={{ borderRadius: 8, borderWidth: 1, borderStyle: draft && showDraft ? "dashed" : "solid", borderColor: cancelled ? C.textLight : C.primary,
      backgroundColor: cancelled ? C.bg : "#E0EEF8", padding: 8, marginBottom: 8 }}>
      <Text style={{ color: cancelled ? C.textMuted : C.primary, fontWeight: "700", fontSize: 11, textDecorationLine: cancelled ? "line-through" : "none" }}>{lesson.start}–{lesson.end}</Text>
      <Text style={{ color: cancelled ? C.textMuted : C.text, fontWeight: "700", fontSize: 12, textDecorationLine: cancelled ? "line-through" : "none" }} numberOfLines={2}>{lesson.title}</Text>
      {!!lesson.teacherName && <Text style={{ color: C.textMuted, fontSize: 11 }} numberOfLines={1}>{lesson.teacherName}</Text>}
      {!!(lesson.className || lesson.room) && (
        <Text style={{ color: C.textLight, fontSize: 11 }} numberOfLines={1}>{[lesson.className, lesson.room].filter(Boolean).join(" · ")}</Text>
      )}
      {labels.length > 0 && (
        <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 4, marginTop: 4 }}>
          {labels.map(l => <StatusChip key={l.key} label={l.label} tone={l.tone} style={{ paddingHorizontal: 6, paddingVertical: 1 }} />)}
        </View>
      )}
    </View>
  );
  if (!onPress) return body;
  const a11y = [lesson.title, `${lesson.start}–${lesson.end}`, ...labels.map(l => l.label)].join(", ");
  return (
    <TouchableOpacity onPress={() => onPress(lesson)} accessibilityRole="button" accessibilityLabel={`Dettaglio lezione: ${a11y}`}>
      {body}
    </TouchableOpacity>
  );
}

// "Lun 16/11" se si conosce la settimana, altrimenti "Lun"
function dayHeader(day, weekFrom) {
  const monday = parseYmd(weekFrom);
  if (!monday) return day.short;
  const d = new Date(monday.getFullYear(), monday.getMonth(), monday.getDate() + day.id - 1);
  return `${day.short} ${String(d.getDate()).padStart(2, "0")}/${String(d.getMonth() + 1).padStart(2, "0")}`;
}

// lessons: null durante il caricamento, altrimenti le lezioni della settimana (vedi lessonService).
// week: { from, to } per le date nelle intestazioni. showDraft: etichetta "Bozza" (admin e staff).
export default function LessonCalendar({ lessons, week, onPressLesson, showDraft = false, emptyText = "Nessuna lezione in questa settimana" }) {
  // Larghezza reale del riquadro (onLayout); prima della misura si usa quella della finestra
  const { width: windowWidth } = useWindowDimensions();
  const [measured, setMeasured] = useState(0);
  if (!lessons) return <Text style={{ color: C.textMuted, paddingVertical: 8 }}>Caricamento…</Text>;
  if (lessons.length === 0) return <Text style={{ color: C.textMuted, paddingVertical: 8 }}>{emptyText}</Text>;

  // Sabato e domenica solo se ci sono lezioni
  const days = DAYS.filter(d => d.id <= 5 || lessons.some(l => l.day === d.id));
  const layout = calendarLayout(measured || windowWidth, days.length);
  const onLayout = (e) => { const w = Math.round(e.nativeEvent.layout.width); if (w && w !== measured) setMeasured(w); };
  const columns = layout === "columns";
  return (
    <View onLayout={onLayout} testID={`lesson-calendar-${layout}`} style={{ flexDirection: columns ? "row" : "column", gap: columns ? GAP : 12 }}>
      {days.map(d => {
        const dayLessons = lessons.filter(l => l.day === d.id);
        return (
          <View key={d.id} style={columns ? { flex: 1, minWidth: MIN_COL_W } : null}>
            <Text style={{ color: C.textMuted, fontWeight: "700", fontSize: 12, marginBottom: 6, textAlign: columns ? "center" : "left" }}>{dayHeader(d, week?.from)}</Text>
            {dayLessons.length === 0
              ? <Text style={{ color: C.textLight, fontSize: 11, textAlign: columns ? "center" : "left", marginBottom: 8 }}>Nessuna lezione</Text>
              : dayLessons.map(l => <LessonCard key={l.id} lesson={l} showDraft={showDraft} onPress={onPressLesson} />)}
          </View>
        );
      })}
    </View>
  );
}
