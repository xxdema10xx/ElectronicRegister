// src/components/AccountFormModal.js
// Creazione di un account di accesso (POST /api/institution/accounts). L'email la genera il server; la password
// temporanea compare una sola volta, qui, e va comunicata alla persona (al primo accesso dovrà cambiarla).
//   allowedRoles  ruoli proponibili (admin: tutti; segreteria: Student, Teacher)
//   preset        { firstName, lastName, studentId?, teacherId?, roles? }: account per una scheda esistente
//                 (nome e cognome devono coincidere con la scheda, il ruolo è fisso)
//   classOptions  classi per una nuova scheda studente (solo admin: la segreteria non può elencarle)
import { useEffect, useRef, useState } from "react";
import { Platform, Text, View } from "react-native";
import { C, s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import FormModal from "./FormModal";
import Input from "./Input";
import Btn from "./Btn";
import CheckboxField from "./CheckboxField";
import SelectField from "./SelectField";
import { ACCOUNT_ROLES, accountErrorMessage, accountFormErrors, createAccount, newEventId, normalizeFiscalCode } from "../api/accountService";

const empty = (preset) => ({
  firstName: preset?.firstName || "", lastName: preset?.lastName || "", fiscalCode: "",
  roles: preset?.roles || [], classId: null, reason: "",
});

export default function AccountFormModal({ visible, onClose, onCreated, allowedRoles = ["Student", "Teacher"], preset, classOptions }) {
  const { token } = useAuth();
  const [form, setForm] = useState(() => empty(preset));
  const [errors, setErrors] = useState({});
  const [failure, setFailure] = useState(null);
  const [saving, setSaving] = useState(false);
  const [created, setCreated] = useState(null); // risposta del server: email e password temporanea
  // Un eventId per intenzione: resta uguale se si ripete la stessa richiesta (risposta persa, server occupato),
  // cambia appena si modifica il modulo (sarebbe un'altra richiesta: con lo stesso id il server risponderebbe 409)
  const eventId = useRef(null);

  useEffect(() => {
    if (!visible) return;
    setForm(empty(preset)); setErrors({}); setFailure(null); setCreated(null);
    eventId.current = newEventId();
  }, [visible]);

  const linked = !!(preset?.studentId || preset?.teacherId);
  const set = (patch) => { setForm(f => ({ ...f, ...patch })); setFailure(null); eventId.current = newEventId(); };
  const toggleRole = (id) => set({ roles: form.roles.includes(id) ? form.roles.filter(r => r !== id) : [...form.roles, id] });
  const roles = ACCOUNT_ROLES.filter(r => allowedRoles.includes(r.id));
  const showClass = !linked && form.roles.includes("Student") && Array.isArray(classOptions);

  async function submit() {
    const e = accountFormErrors(form);
    setErrors(e);
    if (Object.keys(e).length > 0) return;
    setSaving(true);
    setFailure(null);
    try {
      const result = await createAccount({
        ...form, eventId: eventId.current, classId: showClass ? form.classId : null,
        studentId: preset?.studentId, teacherId: preset?.teacherId,
      }, token);
      setCreated(result);
      onCreated?.(result);
    } catch (err) { setFailure(accountErrorMessage(err)); }
    finally { setSaving(false); }
  }

  if (created) {
    return (
      <FormModal visible={visible} title="Account creato" onClose={onClose}>
        <Text style={s.label}>Email per accedere</Text>
        <Text selectable style={{ fontSize: 16, color: C.text, fontWeight: "700", marginBottom: 14 }}>{created.email}</Text>
        {created.temporaryPassword ? (
          <>
            <Text style={s.label}>Password temporanea</Text>
            <Text selectable style={{ fontSize: 18, color: C.text, fontWeight: "800", marginBottom: 8, fontFamily: Platform.OS === "web" ? "monospace" : undefined }}>
              {created.temporaryPassword}
            </Text>
            <View style={{ backgroundColor: "#FEF3C7", borderRadius: 10, padding: 12, marginBottom: 16 }}>
              <Text style={{ color: "#92400E" }}>
                Annotala ora e consegnala alla persona: non sarà più visibile. Al primo accesso dovrà scegliere una password sua.
              </Text>
            </View>
          </>
        ) : (
          <View style={{ backgroundColor: "#E0EEF8", borderRadius: 10, padding: 12, marginBottom: 16 }}>
            <Text style={{ color: C.primary }}>
              Questa persona ha già un account attivo sulla piattaforma: ora è anche membro dell'istituto e accede con la password che usa già.
            </Text>
          </View>
        )}
        <Btn label="Fatto" onPress={onClose} />
      </FormModal>
    );
  }

  return (
    <FormModal visible={visible} title="Nuovo account" onClose={onClose} wide>
      {linked && (
        <Text style={[s.itemSub, { marginBottom: 12 }]}>
          Account collegato alla scheda di {preset.firstName} {preset.lastName}: nome e cognome devono restare quelli della scheda.
        </Text>
      )}
      <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 12 }}>
        <View style={{ flex: 1, minWidth: 200 }}>
          <Input label="Nome" value={form.firstName} onChangeText={v => set({ firstName: v })} editable={!linked} error={errors.firstName} />
        </View>
        <View style={{ flex: 1, minWidth: 200 }}>
          <Input label="Cognome" value={form.lastName} onChangeText={v => set({ lastName: v })} editable={!linked} error={errors.lastName} />
        </View>
      </View>
      <Input label="Codice fiscale" value={form.fiscalCode} onChangeText={v => set({ fiscalCode: normalizeFiscalCode(v) })}
        autoCapitalize="characters" maxLength={16} placeholder="RSSMRA80A01H501U" error={errors.fiscalCode} />
      {!preset?.roles && (
        <View style={{ marginBottom: 6 }}>
          <Text style={s.label}>Ruoli</Text>
          {roles.map(r => <CheckboxField key={r.id} label={r.label} value={form.roles.includes(r.id)} onChange={() => toggleRole(r.id)} />)}
          {!!errors.roles && <Text style={[s.errorText, { marginTop: -8, marginBottom: 10 }]}>{errors.roles}</Text>}
        </View>
      )}
      {showClass && (
        <SelectField label="Classe (facoltativa)" value={form.classId} options={classOptions} getLabel={c => c.name}
          onSelect={(classId) => set({ classId })} placeholder="Nessuna classe" emptyMessage="Nessuna classe disponibile" />
      )}
      <Input label="Motivazione (resta nello storico)" value={form.reason} onChangeText={v => set({ reason: v })}
        placeholder="Es. Iscrizione al corso" maxLength={500} error={errors.reason} />
      <Text style={[s.itemSub, { marginBottom: 12 }]}>L'indirizzo email lo genera il sistema (nome.cognome).</Text>
      {!!failure && <Text style={[s.errorText, { marginBottom: 10 }]}>{failure}</Text>}
      <View style={{ flexDirection: "row", justifyContent: "flex-end", gap: 10 }}>
        <Btn label="Annulla" variant="ghost" onPress={onClose} style={{ paddingHorizontal: 22 }} />
        <Btn label="Crea account" onPress={submit} loading={saving} style={{ paddingHorizontal: 22 }} />
      </View>
    </FormModal>
  );
}
