// src/components/CalendarSettingsForm.js
// Impostazioni del calendario dell'istituto (admin e staff): fuso orario, giorni di anticipo per
// l'approvazione del mese, motivo. Prima configurazione con expectedRevision -1, poi con la revisione letta.
// "Europe/Rome" è solo proposto: va confermato, il server non ha un fuso predefinito.
import { useState } from "react";
import { Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import { useIntentId } from "../hooks/useIntentId";
import Btn from "./Btn";
import Input from "./Input";
import CheckboxField from "./CheckboxField";
import { REASON_MAX, saveCalendarSettings, validReason } from "../api/calendarService";
import { calendarErrorMessage } from "../api/calendarImportService";

export const PROPOSED_TIME_ZONE = "Europe/Rome";

// Il codice calendar.revision-conflict vale anche per il mese: qui il testo parla delle impostazioni
export const SETTINGS_CONFLICT_MESSAGE = "Le impostazioni sono state modificate nel frattempo: ricarica e riprova.";
export const settingsErrorMessage = (e) => (e?.code === "calendar.revision-conflict" ? SETTINGS_CONFLICT_MESSAGE : calendarErrorMessage(e));

// settings: null = da configurare, altrimenti { timeZoneId, approvalLeadDays, revision }. onSaved(): rileggere.
export default function CalendarSettingsForm({ settings, onSaved }) {
  const { token } = useAuth();
  const first = !settings;
  const { idFor, reset } = useIntentId();
  const [timeZoneId, setTimeZoneId] = useState(settings?.timeZoneId || PROPOSED_TIME_ZONE);
  const [lead, setLead] = useState(settings ? String(settings.approvalLeadDays) : "");
  const [reason, setReason] = useState("");
  const [zoneConfirmed, setZoneConfirmed] = useState(!first);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);

  const leadDays = /^\d{1,3}$/.test(lead.trim()) ? Number(lead.trim()) : NaN;
  const payload = { expectedRevision: first ? -1 : settings.revision, timeZoneId: timeZoneId.trim(), approvalLeadDays: leadDays, reason: reason.trim() };
  const formError = !payload.timeZoneId ? "Indica il fuso orario."
    : !zoneConfirmed ? "Conferma il fuso orario."
    : !(leadDays >= 0 && leadDays <= 366) ? "I giorni di anticipo devono essere un numero tra 0 e 366."
    : !validReason(reason) ? "Indica il motivo (al massimo 500 caratteri)." : null;

  async function save() {
    if (formError) { setError(formError); return; }
    setSaving(true); setError(null); setSaved(false);
    try {
      await saveCalendarSettings({ eventId: idFor(payload), ...payload }, token);
      reset(); setReason(""); setSaved(true);
      onSaved?.();
    } catch (e) { setError(settingsErrorMessage(e)); }
    finally { setSaving(false); }
  }

  return (
    <View>
      <Text style={s.sectionTitle}>{first ? "Configura il calendario" : "Impostazioni del calendario"}</Text>
      <Text style={[s.itemSub, { marginBottom: 12 }]}>
        {first ? "Il calendario dell'istituto non è ancora configurato: finché non lo è non si possono caricare lezioni." : `Revisione ${settings.revision}.`}
      </Text>
      <Text style={{ color: C.warning, fontSize: 13, marginBottom: 10 }}>
        Il fuso orario non si può più cambiare dopo che sono state create lezioni.
      </Text>
      <Input label="Fuso orario (es. Europe/Rome)" value={timeZoneId} onChangeText={(v) => { setTimeZoneId(v); if (first) setZoneConfirmed(false); }} autoCapitalize="none" />
      {first && <CheckboxField label={`Confermo il fuso orario ${timeZoneId.trim() || "indicato"}`} value={zoneConfirmed} onChange={setZoneConfirmed} />}
      <Input label="Giorni di anticipo per l'approvazione del mese (0–366)" value={lead} onChangeText={setLead} keyboardType="number-pad" placeholder="Es. 7" />
      <Input label="Motivo (obbligatorio, resta nello storico)" value={reason} onChangeText={setReason} maxLength={REASON_MAX} />
      {!!error && <Text accessibilityRole="alert" style={{ color: C.danger, fontSize: 13, marginBottom: 8 }}>{error}</Text>}
      {saved && <Text accessibilityRole="alert" style={{ color: C.success, fontSize: 13, marginBottom: 8 }}>Impostazioni salvate.</Text>}
      <Btn label={first ? "Configura il calendario" : "Salva impostazioni"} onPress={save} loading={saving} />
    </View>
  );
}
