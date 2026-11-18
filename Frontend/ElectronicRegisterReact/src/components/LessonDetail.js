// src/components/LessonDetail.js
// Scheda di una lezione: data, orario, classe, modulo, docente, aula, descrizione e stato.
// Admin e staff vedono anche lo "Storico modifiche" con i motivi (GET lessons/{id}/history, 20 alla volta);
// docenti e studenti no: il server non espone loro i motivi e qui non si ricavano da altre fonti.
// `actions` (admin e staff nella pagina Calendario) riceve la lezione letta e mostra i comandi.
import { useCallback, useEffect, useState } from "react";
import { Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import FormModal from "./FormModal";
import Btn from "./Btn";
import StatusChip from "./StatusChip";
import { lessonLabels, toLesson } from "../api/lessonService";
import {
  HISTORY_PAGE_SIZE, PUBLICATION_LABELS, STATUS_LABELS, describeHistory, formatLongDate, getLesson, getLessonHistory, hhmm,
} from "../api/calendarService";
import { calendarErrorMessage } from "../api/calendarImportService";
import { getTeachers } from "../api/teacherService";

export const isManager = (role) => role === "admin" || role === "staff";

function Row({ label, value }) {
  if (!value) return null;
  return (
    <View style={[s.profileRow, { flexDirection: "column", alignItems: "flex-start", gap: 2 }]}>
      <Text style={s.profileKey}>{label}</Text>
      <Text style={{ color: C.text, fontSize: 15, flexShrink: 1 }} selectable>{value}</Text>
    </View>
  );
}

function History({ lessonId, teacherName, version }) {
  const { token } = useAuth();
  const [items, setItems] = useState(null);
  const [page, setPage] = useState(1);
  const [more, setMore] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const load = useCallback(async (p, append) => {
    setLoading(true); setError(null);
    try {
      const list = await getLessonHistory(lessonId, p, token);
      setItems(prev => (append ? [...(prev || []), ...list] : list));
      setMore(list.length === HISTORY_PAGE_SIZE);
      setPage(p);
    } catch (e) { setError(calendarErrorMessage(e)); if (!append) setItems([]); }
    finally { setLoading(false); }
  }, [lessonId, token]);

  useEffect(() => { load(1, false); }, [load, version]);

  return (
    <View style={{ marginTop: 16 }}>
      <Text style={[s.itemTitle, { marginBottom: 6 }]}>Storico modifiche</Text>
      {items === null ? <Text style={s.itemSub}>Caricamento…</Text>
        : items.length === 0 && !error ? <Text style={s.itemSub}>Nessuna modifica registrata.</Text>
        : items.map(entry => {
          const h = describeHistory(entry, teacherName);
          return (
            <View key={h.key} style={[s.profileRow, { flexDirection: "column", alignItems: "flex-start", gap: 2 }]}>
              <Text style={{ color: C.text, fontWeight: "700", fontSize: 14 }}>{h.operation}{h.when ? ` · ${h.when}` : ""}</Text>
              {!!h.change && <Text style={{ color: C.text, fontSize: 13 }}>{h.change}</Text>}
              {!!h.reason && <Text style={{ color: C.textMuted, fontSize: 13, flexShrink: 1 }}>Motivo: {h.reason}</Text>}
            </View>
          );
        })}
      {!!error && <Text style={{ color: C.danger, fontSize: 13, marginTop: 6 }}>{error}</Text>}
      {more && <Btn variant="ghost" label="Mostra altre modifiche" onPress={() => load(page + 1, true)} loading={loading} style={{ marginTop: 8, paddingVertical: 8 }} />}
    </View>
  );
}

// lessonId: lezione da mostrare (null = chiuso). teachers: [{ id, firstName, lastName }] per i nomi; se mancano,
// per admin e staff si leggono qui (docenti e studenti non ne hanno bisogno: il nome è nella lezione).
// reloadKey: cambiando, si rilegge la lezione (dopo un comando). actions(lesson, reload): comandi extra.
export default function LessonDetail({ lessonId, role, onClose, teachers: teachersProp, actions, reloadKey = 0 }) {
  const { token } = useAuth();
  const [lesson, setLesson] = useState(null);
  const [error, setError] = useState(null);
  const [version, setVersion] = useState(0);

  const reload = useCallback(() => setVersion(v => v + 1), []);
  const manager = isManager(role);
  const [loadedTeachers, setLoadedTeachers] = useState([]);
  useEffect(() => {
    if (!manager || teachersProp || !lessonId) return undefined;
    let alive = true;
    getTeachers(token).then(d => { if (alive) setLoadedTeachers(Array.isArray(d) ? d : []); }).catch(() => {});
    return () => { alive = false; };
  }, [manager, teachersProp, lessonId, token]);
  const teachers = teachersProp || loadedTeachers;

  useEffect(() => {
    if (!lessonId) return undefined;
    let alive = true;
    setError(null);
    getLesson(lessonId, token)
      .then(d => { if (alive) setLesson(d); })
      .catch(e => { if (alive) { setLesson(null); setError(calendarErrorMessage(e)); } });
    return () => { alive = false; };
  }, [lessonId, token, version, reloadKey]);

  useEffect(() => { if (!lessonId) { setLesson(null); setError(null); } }, [lessonId]);

  const teacherName = useCallback((id) => {
    const t = teachers.find(x => x.id === id);
    return t ? `${t.firstName || ""} ${t.lastName || ""}`.trim() : null;
  }, [teachers]);

  const view = lesson ? toLesson(lesson) : null;
  const labels = view ? lessonLabels(view, { showDraft: manager }) : [];
  const original = view?.substitute ? teacherName(lesson.originalTeacherId) : null;

  return (
    <FormModal visible={!!lessonId} title={lesson?.moduleName || "Lezione"} onClose={onClose}>
      {!lesson && !error && <Text style={s.itemSub}>Caricamento…</Text>}
      {!!error && <Text style={{ color: C.danger, fontSize: 14 }}>{error}</Text>}
      {lesson && (
        <View>
          {labels.length > 0 && (
            <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 6, marginBottom: 8 }}>
              {labels.map(l => <StatusChip key={l.key} label={l.label} tone={l.tone} />)}
            </View>
          )}
          <Row label="Data" value={formatLongDate(lesson.date)} />
          <Row label="Orario" value={`${hhmm(lesson.startsAt)}–${hhmm(lesson.endsAt)}`} />
          <Row label="Classe" value={lesson.className} />
          <Row label="Modulo" value={lesson.moduleName} />
          <Row label={view.substitute ? "Docente (supplenza)" : "Docente"} value={lesson.teacherName || teacherName(lesson.teacherId)} />
          <Row label="Docente titolare" value={original} />
          <Row label="Aula" value={lesson.room} />
          <Row label="Descrizione" value={lesson.description} />
          <Row label="Stato" value={[STATUS_LABELS[lesson.status] || lesson.status, manager ? PUBLICATION_LABELS[lesson.publication] : null].filter(Boolean).join(" · ")} />
          {lesson.status === "Completed" && lesson.deliveredMinutes != null && <Row label="Minuti svolti" value={String(lesson.deliveredMinutes)} />}
          {actions ? actions(lesson, reload) : null}
          {manager && <History lessonId={lesson.id} teacherName={teacherName} version={`${version}-${reloadKey}`} />}
        </View>
      )}
    </FormModal>
  );
}
