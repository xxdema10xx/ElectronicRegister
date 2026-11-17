// src/components/LessonCalendar.js
// Orario settimanale in sola lettura: una colonna per giorno, lezioni in ordine di orario.
import { ScrollView, Text, View } from "react-native";
import { C } from "../constants/theme";
import { DAYS } from "../api/lessonService";

const COL_W = 190;

function LessonCard({ lesson }) {
  return (
    <View style={{ borderRadius: 8, borderWidth: 1, borderColor: C.primary, backgroundColor: "#E0EEF8", padding: 8, marginBottom: 8 }}>
      <Text style={{ color: C.primary, fontWeight: "700", fontSize: 11 }}>{lesson.start}–{lesson.end}</Text>
      <Text style={{ color: C.text, fontWeight: "700", fontSize: 12 }} numberOfLines={1}>{lesson.title}</Text>
      {!!lesson.teacherName && <Text style={{ color: C.textMuted, fontSize: 11 }} numberOfLines={1}>{lesson.teacherName}</Text>}
      {!!(lesson.className || lesson.room) && (
        <Text style={{ color: C.textLight, fontSize: 11 }} numberOfLines={1}>{[lesson.className, lesson.room].filter(Boolean).join(" · ")}</Text>
      )}
    </View>
  );
}

// lessons: null durante il caricamento, altrimenti le lezioni della settimana (vedi lessonService)
export default function LessonCalendar({ lessons }) {
  if (!lessons) return <Text style={{ color: C.textMuted, paddingVertical: 8 }}>Caricamento…</Text>;
  if (lessons.length === 0) return <Text style={{ color: C.textMuted, paddingVertical: 8 }}>Orario non ancora disponibile</Text>;

  // Sabato e domenica solo se ci sono lezioni
  const days = DAYS.filter(d => d.id <= 5 || lessons.some(l => l.day === d.id));
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false}>
      <View style={{ flexDirection: "row", gap: 8 }}>
        {days.map(d => {
          const dayLessons = lessons.filter(l => l.day === d.id);
          return (
            <View key={d.id} style={{ width: COL_W }}>
              <Text style={{ color: C.textMuted, fontWeight: "700", fontSize: 12, marginBottom: 6, textAlign: "center" }}>{d.short}</Text>
              {dayLessons.length === 0
                ? <Text style={{ color: C.textLight, fontSize: 11, textAlign: "center", marginBottom: 8 }}>Nessuna lezione</Text>
                : dayLessons.map(l => <LessonCard key={l.id} lesson={l} />)}
            </View>
          );
        })}
      </View>
    </ScrollView>
  );
}
