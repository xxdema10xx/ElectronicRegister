// src/components/ClassHoursReport.js
// Monte ore di una classe (admin e staff, GET calendar/hours): per modulo previste · programmate · svolte,
// residui, eccedenze in rosso (sempre con l'etichetta, non solo il colore), avvisi tradotti; poi totali,
// unità formative e andamento mensile. I nomi arrivano dalla versione del piano usata dal report.
import { useEffect, useState } from "react";
import { Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import StatusChip from "./StatusChip";
import { HOURS_WARNING_LABELS, formatMinutes, getClassHours, getPlanNames } from "../api/calendarService";
import { TEACHING_PLAN_MESSAGE, calendarErrorMessage } from "../api/calendarImportService";
import { MONTHS } from "../api/lessonService";

const PLAN_CODES = ["calendar.teaching-configuration-required", "calendar.published-plan-required"];

function Figure({ label, value, color }) {
  return (
    <View style={{ minWidth: 120, flexGrow: 1, paddingVertical: 4 }}>
      <Text style={{ color: C.textMuted, fontSize: 12 }}>{label}</Text>
      <Text style={{ color: color || C.text, fontSize: 15, fontWeight: "700" }}>{value}</Text>
    </View>
  );
}

function ModuleRow({ m, name }) {
  const warnings = m.warnings || [];
  return (
    <View style={[s.profileRow, { flexDirection: "column", alignItems: "stretch", gap: 4 }]}>
      <Text style={s.itemTitle}>{name}</Text>
      <View style={{ flexDirection: "row", flexWrap: "wrap", columnGap: 12 }}>
        <Figure label="Previste" value={formatMinutes(m.plannedMinutes)} />
        <Figure label="Programmate" value={formatMinutes(m.scheduledMinutes)} />
        <Figure label="Svolte" value={formatMinutes(m.deliveredMinutes)} />
        <Figure label="Da programmare" value={formatMinutes(m.remainingToScheduleMinutes ?? Math.max(0, m.plannedMinutes - m.scheduledMinutes))} />
        <Figure label="Da svolgere" value={formatMinutes(m.remainingToDeliverMinutes ?? Math.max(0, m.plannedMinutes - m.deliveredMinutes))} />
        {m.scheduledExcessMinutes > 0 && <Figure label="Programmate in eccesso" value={`+${formatMinutes(m.scheduledExcessMinutes)}`} color={C.danger} />}
        {m.deliveredExcessMinutes > 0 && <Figure label="Svolte in eccesso" value={`+${formatMinutes(m.deliveredExcessMinutes)}`} color={C.danger} />}
      </View>
      {warnings.length > 0 && (
        <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 6 }}>
          {warnings.map(w => (
            <StatusChip key={w} label={HOURS_WARNING_LABELS[w] || "Avviso"} tone={w === "calendar.over-scheduled" || w === "calendar.over-delivered" ? "red" : "amber"} />
          ))}
        </View>
      )}
    </View>
  );
}

export default function ClassHoursReport({ classId }) {
  const { token } = useAuth();
  const [state, setState] = useState({ report: null, names: null, error: null, loading: true });

  useEffect(() => {
    if (!classId) return undefined;
    let alive = true;
    setState({ report: null, names: null, error: null, loading: true });
    (async () => {
      try {
        const report = await getClassHours(classId, token);
        const names = await getPlanNames(report?.versionId, token);
        if (alive) setState({ report, names, error: null, loading: false });
      } catch (e) {
        if (alive) setState({ report: null, names: null, error: PLAN_CODES.includes(e?.code) ? TEACHING_PLAN_MESSAGE : calendarErrorMessage(e), loading: false });
      }
    })();
    return () => { alive = false; };
  }, [classId, token]);

  if (!classId) return <Text style={s.itemSub}>Scegli una classe.</Text>;
  if (state.loading) return <Text style={s.itemSub}>Caricamento…</Text>;
  if (state.error) return <Text accessibilityRole="alert" style={{ color: C.danger, fontSize: 14 }}>{state.error}</Text>;

  const { report, names } = state;
  const modules = report?.modules || [];
  const units = report?.units || [];
  const months = (report?.months || []).slice().sort((a, b) => a.year - b.year || a.month - b.month);
  const total = (k) => report?.[k] ?? modules.reduce((sum, m) => sum + (m[k] || 0), 0);

  if (modules.length === 0) return <Text style={s.itemSub}>Il piano formativo della classe non ha moduli.</Text>;

  return (
    <View>
      <Text style={[s.itemTitle, { marginBottom: 4 }]}>Moduli</Text>
      {modules.map(m => <ModuleRow key={m.moduleId} m={m} name={names?.modules[m.moduleId] || "Modulo"} />)}

      <Text style={[s.itemTitle, { marginTop: 16, marginBottom: 4 }]}>Totale della classe</Text>
      <View style={{ flexDirection: "row", flexWrap: "wrap", columnGap: 12 }}>
        <Figure label="Previste" value={formatMinutes(total("plannedMinutes"))} />
        <Figure label="Programmate" value={formatMinutes(total("scheduledMinutes"))} />
        <Figure label="Svolte" value={formatMinutes(total("deliveredMinutes"))} />
      </View>

      {units.length > 0 && (
        <View style={{ marginTop: 16 }}>
          <Text style={[s.itemTitle, { marginBottom: 4 }]}>Per unità formativa</Text>
          {units.map(u => (
            <View key={u.unitId} style={[s.profileRow, { flexDirection: "column", alignItems: "stretch" }]}>
              <Text style={{ color: C.text, fontWeight: "700" }}>{names?.units[u.unitId] || "Unità formativa"}</Text>
              <View style={{ flexDirection: "row", flexWrap: "wrap", columnGap: 12 }}>
                <Figure label="Previste" value={formatMinutes(u.plannedMinutes)} />
                <Figure label="Programmate" value={formatMinutes(u.scheduledMinutes)} />
                <Figure label="Svolte" value={formatMinutes(u.deliveredMinutes)} />
              </View>
            </View>
          ))}
        </View>
      )}

      {months.length > 0 && (
        <View style={{ marginTop: 16 }}>
          <Text style={[s.itemTitle, { marginBottom: 4 }]}>Andamento mensile</Text>
          {months.map(m => (
            <View key={`${m.year}-${m.month}`} style={[s.profileRow, { flexWrap: "wrap", columnGap: 12 }]}>
              <Text style={[s.profileKey, { minWidth: 130 }]}>{`${MONTHS[m.month - 1] || m.month} ${m.year}`}</Text>
              <Text style={{ color: C.text, fontSize: 13 }}>Programmate {formatMinutes(m.scheduledMinutes)} · Svolte {formatMinutes(m.deliveredMinutes)}</Text>
            </View>
          ))}
        </View>
      )}
    </View>
  );
}
