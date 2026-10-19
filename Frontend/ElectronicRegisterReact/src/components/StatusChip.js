// src/components/StatusChip.js
import { Text, View } from "react-native";

const TONES = {
  gray: ["#E2E8F0", "#475569"],
  green: ["#D1FAE5", "#065F46"],
  amber: ["#FEF3C7", "#92400E"],
  red: ["#FEE2E2", "#991B1B"],
  blue: ["#DBEAFE", "#1D4ED8"],
};

// Etichetta colorata per gli stati (esame, consegna)
export default function StatusChip({ label, tone = "gray", style }) {
  const [bg, fg] = TONES[tone] || TONES.gray;
  return (
    <View style={[{ backgroundColor: bg, borderRadius: 12, paddingHorizontal: 10, paddingVertical: 3, alignSelf: "flex-start" }, style]}>
      <Text style={{ color: fg, fontSize: 12, fontWeight: "700" }}>{label}</Text>
    </View>
  );
}

export const EXAM_TONE = { draft: "gray", published: "green", closed: "blue", cancelled: "red" };
export const SUBMISSION_TONE = { in_progress: "gray", graded: "green", pending_review: "amber", failed_auto: "red", finalized: "green" };
