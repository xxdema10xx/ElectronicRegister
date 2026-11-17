// src/components/ExamGradeTag.js
// Etichetta "da esame" per il voto ufficiale di una consegna d'esame (GradeDto.source === "exam"): nel registro
// non si modifica né si elimina (il server risponderebbe 409), si cambia dalla correzione dell'esame.
import { Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { EXAM_GRADE_HINT, EXAM_GRADE_LABEL } from "../constants/gradeScale";

export default function ExamGradeTag() {
  return (
    <View accessible accessibilityLabel={`Voto ${EXAM_GRADE_LABEL}: ${EXAM_GRADE_HINT}`}
      style={[s.badge, { backgroundColor: C.studentBadge, alignSelf: "center" }]}>
      <Text style={[s.badgeText, { color: C.primary }]}>{EXAM_GRADE_LABEL}</Text>
    </View>
  );
}
