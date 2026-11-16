// src/components/ClassCatalogForms.js
// Moduli dell'amministratore per preparare l'istituto (schermata Classi):
//   CatalogAddModal     nuovo biennio, area di studio, corso di studio o classe, al livello in cui ci si trova
//   ClassSubjectModal   materia della classe con il suo docente, oppure cambio del docente
// Aree e corsi sono dell'istituto e si collegano al biennio/all'area: si può riusarne uno esistente o crearne uno.
import { useEffect, useState } from "react";
import { Text, View } from "react-native";
import { s } from "../constants/theme";
import { useAuth } from "../hooks/useAuth";
import FormModal from "./FormModal";
import Input from "./Input";
import Btn from "./Btn";
import DateField from "./DateField";
import SelectField from "./SelectField";
import {
  addClassSubject, changeClassSubjectTeacher, createBiennium, createClass, createStudyArea, createStudyPath,
  getAllStudyAreas, getAllStudyPaths, linkStudyArea, linkStudyPath,
} from "../api/classService";
import { getSubjects } from "../api/subjectService";
import { getTeachers } from "../api/teacherService";

const NEW = "__new__";
const TITLES = { bienni: "Nuovo biennio", areas: "Aggiungi area di studio", paths: "Aggiungi corso di studio", classes: "Nuova classe" };

// Footer comune: errore, Annulla e Salva
function Actions({ failure, saving, onCancel, onSave, label = "Salva" }) {
  return (
    <>
      {!!failure && <Text style={[s.errorText, { marginBottom: 10 }]}>{failure}</Text>}
      <View style={{ flexDirection: "row", justifyContent: "flex-end", gap: 10 }}>
        <Btn label="Annulla" variant="ghost" onPress={onCancel} style={{ paddingHorizontal: 22 }} />
        <Btn label={label} onPress={onSave} loading={saving} style={{ paddingHorizontal: 22 }} />
      </View>
    </>
  );
}

// level: "bienni" | "areas" | "paths" | "classes"; sel: { biennium, area, path }; linked: elementi già presenti
// a questo livello (per non proporre di nuovo un'area o un corso già collegati); onDone(): ricarica l'elenco
export function CatalogAddModal({ visible, level, sel, linked = [], onClose, onDone }) {
  const { token } = useAuth();
  const [form, setForm] = useState({});
  const [catalog, setCatalog] = useState([]);
  const [failure, setFailure] = useState(null);
  const [saving, setSaving] = useState(false);
  const set = (patch) => { setForm(f => ({ ...f, ...patch })); setFailure(null); };

  useEffect(() => {
    if (!visible) return;
    setForm({ choice: null }); setFailure(null); setCatalog([]);
    const load = level === "areas" ? getAllStudyAreas : level === "paths" ? getAllStudyPaths : null;
    if (load) load(token).then(setCatalog).catch(() => setCatalog([]));
  }, [visible, level]);

  const usedIds = new Set(linked.map(l => l.studyAreaId || l.studyPathId));
  const reusable = catalog.filter(x => !usedIds.has(x.id));
  const isNew = form.choice === NEW || ((level === "areas" || level === "paths") && reusable.length === 0);
  const what = level === "areas" ? "area" : "corso";

  function validate() {
    if (level === "bienni") {
      if (!form.startDate || !form.endDate) return "Indica l'inizio e la fine del biennio.";
      if (form.endDate < form.startDate) return "La fine deve venire dopo l'inizio.";
    } else if (level === "classes") {
      const name = (form.name || "").trim();
      if (!name || name.length > 20) return "Il nome della classe deve avere da 1 a 20 caratteri.";
    } else if (isNew) {
      const name = (form.name || "").trim();
      if (!name || name.length > 100) return "Il nome deve avere da 1 a 100 caratteri.";
    } else if (!form.choice) return `Scegli un ${what} o creane uno nuovo.`;
    return null;
  }

  async function save() {
    const problem = validate();
    if (problem) return setFailure(problem);
    setSaving(true);
    try {
      if (level === "bienni") await createBiennium({ startDate: form.startDate, endDate: form.endDate }, token);
      else if (level === "classes") await createClass({ name: form.name, bienniumStudyPathId: sel.path.id }, token);
      else if (level === "areas") {
        const areaId = isNew ? await createStudyArea({ name: form.name, description: form.description }, token) : form.choice;
        await linkStudyArea(sel.biennium.id, areaId, token);
      } else {
        const pathId = isNew ? await createStudyPath({ name: form.name, description: form.description }, token) : form.choice;
        await linkStudyPath(sel.area.id, pathId, token);
      }
      onDone();
      onClose();
    } catch (e) {
      setFailure(e.status === 409 ? "Esiste già: controlla nome o date (devono essere unici nell'istituto)." : e.message);
    } finally { setSaving(false); }
  }

  return (
    <FormModal visible={visible} title={TITLES[level] || ""} onClose={onClose}>
      {level === "bienni" && <>
        <DateField label="Inizio" value={form.startDate} onChange={(startDate) => set({ startDate })} placeholder="Data di inizio" />
        <DateField label="Fine" value={form.endDate} onChange={(endDate) => set({ endDate })} placeholder="Data di fine" />
      </>}
      {level === "classes" && (
        <Input label="Nome della classe" value={form.name || ""} onChangeText={(name) => set({ name })} maxLength={20} placeholder="Es. SW-25A" />
      )}
      {(level === "areas" || level === "paths") && <>
        {reusable.length > 0 && (
          <SelectField label={level === "areas" ? "Area di studio" : "Corso di studio"} value={form.choice}
            options={[...reusable, { id: NEW, name: `+ Nuova ${level === "areas" ? "area" : "corso"}` }]}
            getLabel={(x) => x.name} onSelect={(choice) => set({ choice })} placeholder={`Scegli ${level === "areas" ? "un'area" : "un corso"}…`} />
        )}
        {isNew && <>
          <Input label="Nome" value={form.name || ""} onChangeText={(name) => set({ name })} maxLength={100}
            placeholder={level === "areas" ? "Es. Tecnologie dell'informazione" : "Es. Software developer"} />
          <Input label="Descrizione (facoltativa)" value={form.description || ""} onChangeText={(description) => set({ description })} />
        </>}
      </>}
      <Actions failure={failure} saving={saving} onCancel={onClose} onSave={save} />
    </FormModal>
  );
}

