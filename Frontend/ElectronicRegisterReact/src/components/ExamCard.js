// src/components/ExamCard.js
// Riquadro di un esame nell'elenco docente/admin. Gerarchia: stato a colpo d'occhio (barra laterale e
// chip), dettagli come pillole con icona, azioni nel piè di pagina: quelle di consultazione a sinistra,
// quelle che cambiano lo stato dell'esame (Pubblica, Elimina, Annulla) in basso a destra.
import { ActivityIndicator, Platform, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import StatusChip, { EXAM_TONE } from "./StatusChip";
import { EXAM_STATUS_LABEL, parseUtc } from "../api/examContract";

const ACCENT = { draft: "#94A3B8", published: C.success, closed: "#3B82F6", cancelled: C.danger };
const ICON_BG = { draft: "#F1F5F9", published: "#D1FAE5", closed: "#DBEAFE", cancelled: "#FEE2E2" };

const pad = (n) => String(n).padStart(2, "0");
const day = (d) => `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()}`;
const time = (d) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;

// "03/11/2026 · 08:00–12:00" se nello stesso giorno, altrimenti inizio e fine completi
function windowLabel(from, until) {
  const a = parseUtc(from), b = parseUtc(until);
  if (!a || !b || isNaN(a) || isNaN(b)) return "Date da definire";
  return day(a) === day(b) ? `${day(a)} · ${time(a)}–${time(b)}` : `${day(a)} ${time(a)} → ${day(b)} ${time(b)}`;
}

function Pill({ icon, text }) {
  return (
    <View style={{ flexDirection: "row", alignItems: "center", gap: 5, backgroundColor: "#F1F5F9", borderRadius: 999, paddingVertical: 4, paddingHorizontal: 10 }}>
      <Ionicons name={icon} size={14} color={C.textMuted} />
      <Text style={{ color: C.textMuted, fontSize: 12, fontWeight: "600" }}>{text}</Text>
    </View>
  );
}

// tone: "neutral" (contorno discreto), "danger" (contorno rosso), "success" (pieno verde)
export function ExamAction({ label, icon, onPress, tone = "neutral", disabled, loading }) {
  const filled = tone === "success";
  const color = filled ? C.white : tone === "danger" ? C.danger : C.primary;
  const border = filled ? C.success : tone === "danger" ? "#FCA5A5" : C.border;
  return (
    <TouchableOpacity onPress={onPress} disabled={disabled} activeOpacity={0.75} accessibilityRole="button" accessibilityLabel={label}
      style={{ flexDirection: "row", alignItems: "center", gap: 6, paddingVertical: 8, paddingHorizontal: 14, borderRadius: 10,
        borderWidth: 1, borderColor: border, backgroundColor: filled ? C.success : C.white, opacity: disabled ? 0.55 : 1 }}>
      {loading ? <ActivityIndicator size="small" color={color} /> : <Ionicons name={icon} size={16} color={color} />}
      <Text style={{ color, fontSize: 13, fontWeight: "700" }}>{label}</Text>
    </TouchableOpacity>
  );
}

// onPress: tocco sulla card → dettaglio dell'esame (da lì, se è una bozza, "Modifica").
// href (solo web): la card è un link vero (<a href>). Ctrl/Cmd/Maiusc+clic, rotella e "Apri in nuova scheda"
// li gestisce il browser; il clic normale resta nella stessa scheda (onPress, senza ricaricare la pagina).
// unsaved: la voce ha modifiche locali non salvate (card "Riprendi bozza" in cima): si evidenzia.
// actions: { submissions, close, publish, cancel, remove } → funzione o null se non consentita
export default function ExamCard({ exam, status, contextLabel, busy, onPress, href, unsaved, actions }) {
  const press = (e) => {
    const ne = e?.nativeEvent || e;
    if (href && (ne?.metaKey || ne?.ctrlKey || ne?.shiftKey)) return; // nuova scheda o finestra: ci pensa il browser
    e?.preventDefault?.();
    onPress?.();
  };
  const secondary = [
    actions.submissions && <ExamAction key="subs" label="Consegne" icon="people-outline" onPress={actions.submissions} disabled={busy} />,
    actions.close && <ExamAction key="close" label="Chiudi" icon="lock-closed-outline" onPress={actions.close} disabled={busy} />,
  ].filter(Boolean);
  const primary = [
    actions.remove && <ExamAction key="remove" label="Elimina" icon="trash-outline" tone="danger" onPress={actions.remove} disabled={busy} />,
    actions.cancel && <ExamAction key="cancel" label="Annulla esame" icon="close-circle-outline" tone="danger" onPress={actions.cancel} disabled={busy} />,
    actions.publish && <ExamAction key="publish" label="Pubblica" icon="paper-plane-outline" tone="success" onPress={actions.publish} disabled={busy} loading={busy} />,
  ].filter(Boolean);

  return (
    <View style={[s.card, { marginBottom: 12, padding: 0, overflow: "hidden", flexDirection: "row" }, unsaved && { borderWidth: 1, borderColor: C.warning }]}>
      <View style={{ width: 5, backgroundColor: ACCENT[status] || C.border }} />
      <View style={{ flex: 1, padding: 16 }}>
        <TouchableOpacity onPress={press} disabled={!onPress || busy} activeOpacity={0.7} accessibilityRole={href && Platform.OS === "web" ? "link" : "button"}
          {...(href && Platform.OS === "web" ? { href } : null)}
          accessibilityLabel={`Apri ${exam.name}`} accessibilityHint="Mostra il dettaglio dell'esame">
        {/* Intestazione: icona, titolo e contesto; stato in alto a destra */}
        <View style={{ flexDirection: "row", alignItems: "flex-start", gap: 12 }}>
          <View style={{ width: 44, height: 44, borderRadius: 12, alignItems: "center", justifyContent: "center", backgroundColor: ICON_BG[status] || "#F1F5F9" }}>
            <Ionicons name="document-text-outline" size={22} color={ACCENT[status] || C.textMuted} />
          </View>
          <View style={{ flex: 1, minWidth: 0 }}>
            <Text style={{ color: C.text, fontSize: 16, fontWeight: "800" }} numberOfLines={2}>{exam.name}</Text>
            {!!contextLabel && <Text style={{ color: C.textMuted, fontSize: 13, marginTop: 2 }} numberOfLines={1}>{contextLabel}</Text>}
            {!!exam.description && <Text style={{ color: C.textMuted, fontSize: 13, marginTop: 4 }} numberOfLines={2}>{exam.description}</Text>}
          </View>
          <View style={{ alignItems: "flex-end", gap: 6 }}>
            <StatusChip label={EXAM_STATUS_LABEL[status] || status} tone={EXAM_TONE[status]} />
            {exam.isRetake && <StatusChip label="Recupero" tone="amber" />}
            {unsaved && <StatusChip label="Modifiche non salvate" tone="amber" />}
          </View>
        </View>

        {/* Dettagli */}
        <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 6, marginTop: 12 }}>
          <Pill icon="calendar-outline" text={windowLabel(exam.availableFrom, exam.availableUntil)} />
          <Pill icon="time-outline" text={`${exam.durationMinutes} min`} />
          <Pill icon="flag-outline" text={`Soglia ${exam.passingPercent}%`} />
          <Pill icon="star-outline" text={`${exam.totalPoints} punti`} />
          <Pill icon="people-outline" text={`${exam.assignedStudentsCount} ${exam.assignedStudentsCount === 1 ? "studente" : "studenti"}`} />
        </View>
        </TouchableOpacity>

        {/* Azioni: consultazione a sinistra, cambi di stato in basso a destra */}
        {(secondary.length > 0 || primary.length > 0) && (
          <View style={{ borderTopWidth: 1, borderTopColor: C.border, marginTop: 14, paddingTop: 12,
            flexDirection: "row", flexWrap: "wrap", alignItems: "center", gap: 8 }}>
            {secondary}
            {primary.length > 0 && (
              <View style={{ marginLeft: "auto", flexDirection: "row", flexWrap: "wrap", justifyContent: "flex-end", gap: 8 }}>
                {primary}
              </View>
            )}
          </View>
        )}
      </View>
    </View>
  );
}
