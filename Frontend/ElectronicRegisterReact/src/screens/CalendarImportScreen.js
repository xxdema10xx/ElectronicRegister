// src/screens/CalendarImportScreen.js
// Calendario di admin e staff: carica le lezioni del mese da un file Excel/CSV e le pubblica.
// Se il calendario dell'istituto non è configurato mostra prima il modulo delle impostazioni.
// 1. Anteprima del file (errori, conflitti di orario, impatto sul monte ore)
// 2. Conferma: le lezioni diventano bozze
// 3. Pubblicazione del mese per ogni classe: solo da qui docenti e studenti le vedono
import { useCallback, useEffect, useMemo, useState } from "react";
import { ScrollView, Text, View } from "react-native";
import * as DocumentPicker from "expo-document-picker";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import Card from "../components/Card";
import Btn from "../components/Btn";
import Input from "../components/Input";
import SelectField from "../components/SelectField";
import CheckboxField from "../components/CheckboxField";
import Loader from "../components/Loader";
import CalendarSettingsForm from "../components/CalendarSettingsForm";
import { calendarTemplateCsv, canDownload, downloadCsv, teachersCsv } from "../utils/csvDownload";
import { notify, confirmAction } from "../utils/dialogs";
import { getClassesForPicker } from "../api/lessonService";
import { getTeachers } from "../api/teacherService";
import {
  FILE_TOO_LARGE_MESSAGE, HOUR_WARNING_LABELS, IMPORT_MODES, MAX_UPLOAD_BYTES, calendarErrorMessage, confirmImport, confirmLabel, conflictMessage,
  getCalendarSettings, getMonthStatus, isCsvFile, issueMessage, issueParts, maxUploadLabel, precheckCalendarCsv, previewImport, publishPeriod, readFileText,
} from "../api/calendarImportService";
import { formatMinutes } from "../api/calendarService";
import { plural } from "../utils/plural";

const MONTH_NAMES = ["Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno", "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];
const MAX_ROWS_SHOWN = 100;
const FILE_TYPES = [
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "text/csv", "text/comma-separated-values",
  "application/vnd.ms-excel", ".xlsx", ".csv",
];

// Dal mese scorso a undici mesi avanti
function monthOptions(now = new Date()) {
  return Array.from({ length: 13 }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - 1 + i, 1);
    return { id: `${d.getFullYear()}-${d.getMonth() + 1}`, year: d.getFullYear(), month: d.getMonth() + 1, name: `${MONTH_NAMES[d.getMonth()]} ${d.getFullYear()}` };
  });
}

const formatDay = (date) => { const [y, m, d] = String(date).split("-"); return d && m ? `${d}/${m}/${y}` : String(date || ""); };
const hasErrors = (row) => (row.issues || []).some(i => !i.warning) || (row.conflicts || []).length > 0;

function Section({ title, subtitle, children }) {
  return (
    <Card>
      <Text style={s.sectionTitle}>{title}</Text>
      {!!subtitle && <Text style={[s.itemSub, { marginBottom: 12 }]}>{subtitle}</Text>}
      {children}
    </Card>
  );
}

function Stat({ label, value, color }) {
  return (
    <View style={{ minWidth: 110, paddingVertical: 6 }}>
      <Text style={{ fontSize: 22, fontWeight: "800", color: color || C.text }}>{value}</Text>
      <Text style={s.itemSub}>{label}</Text>
    </View>
  );
}

const Note = ({ children, color = C.textMuted }) => <Text style={{ color, fontSize: 13, marginBottom: 6 }}>{children}</Text>;