// current: assegnazione esistente ({ classSubjectId, subjectName, teacherId, revision }) → solo cambio docente.
// taken: id delle materie già presenti nella classe.
export function ClassSubjectModal({ visible, classId, current, taken = [], onClose, onDone }) {
  const { token } = useAuth();
  const [subjects, setSubjects] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [subjectId, setSubjectId] = useState(null);
  const [teacherId, setTeacherId] = useState(null);
  const [failure, setFailure] = useState(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!visible) return;
    setFailure(null); setSubjectId(null); setTeacherId(current?.teacherId || null);
    getTeachers(token).then(d => setTeachers((d || []).map(t => ({ id: t.id, name: `${t.lastName} ${t.firstName}` })).sort((a, b) => a.name.localeCompare(b.name)))).catch(() => setTeachers([]));
    if (!current) getSubjects(undefined, token).then(d => setSubjects((d || []).filter(x => !taken.includes(x.id)))).catch(() => setSubjects([]));
  }, [visible]);

  async function save() {
    if (!current && !subjectId) return setFailure("Scegli la materia.");
    if (!teacherId) return setFailure("Scegli il docente.");
    setSaving(true);
    try {
      if (current) await changeClassSubjectTeacher(current.classSubjectId, teacherId, current.revision ?? 0, token);
      else await addClassSubject({ classId, subjectId, teacherId }, token);
      onDone();
      onClose();
    } catch (e) {
      setFailure(e.status === 409
        ? (current ? "Il docente non si può cambiare: ci sono esami o voti collegati, oppure l'assegnazione è stata modificata nel frattempo." : "Questa materia è già assegnata alla classe.")
        : e.message);
    } finally { setSaving(false); }
  }

  return (
    <FormModal visible={visible} title={current ? `Docente di ${current.subjectName}` : "Aggiungi materia"} onClose={onClose}>
      {!current && (
        <SelectField label="Materia" value={subjectId} options={subjects} getLabel={x => x.name} onSelect={setSubjectId}
          placeholder="Scegli la materia…" emptyMessage="Nessuna materia disponibile: creale prima nella sezione Materie" />
      )}
      <SelectField label="Docente" value={teacherId} options={teachers} getLabel={x => x.name} onSelect={setTeacherId}
        placeholder="Scegli il docente…" emptyMessage="Nessun docente: crealo prima nella sezione Professori" />
      <Actions failure={failure} saving={saving} onCancel={onClose} onSave={save} />
    </FormModal>
  );
}
