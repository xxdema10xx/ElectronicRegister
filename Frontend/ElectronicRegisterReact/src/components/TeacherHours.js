// src/components/TeacherHours.js
// "Le mie ore" del docente: per classe + modulo le ore previste dal modulo, le mie programmate (lezioni non
// annullate) e le mie svolte (solo lezioni Completed), in un intervallo (di default l'anno scolastico).
// Sono le ore del docente, non il totale del modulo: le lezioni dei supplenti non sono visibili qui,
// quindi non si mostrano residui né avvisi di sforamento.
import { useCallback, useEffect, useRef, useState } from "react";
import { Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Btn from "./Btn";
import Input from "./Input";
import { formatMinutes, formatShortDate, getLessonModule, getTeacherHours, rangeError, schoolYear } from "../api/calendarService";
import { getAllLessons } from "../api/lessonService";
import { calendarErrorMessage } from "../api/calendarImportService";

function Figure({ label, value }) {
  return (
    <View style={{ minWidth: 140, flexGrow: 1, paddingVertical: 4 }}>
      <Text style={{ color: C.textMuted, fontSize: 12 }}>{label}</Text>
      <Text style={{ color: C.text, fontSize: 15, fontWeight: "700" }}>{value}</Text>
    </View>
  );
}

export default function TeacherHours() {
  const { token } = useAuth();
  const initial = schoolYear();
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [range, setRange] = useState(initial);
  const [rows, setRows] = useState(null);
  const [error, setError] = useState(null);
  const cache = useRef(new Map()); // ore previste per modulo: una chiamata per modulo

  const load = useCallback(async (r) => {
    setRows(null); setError(null);
    try {
      const out = await getTeacherHours(r, token, {
        loadLessons: (q) => getAllLessons("/institution/calendar/teachers/me", { from: q.from, to: q.to }, token),
        loadModule: (lessonId) => getLessonModule(lessonId, token),
        moduleCache: cache.current,
      });
      setRows(out);
    } catch (e) { setRows([]); setError(calendarErrorMessage(e)); }
  }, [token]);

  useEffect(() => { load(range); }, [load, range]);

  const formError = rangeError({ from: from.trim(), to: to.trim() });
  const apply = () => { if (!formError) setRange({ from: from.trim(), to: to.trim() }); };

  return (
    <View>
      <Text style={{ color: C.text, fontSize: 14, marginBottom: 12 }}>
        Queste sono le <Text style={{ fontWeight: "700" }}>tue</Text> ore, non il totale del modulo: le lezioni fatte da un supplente non
        compaiono qui. Per questo «previste − mie» non è il residuo del modulo.
      </Text>
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
        <View style={{ flex: 1, minWidth: 140 }}><Input label="Dal (AAAA-MM-GG)" value={from} onChangeText={setFrom} autoCapitalize="none" /></View>
        <View style={{ flex: 1, minWidth: 140 }}><Input label="Al (AAAA-MM-GG)" value={to} onChangeText={setTo} autoCapitalize="none" /></View>
      </View>
      {!!formError && <Text style={{ color: C.danger, fontSize: 12, marginBottom: 8 }}>{formError}</Text>}
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8, marginBottom: 12 }}>
        <Btn label="Aggiorna" onPress={apply} disabled={!!formError} style={{ paddingVertical: 10 }} />
        <Btn label="Anno scolastico corrente" variant="ghost" style={{ paddingVertical: 10 }}
          onPress={() => { const y = schoolYear(); setFrom(y.from); setTo(y.to); setRange(y); }} />
      </View>
      <Text style={[s.itemSub, { marginBottom: 8 }]}>Periodo: {formatShortDate(range.from)} – {formatShortDate(range.to)}</Text>

      {!!error && <Text accessibilityRole="alert" style={{ color: C.danger, fontSize: 14 }}>{error}</Text>}
      {rows === null ? <Text style={s.itemSub}>Caricamento…</Text>
        : rows.length === 0 && !error ? <Text style={s.itemSub}>Nessuna lezione pubblicata in questo periodo.</Text>
        : rows.map(r => (
          <View key={`${r.classId}|${r.moduleId}`} style={[s.profileRow, { flexDirection: "column", alignItems: "stretch" }]}>
            <Text style={s.itemTitle}>{r.className} · {r.moduleName}</Text>
            <View style={{ flexDirection: "row", flexWrap: "wrap", columnGap: 12 }}>
              <Figure label="Previste dal modulo" value={r.plannedMinutes == null ? "—" : formatMinutes(r.plannedMinutes)} />
              <Figure label="Mie programmate" value={formatMinutes(r.scheduledMinutes)} />
              <Figure label="Mie svolte" value={formatMinutes(r.deliveredMinutes)} />
            </View>
          </View>
        ))}
    </View>
  );
}
