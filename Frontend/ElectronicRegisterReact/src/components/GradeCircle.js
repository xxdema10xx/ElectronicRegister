// src/components/GradeCircle.js
import { Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { gradeTone } from "../constants/gradeScale";

// Verde se sufficiente (>= 18), arancione vicino alla soglia (15–17), rosso sotto
export const GRADE_COLORS = { pass: C.success, near: C.warning, fail: C.danger, neutral: C.textMuted };

export default function GradeCircle({ value }) {
  const color = GRADE_COLORS[gradeTone(value)];
  return (
    <View style={[s.gradeCircle, { backgroundColor: color }]}>
      <Text style={s.gradeValue}>{value}</Text>
    </View>
  );
}
