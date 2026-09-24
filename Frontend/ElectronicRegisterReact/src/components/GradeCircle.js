// src/components/GradeCircle.js
import { Text, View } from "react-native";
import { C, s } from "../constants/theme";

export default function GradeCircle({ value }) {
  const color = value >= 6 ? C.success : value >= 5 ? C.warning : C.danger;
  return (
    <View style={[s.gradeCircle, { backgroundColor: color }]}>
      <Text style={s.gradeValue}>{value}</Text>
    </View>
  );
}
