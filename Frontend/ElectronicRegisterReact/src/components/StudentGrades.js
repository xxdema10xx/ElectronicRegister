// src/components/StudentGrades.js
// Voti di un singolo studente (vista admin): elenco, aggiunta, modifica ed eliminazione.
import { useCallback, useEffect, useState } from "react";
import { Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import FormModal from "./FormModal";
import GradeCircle from "./GradeCircle";
import Input from "./Input";
import SelectField from "./SelectField";
import DateField from "./DateField";
import Btn from "./Btn";
import { getGradesPage, createGrade, updateGrade, deleteGrade } from "../api/gradeService";
import { getSubjects } from "../api/subjectService";
import { formatDateForDisplay, toISODate } from "../utils/dateUtils";
import { notify, confirmAction } from "../utils/dialogs";

const emptyForm = () => ({ id: null, subjectId: "", value: "", date: toISODate(new Date()) });

export default function StudentGrades({ student, token, onClose }) {
  const [grades, setGrades] = useState([]);
  const [subjects, setSubjects] = useState([]);
  const [loading, setLoading] = useState(true);
  const [form, setForm] = useState(null); // null = elenco; oggetto = form di aggiunta/modifica
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    if (!student) return;
    setLoading(true);
    try {
      const [page, subs] = await Promise.all([
        getGradesPage({ pageNumber: 1, pageSize: 100, studentId: student.id }, token),
        getSubjects(undefined, token),
      ]);
      const items = Array.isArray(page?.items) ? page.items : [];
      setGrades(items.slice().sort((a, b) => String(b.date).localeCompare(String(a.date))));
      setSubjects(Array.isArray(subs) ? subs : []);
    } catch (e) {
      if (!String(e.message).includes("404")) notify("Errore", e.message);
      setGrades([]);
    } finally { setLoading(false); }
  }, [student, token]);

  useEffect(() => { setForm(null); load(); }, [load]);

  const subjectName = id => subjects.find(x => x.id === id)?.name || "—";

  async function save() {
    const value = parseFloat(String(form.value).replace(",", "."));
    if (!form.subjectId) return notify("Attenzione", "Seleziona la materia.");
    if (!(value >= 1 && value <= 10)) return notify("Attenzione", "Il voto deve essere tra 1 e 10.");
    if (!form.date) return notify("Attenzione", "Seleziona la data.");
    setSaving(true);
    try {
      if (form.id) await updateGrade(form.id, { subjectId: form.subjectId, value, date: form.date }, token);
      else await createGrade({ studentId: student.id, subjectId: form.subjectId, value, date: form.date }, token);
      setForm(null);
      load();
    } catch (e) { notify("Errore", e.message); }
    finally { setSaving(false); }
  }

  function remove(g) {
    confirmAction("Conferma", "Eliminare questo voto?", async () => {
      try { await deleteGrade(g.id, token); load(); }
      catch (e) { notify("Errore", e.message); }
    });
  }

  const title = student ? `Voti di ${student.firstName} ${student.lastName}` : "";

  return (
    <FormModal wide visible={!!student} title={form ? (form.id ? "Modifica voto" : "Nuovo voto") : title} onClose={onClose}>
      {form ? (
        <View>
          <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 16 }}>
            <View style={{ flex: 1, minWidth: 200 }}>
              <SelectField label="Materia" value={form.subjectId} options={subjects} getLabel={(sub) => sub.name}
                onSelect={(subjectId) => setForm(f => ({ ...f, subjectId }))} placeholder="Seleziona materia…" emptyMessage="Nessuna materia disponibile" />
            </View>
            <View style={{ flex: 1, minWidth: 200 }}>
              <DateField label="Data" value={form.date} onChange={(date) => setForm(f => ({ ...f, date }))} />
            </View>
          </View>
          <Input label="Voto (1-10)" value={String(form.value)} onChangeText={(value) => setForm(f => ({ ...f, value }))} keyboardType="decimal-pad" />
          <View style={{ flexDirection: "row", justifyContent: "flex-end", gap: 10 }}>
            <Btn label="Annulla" variant="ghost" onPress={() => setForm(null)} style={{ paddingHorizontal: 22 }} />
            <Btn label={form.id ? "Aggiorna" : "Salva"} onPress={save} loading={saving} style={{ paddingHorizontal: 28 }} />
          </View>
        </View>
      ) : (
        <View>
          <View style={{ flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
            <Text style={s.itemSub}>{loading ? "Caricamento…" : `${grades.length} ${grades.length === 1 ? "voto" : "voti"}`}</Text>
            <Btn label="Aggiungi voto" onPress={() => setForm(emptyForm())} style={s.smBtn} textStyle={s.smBtnText} />
          </View>
          {!loading && grades.length === 0 && <Text style={[s.emptyText, { paddingVertical: 24, textAlign: "center" }]}>Nessun voto per questo studente</Text>}
          {grades.map(g => (
            <View key={g.id} style={[s.profileRow, { alignItems: "center", paddingVertical: 10 }]}>
              <GradeCircle value={g.value} />
              <View style={{ flex: 1, marginLeft: 12 }}>
                <Text style={s.itemTitle}>{subjectName(g.subjectId)}</Text>
                <Text style={s.itemSub}>{formatDateForDisplay(g.date)}</Text>
              </View>
              <TouchableOpacity onPress={() => setForm({ id: g.id, subjectId: g.subjectId, value: String(g.value), date: g.date })} style={{ padding: 6 }}>
                <Ionicons name="create-outline" size={26} color={C.footer} />
              </TouchableOpacity>
              <TouchableOpacity onPress={() => remove(g)} style={{ padding: 6 }}>
                <Ionicons name="trash-outline" size={26} color={C.footer} />
              </TouchableOpacity>
            </View>
          ))}
        </View>
      )}
    </FormModal>
  );
}