export default function CalendarImportScreen() {
  const { token } = useAuth();
  const months = useMemo(() => monthOptions(), []);

  const [settings, setSettings] = useState(undefined); // undefined: caricamento · null: da configurare
  const [settingsError, setSettingsError] = useState(null);
  const [settingsVersion, setSettingsVersion] = useState(0);
  const [classes, setClasses] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [showTeacherIds, setShowTeacherIds] = useState(false);

  const [monthId, setMonthId] = useState(months[2].id); // mese prossimo
  const [classIds, setClassIds] = useState([]);
  const [mode, setMode] = useState("AppendDraft");
  const [reason, setReason] = useState("");
  const [file, setFile] = useState(null);

  const [preview, setPreview] = useState(null);
  const [previewing, setPreviewing] = useState(false);
  const [ack, setAck] = useState(false);
  const [confirming, setConfirming] = useState(false);

  const [status, setStatus] = useState({}); // classId → { loading, drafts, published, period, error }
  const [publishing, setPublishing] = useState(null);

  const { year, month, name: monthName } = months.find(m => m.id === monthId) || months[2];
  const className = useCallback((id) => classes.find(c => c.id === id)?.name || "Classe", [classes]);

  useEffect(() => {
    let alive = true;
    getCalendarSettings(token)
      .then(d => { if (alive) { setSettings(d); setSettingsError(null); } })
      .catch(e => { if (alive) { setSettings(null); setSettingsError(calendarErrorMessage(e)); } });
    return () => { alive = false; };
  }, [token, settingsVersion]);

  useEffect(() => {
    let alive = true;
    getClassesForPicker(token).then(d => { if (alive) setClasses(d); }).catch(() => {});
    getTeachers(token)
      .then(d => { if (alive) setTeachers((Array.isArray(d) ? d : []).slice().sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`))); })
      .catch(() => {});
    return () => { alive = false; };
  }, [token]);

  // L'anteprima vale solo per mese, classi e modalità con cui è stata generata
  useEffect(() => { setPreview(null); setAck(false); }, [monthId, classIds, mode]);

  const loadStatus = useCallback(async () => {
    if (classIds.length === 0) { setStatus({}); return; }
    setStatus(Object.fromEntries(classIds.map(id => [id, { loading: true }])));
    const entries = await Promise.all(classIds.map(async id => {
      try { return [id, await getMonthStatus(id, year, month, token)]; }
      catch (e) { return [id, { error: calendarErrorMessage(e) }]; }
    }));
    setStatus(Object.fromEntries(entries));
  }, [classIds, year, month, token]);

  useEffect(() => { loadStatus(); }, [loadStatus]);

  const toggleClass = (id) => setClassIds(list => list.includes(id) ? list.filter(x => x !== id) : [...list, id]);

  // Un nuovo file (anche rifiutato) sostituisce il precedente: anteprima e conferma degli avvisi si azzerano
  const clearFile = () => { setFile(null); setPreview(null); setAck(false); };

  async function pickFile() {
    try {
      const res = await DocumentPicker.getDocumentAsync({ type: FILE_TYPES, multiple: false, copyToCacheDirectory: true });
      if (res.canceled || !res.assets?.length) return;
      const asset = res.assets[0];
      if (!/\.(xlsx|csv)$/i.test(asset.name || "")) { clearFile(); notify("File non supportato", "Scegli un file .xlsx o .csv."); return; }
      // Limite reale del server web (EXPO_PUBLIC_CALENDAR_MAX_UPLOAD_MB, default 1 MB): si avvisa prima
      // dell'invio invece di ricevere un 413 o una connessione chiusa
      const size = asset.size ?? asset.file?.size;
      if (size && size > MAX_UPLOAD_BYTES) { clearFile(); notify("File troppo grande", `Il file può essere al massimo di ${maxUploadLabel()}. ${FILE_TOO_LARGE_MESSAGE}`); return; }
      clearFile(); setFile(asset);
    } catch (e) { clearFile(); notify("Errore", e.message); }
  }

  async function runPreview() {
    if (!file) return notify("Attenzione", "Scegli il file con le lezioni.");
    if (classIds.length === 0) return notify("Attenzione", "Seleziona almeno una classe.");
    if (!reason.trim()) return notify("Attenzione", `Indica una motivazione (es. "Orario di ${monthName.toLowerCase()}").`);
    // CSV: i file che il backend rifiuterebbe comunque non consumano una delle 20 anteprime
    // (tabs: per tradurre l'InvalidHeader che il backend dà ai file separati dal tab)
    let tabs = false;
    if (isCsvFile(file)) {
      const text = await readFileText(file);
      if (text !== null) {
        const check = precheckCalendarCsv(text);
        tabs = check.tabSeparated;
        if (!check.ok) return notify("File da correggere", check.message);
      }
    }
    setPreviewing(true); setPreview(null); setAck(false);
    try { setPreview(await previewImport({ file, year, month, classIds, mode, reason }, token)); }
    catch (e) {
      // Oltre il limite Nginx può chiudere la connessione senza risposta: sembra un errore di rete
      const closed = !e?.status && !/tempo/i.test(e?.message || "");
      notify("Anteprima non riuscita", closed ? `${FILE_TOO_LARGE_MESSAGE} Se il file è piccolo, controlla la connessione.` : calendarErrorMessage(e, { tabSeparated: tabs }));
    }
    finally { setPreviewing(false); }
  }

  const rows = preview?.rows || [];
  const errorRows = rows.filter(hasErrors);
  const warningRows = rows.filter(r => !hasErrors(r) && (r.issues || []).some(i => i.warning));
  const hourWarnings = (preview?.hours || []).filter(h => h.publishedMinutes + h.draftMinutesAfterImport !== h.plannedMinutes);
  const needsAck = warningRows.length > 0 || rows.some(r => (r.issues || []).some(i => i.warning)) || hourWarnings.length > 0;
  const canConfirm = !!preview?.canConfirm && (!needsAck || ack);
  const readyRows = preview ? (preview.validRows ?? rows.length - errorRows.length) : 0;
  const underScheduled = hourWarnings.some(h => h.warning === "calendar.under-scheduled" || (!h.warning && h.publishedMinutes + h.draftMinutesAfterImport < h.plannedMinutes));
  // Avvisi uguali su tutte le righe (es. colonne ignorate): si mostrano una volta sola
  const commonWarnings = useMemo(() => {
    if (!rows.length) return [];
    const key = i => `${i.field}|${i.code}`;
    const first = (rows[0].issues || []).filter(i => i.warning);
    return first.filter(i => rows.every(r => (r.issues || []).some(j => j.warning && key(j) === key(i))));
  }, [rows]);
  const isCommon = (i) => commonWarnings.some(c => c.field === i.field && c.code === i.code);
  const moduleNames = useMemo(() => Object.fromEntries(rows.filter(r => r.lesson?.moduleId && r.moduleName).map(r => [r.lesson.moduleId, r.moduleName])), [rows]);

  async function runConfirm() {
    setConfirming(true);
    try {
      const receipt = await confirmImport(preview, { acknowledgeWarnings: ack, reason }, token);
      const n = receipt?.lessonIds?.length ?? preview.rows.length;
      setPreview(null); setAck(false); setFile(null);
      notify("Lezioni caricate", `${plural(n, "lezione creata", "lezioni create")} in bozza per ${monthName}. Per renderle visibili a docenti e studenti pubblica il mese qui sotto.`);
      loadStatus();
    } catch (e) {
      notify("Conferma non riuscita", calendarErrorMessage(e));
      if (["calendar.preview-expired-or-consumed", "calendar.preview-digest-conflict", "calendar.preview-stale-or-invalid"].includes(e.code)) setPreview(null);
    } finally { setConfirming(false); }
  }

  function askPublish(classId) {
    const st = status[classId];
    if (!reason.trim()) return notify("Attenzione", "Indica una motivazione nel campo \"Motivazione\" qui sopra.");
    if (!st?.period) return;
    confirmAction("Pubblica il calendario",
      st.drafts === 1
        ? `La lezione in bozza di ${className(classId)} per ${monthName} diventerà visibile a docenti e studenti.`
        : `Le ${st.drafts} lezioni in bozza di ${className(classId)} per ${monthName} diventeranno visibili a docenti e studenti.`,
      async () => {
        setPublishing(classId);
        try {
          await publishPeriod(classId, year, month, st.period.revision, reason, token);
          notify("Calendario pubblicato", `${className(classId)}: le lezioni di ${monthName} sono ora visibili.`);
        } catch (e) { notify("Pubblicazione non riuscita", calendarErrorMessage(e)); }
        finally { setPublishing(null); loadStatus(); }
      }, "Pubblica");
  }

  if (settings === undefined) return <Loader />;
  const downloads = canDownload();

  return (
    <ScrollView contentContainerStyle={{ padding: 16, gap: 16, paddingBottom: 32 }}>
      {settings === null && settingsError && (
        <Card style={{ borderLeftWidth: 4, borderLeftColor: C.warning }}>
          <Text style={s.itemTitle}>Calendario non disponibile</Text>
          <Text style={s.itemSub}>{settingsError}</Text>
        </Card>
      )}
      {settings === null && !settingsError && (
        <Card style={{ borderLeftWidth: 4, borderLeftColor: C.warning }}>
          <CalendarSettingsForm settings={null} onSaved={() => setSettingsVersion(v => v + 1)} />
        </Card>
      )}

      <Section title="1. Mese e classi" subtitle="Le lezioni del file devono cadere tutte nel mese scelto e riguardare solo le classi selezionate.">
        <SelectField label="Mese" value={monthId} options={months} onSelect={setMonthId} getLabel={m => m.name} />
        <Text style={s.label}>Classi</Text>
        {classes.length === 0 ? <Note>Nessuna classe disponibile.</Note> : (
          <View style={{ flexDirection: "row", flexWrap: "wrap", columnGap: 24 }}>
            {classes.map(c => (
              // maxWidth: i nomi lunghi vanno a capo invece di uscire dal riquadro (telefono)
              <View key={c.id} style={{ maxWidth: "100%" }}>
                <CheckboxField label={c.name} value={classIds.includes(c.id)} onChange={() => toggleClass(c.id)} />
              </View>
            ))}
          </View>
        )}
        <Input label="Motivazione (obbligatoria, resta nello storico)" value={reason} onChangeText={setReason} maxLength={500} placeholder={`Es. Orario di ${monthName.toLowerCase()}`} />
      </Section>

      <Section title="2. Carica il file" subtitle={`File .xlsx o .csv, al massimo ${maxUploadLabel()} e 2000 righe. La prima riga contiene le intestazioni.`}>
        <Note>Colonne obbligatorie: <Text style={{ fontWeight: "700" }}>Data</Text> (AAAA-MM-GG), <Text style={{ fontWeight: "700" }}>Ora inizio</Text> e <Text style={{ fontWeight: "700" }}>Ora fine</Text> (HH:mm), <Text style={{ fontWeight: "700" }}>Classe</Text> (nome), <Text style={{ fontWeight: "700" }}>Modulo</Text> (codice o nome del modulo del piano formativo), <Text style={{ fontWeight: "700" }}>Docente</Text>.</Note>
        <Note>Facoltative: Aula, Descrizione, RiferimentoEsterno. Le altre colonne vengono ignorate.</Note>
        <Note>Esempio di riga: <Text style={{ fontFamily: "monospace" }}>2026-12-01;09:00;11:00;SW-25A;Basi di dati;ID del docente;Lab 1;Introduzione a SQL</Text></Note>
        <Note>Nei file .xlsx date e ore vanno scritte come testo (<Text style={{ fontFamily: "monospace" }}>2026-12-01</Text>, <Text style={{ fontFamily: "monospace" }}>09:00</Text>) oppure nei formati data e ora standard di Excel. Formule e più fogli non sono ammessi.</Note>
        <Note>Il docente deve essere il titolare della materia del modulo nella classe; per un supplente importa la lezione con il titolare e poi usa «Sostituisci docente» nella vista Lezioni.</Note>
        {downloads && (
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8, marginBottom: 10 }}>
            <Btn variant="ghost" label="Scarica modello" accessibilityLabel="Scarica il modello CSV" onPress={() => downloadCsv("modello-calendario.csv", calendarTemplateCsv())} style={{ paddingVertical: 8 }} />
            <Btn variant="ghost" label="Scarica elenco docenti" accessibilityLabel="Scarica l'elenco dei docenti con ID" onPress={() => downloadCsv("docenti.csv", teachersCsv(teachers))} style={{ paddingVertical: 8 }} disabled={teachers.length === 0} />
          </View>
        )}
        <Note color={C.text}>Il docente si indica con il suo <Text style={{ fontWeight: "700" }}>ID</Text> (o con un riferimento esterno già registrato), non con il nome.</Note>
        <Btn variant="ghost" label={showTeacherIds ? "Nascondi ID dei docenti" : "Mostra ID dei docenti"} onPress={() => setShowTeacherIds(v => !v)} style={{ alignSelf: "flex-start", paddingVertical: 8, marginBottom: 10 }} />
        {showTeacherIds && (
          <View style={{ marginBottom: 12 }}>
            {teachers.length === 0 ? <Note>Nessun docente.</Note> : teachers.map(t => (
              <View key={t.id} style={[s.profileRow, { flexWrap: "wrap", gap: 8 }]}>
                <Text style={s.profileKey}>{`${t.lastName || ""} ${t.firstName || ""}`.trim()}</Text>
                <Text selectable style={{ fontSize: 13, color: C.text, fontFamily: "monospace" }}>{t.id}</Text>
              </View>
            ))}
          </View>
        )}
        <SelectField label="Se il mese ha già lezioni in bozza" value={mode} options={IMPORT_MODES} onSelect={setMode} />
        {mode === "ReplaceDraftScope" && <Note color={C.danger}>Le lezioni in bozza (non pubblicate) del mese per le classi selezionate verranno annullate e sostituite da quelle del file.</Note>}
        <View style={{ flexDirection: "row", alignItems: "center", gap: 12, flexWrap: "wrap", marginBottom: 12 }}>
          <Btn variant="ghost" label={file ? "Cambia file" : "Scegli file"} onPress={pickFile} />
          <Text style={s.itemSub} numberOfLines={1}>{file ? file.name : "Nessun file selezionato"}</Text>
        </View>
        <Btn label="Genera anteprima" onPress={runPreview} loading={previewing} disabled={settings === null || !file || classIds.length === 0} />
      </Section>

      {preview && (
        <Section title="3. Anteprima" subtitle={`Valida fino alle ${new Date(preview.expiresAtUtc).toLocaleTimeString("it-IT", { hour: "2-digit", minute: "2-digit" })}. Nessuna lezione è ancora stata creata.`}>
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 12, marginBottom: 8 }}>
            <Stat label="Righe nel file" value={preview.totalRows ?? rows.length} />
            <Stat label="Pronte" value={preview.validRows ?? rows.length - errorRows.length} color={C.success} />
            <Stat label="Con errori" value={preview.invalidRows ?? errorRows.length} color={errorRows.length ? C.danger : C.text} />
            <Stat label="Bozze sostituite" value={preview.replaced} />
          </View>

          {commonWarnings.map((w, i) => <Note key={i} color={C.warning}>⚠ {issueMessage(w, preview.columns)}</Note>)}

          {errorRows.length > 0 && (
            <View style={{ marginTop: 8 }}>
              <Text style={[s.itemTitle, { color: C.danger, marginBottom: 6 }]}>Righe da correggere nel file</Text>
              {errorRows.slice(0, MAX_ROWS_SHOWN).map(r => (
                <View key={r.rowNumber} style={[s.profileRow, { flexDirection: "column", alignItems: "flex-start" }]}>
                  <Text style={s.itemTitle}>Riga {r.rowNumber}</Text>
                  <Text style={s.itemSub}>{[r.lesson ? `${formatDay(r.lesson.date)} ${String(r.lesson.startsAt).slice(0, 5)}–${String(r.lesson.endsAt).slice(0, 5)}` : null, r.className || r.classReference, r.moduleName || r.moduleReference, r.teacherName || r.teacherReference].filter(Boolean).join(" · ")}</Text>
                  {(r.issues || []).filter(i => !isCommon(i)).map((i, k) => {
                    // Messaggio per codice; il codice compare (piccolo) solo se non è tra quelli tradotti
                    const { text, code } = issueParts(i, preview.columns);
                    return (
                      <Text key={k} style={{ color: i.warning ? C.warning : C.danger, fontSize: 13 }}>
                        {i.warning ? "⚠" : "✕"} {text}{code ? <Text style={{ fontSize: 11, color: C.textMuted }}> ({code})</Text> : null}
                      </Text>
                    );
                  })}
                  {(r.conflicts || []).map((c, k) => <Text key={`c${k}`} style={{ color: C.danger, fontSize: 13 }}>✕ {conflictMessage(c)}</Text>)}
                </View>
              ))}
              {errorRows.length > MAX_ROWS_SHOWN && <Note>{errorRows.length - MAX_ROWS_SHOWN === 1 ? "… e un'altra riga." : `… e altre ${errorRows.length - MAX_ROWS_SHOWN} righe.`}</Note>}
            </View>
          )}

          {warningRows.some(r => r.issues.some(i => i.warning && !isCommon(i))) && (
            <View style={{ marginTop: 8 }}>
              <Text style={[s.itemTitle, { color: C.warning, marginBottom: 6 }]}>Avvisi</Text>
              {warningRows.filter(r => r.issues.some(i => i.warning && !isCommon(i))).slice(0, MAX_ROWS_SHOWN).map(r => (
                <Text key={r.rowNumber} style={{ color: C.textMuted, fontSize: 13 }}>Riga {r.rowNumber}: {r.issues.filter(i => i.warning && !isCommon(i)).map(i => issueMessage(i, preview.columns)).join("; ")}</Text>
              ))}
            </View>
          )}

          {hourWarnings.length > 0 && (
            <View style={{ marginTop: 12 }}>
              <Text style={[s.itemTitle, { marginBottom: 2 }]}>Monte ore dopo l'import</Text>
              <Note>Ore pubblicate + bozze rispetto alle ore previste dal piano formativo (su tutto il percorso).</Note>
              {/* l'etichetta dell'avviso accompagna sempre il colore */}
              {hourWarnings.map(h => {
                const projected = h.publishedMinutes + h.draftMinutesAfterImport;
                const diff = projected - h.plannedMinutes;
                return (
                  // flexWrap: su telefono le ore vanno sotto il nome del modulo invece di troncarlo
                  <View key={`${h.classId}-${h.moduleId}`} style={[s.profileRow, { flexWrap: "wrap", columnGap: 12, rowGap: 2 }]}>
                    <Text style={[s.profileKey, { flex: 1, minWidth: 200 }]}>{className(h.classId)} · {moduleNames[h.moduleId] || "Modulo"}</Text>
                    <Text style={{ fontSize: 13, color: diff > 0 ? C.danger : C.textMuted }}>{HOUR_WARNING_LABELS[h.warning] || HOUR_WARNING_LABELS[diff > 0 ? "calendar.over-scheduled" : "calendar.under-scheduled"]}: {formatMinutes(projected)} su {formatMinutes(h.plannedMinutes)} ({diff > 0 ? "+" : ""}{formatMinutes(diff)})</Text>
                  </View>
                );
              })}
            </View>
          )}

          <View style={{ marginTop: 16 }}>
            {!preview.canConfirm ? (
              <Note color={C.danger}>Correggi le righe con errori nel file e genera di nuovo l'anteprima.</Note>
            ) : needsAck ? (
              <>
                <CheckboxField label="Ho letto gli avvisi e voglio procedere" value={ack} onChange={setAck} />
                {underScheduled && <Note>In un import di un solo mese è normale che i moduli risultino sotto il monte ore: l'avviso riguarda le ore di tutto il percorso.</Note>}
              </>
            ) : null}
            <Btn variant="success" label={confirmLabel(readyRows)} onPress={runConfirm} loading={confirming} disabled={!canConfirm || readyRows === 0} />
          </View>
        </Section>
      )}

      <Section title={`${preview ? "4" : "3"}. Pubblica ${monthName.toLowerCase()}`} subtitle="Docenti e studenti vedono solo le lezioni pubblicate. Si pubblica una classe alla volta.">
        {classIds.length === 0 ? <Note>Seleziona le classi qui sopra.</Note> : classIds.map(id => {
          const st = status[id] || {};
          const approval = st.period?.approval;
          return (
            <View key={id} style={[s.profileRow, { alignItems: "center", gap: 12, flexWrap: "wrap" }]}>
              <View style={{ flex: 1, minWidth: 180 }}>
                <Text style={s.itemTitle}>{className(id)}</Text>
                {st.loading ? <Text style={s.itemSub}>Caricamento…</Text>
                  : st.error ? <Text style={[s.itemSub, { color: C.danger }]}>{st.error}</Text>
                  : <Text style={s.itemSub}>{st.drafts} in bozza · {st.published} pubblicate{approval ? ` · da pubblicare entro il ${formatDay(approval.deadlineLocalDate)}${approval.isOverdue && st.drafts ? " (scaduto)" : ""}` : ""}</Text>}
              </View>
              <Btn label="Pubblica" style={[s.smBtn, { paddingVertical: 8 }]} textStyle={s.smBtnText}
                onPress={() => askPublish(id)} loading={publishing === id}
                disabled={!!st.loading || !!st.error || !st.period || !st.drafts || publishing !== null} />
            </View>
          );
        })}
      </Section>
    </ScrollView>
  );
}
