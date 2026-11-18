// src/screens/CalendarScreen.js
// Pagina "Calendario" di admin e staff, divisa in viste:
//  - Lezioni: classe + settimana, bozze e pubblicate, "Mostra annullate"; dal dettaglio si annulla, sposta,
//    sostituisce il docente (LessonActions).
//  - Importa: caricamento del mese da file e pubblicazione (CalendarImportScreen).
//  - Monte ore: ore previste, programmate e svolte per modulo della classe (ClassHoursReport).
//  - Impostazioni: fuso orario e giorni di anticipo (CalendarSettingsForm).
// Su web vista, classe e settimana stanno nell'URL (?vista=…&classe=…&settimana=…).
import { useCallback, useEffect, useState } from "react";
import { ScrollView, Text, TouchableOpacity, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import SelectField from "../components/SelectField";
import CheckboxField from "../components/CheckboxField";
import LessonCalendar from "../components/LessonCalendar";
import WeekNavigator from "../components/WeekNavigator";
import LessonDetail from "../components/LessonDetail";
import LessonActions from "../components/LessonActions";
import CalendarImportScreen from "./CalendarImportScreen";
import CalendarSettingsForm from "../components/CalendarSettingsForm";
import ClassHoursReport from "../components/ClassHoursReport";
import { getClassesForPicker, getSchedule } from "../api/lessonService";
import { getTeachers } from "../api/teacherService";
import { calendarErrorMessage, getCalendarSettings } from "../api/calendarImportService";
import { useLessons, useWeek } from "../hooks/useWeekSchedule";
import { readSearchParams, replaceSearchParams } from "../navigation/examRoutes";

export const CALENDAR_VIEWS = [
  { key: "lezioni", label: "Lezioni" },
  { key: "importa", label: "Importa" },
  { key: "ore", label: "Monte ore" },
  { key: "impostazioni", label: "Impostazioni" },
];

function SettingsView() {
  const { token } = useAuth();
  const [settings, setSettings] = useState(undefined);
  const [error, setError] = useState(null);
  const [version, setVersion] = useState(0);
  useEffect(() => {
    let alive = true;
    getCalendarSettings(token)
      .then(d => { if (alive) { setSettings(d); setError(null); } })
      .catch(e => { if (alive) { setSettings(null); setError(calendarErrorMessage(e)); } });
    return () => { alive = false; };
  }, [token, version]);
  return (
    <Card>
      {settings === undefined ? <Text style={s.itemSub}>Caricamento…</Text>
        : error ? <Text style={{ color: C.danger }}>{error}</Text>
        : <CalendarSettingsForm settings={settings} onSaved={() => setVersion(v => v + 1)} />}
    </Card>
  );
}

function Tabs({ value, onChange, views }) {
  return (
    <View accessibilityRole="tablist" style={{ flexDirection: "row", flexWrap: "wrap", gap: 8 }}>
      {views.map(v => {
        const active = v.key === value;
        return (
          <TouchableOpacity key={v.key} onPress={() => onChange(v.key)} accessibilityRole="tab" accessibilityState={{ selected: active }}
            style={{ paddingVertical: 8, paddingHorizontal: 14, borderRadius: 20, borderWidth: 1, borderColor: active ? C.primary : C.border, backgroundColor: active ? C.primary : C.white }}>
            <Text style={{ color: active ? C.white : C.text, fontWeight: "700", fontSize: 14 }}>{v.label}</Text>
          </TouchableOpacity>
        );
      })}
    </View>
  );
}

function LessonsView({ role, classes, classId, setClassId, teachers }) {
  const { token } = useAuth();
  const { week, prev, next, today } = useWeek();
  const [showCancelled, setShowCancelled] = useState(false);
  const [openLessonId, setOpenLessonId] = useState(null);
  const [detailKey, setDetailKey] = useState(0);
  const { lessons, error, reload } = useLessons(
    (signal) => (classId
      ? getSchedule({ role, classId, from: week.from, to: week.to, includeCancelled: showCancelled, publication: null, signal }, token)
      : Promise.resolve([])),
    [role, classId, week.from, showCancelled, token],
  );

  const onChanged = useCallback(() => { reload(); setDetailKey(k => k + 1); }, [reload]);

  return (
    <Card>
      <Text style={s.sectionTitle}>Lezioni della classe</Text>
      <Text style={[s.itemSub, { marginBottom: 12 }]}>Bozze e lezioni pubblicate. Tocca una lezione per vederne i dettagli, annullarla, spostarla o sostituire il docente.</Text>
      <SelectField label="Classe" value={classId} options={classes} onSelect={setClassId} getLabel={c => c.name} placeholder="Seleziona classe" emptyMessage="Nessuna classe" />
      {!classId ? <Text style={s.itemSub}>Scegli una classe.</Text> : (
        <>
          <WeekNavigator week={week} onPrev={prev} onNext={next} onToday={today} />
          <CheckboxField label="Mostra annullate" value={showCancelled} onChange={setShowCancelled} />
          {!!error && <Text style={{ color: C.danger, fontSize: 13, marginBottom: 8 }}>{calendarErrorMessage(error)}</Text>}
          <LessonCalendar lessons={lessons} week={week} showDraft onPressLesson={l => setOpenLessonId(l.id)} />
        </>
      )}
      <LessonDetail lessonId={openLessonId} role={role} teachers={teachers} reloadKey={detailKey} onClose={() => setOpenLessonId(null)}
        actions={(lesson) => <LessonActions lesson={lesson} teachers={teachers} onChanged={onChanged} />} />
    </Card>
  );
}

export default function CalendarScreen({ role }) {
  const { token } = useAuth();
  const views = CALENDAR_VIEWS;
  const initial = readSearchParams();
  const [view, setView] = useState(views.some(v => v.key === initial.vista) ? initial.vista : "lezioni");
  const [classes, setClasses] = useState([]);
  const [classId, setClassId] = useState(initial.classe || null);
  const [teachers, setTeachers] = useState([]);
  // La vista Importa resta montata (nascosta) dopo la prima apertura: cambiando scheda non si perdono
  // mese, classi, motivazione, file e anteprima. Prima si smontava e ogni ritorno ripartiva da zero.
  const [importOpened, setImportOpened] = useState(view === "importa");
  useEffect(() => { if (view === "importa") setImportOpened(true); }, [view]);

  useEffect(() => {
    let alive = true;
    getClassesForPicker(token)
      .then(list => { if (alive) { setClasses(list); setClassId(prev => (prev && list.some(c => c.id === prev) ? prev : list[0]?.id || null)); } })
      .catch(() => { if (alive) setClasses([]); });
    getTeachers(token).then(d => { if (alive) setTeachers(Array.isArray(d) ? d : []); }).catch(() => {});
    return () => { alive = false; };
  }, [token]);

  useEffect(() => { replaceSearchParams({ vista: view === "lezioni" ? null : view, classe: view === "lezioni" || view === "ore" ? classId : null }); }, [view, classId]);

  return (
    <View style={{ flex: 1 }}>
      <View style={{ paddingHorizontal: 16, paddingTop: 16 }}>
        <Tabs value={view} onChange={setView} views={views} />
      </View>
      {(importOpened || view === "importa") && (
        <View style={{ flex: 1, display: view === "importa" ? "flex" : "none" }} accessibilityElementsHidden={view !== "importa"} importantForAccessibility={view === "importa" ? "auto" : "no-hide-descendants"}>
          <CalendarImportScreen />
        </View>
      )}
      {view !== "importa" && (
        <ScrollView contentContainerStyle={{ padding: 16, gap: 16, paddingBottom: 32 }}>
          {view === "lezioni" && <LessonsView role={role} classes={classes} classId={classId} setClassId={setClassId} teachers={teachers} />}
          {view === "ore" && (
            <Card>
              <Text style={s.sectionTitle}>Monte ore</Text>
              <Text style={[s.itemSub, { marginBottom: 12 }]}>Per ogni modulo del piano formativo: ore previste, programmate (lezioni pubblicate non annullate) e svolte. Contano tutti i docenti, titolari e supplenti.</Text>
              <SelectField label="Classe" value={classId} options={classes} onSelect={setClassId} getLabel={c => c.name} placeholder="Seleziona classe" emptyMessage="Nessuna classe" />
              <ClassHoursReport classId={classId} />
            </Card>
          )}
          {view === "impostazioni" && <SettingsView />}
        </ScrollView>
      )}
    </View>
  );
}
