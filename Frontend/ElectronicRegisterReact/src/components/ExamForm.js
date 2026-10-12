// src/components/ExamForm.js
// Form di creazione/modifica esame, impaginato per il web: nome a tutta larghezza, materia e data
// affiancate, azioni allineate a destra. Su schermi stretti i campi si impilano.
import { View } from "react-native";
import { useLayout } from "../hooks/useLayout";
import Input from "./Input";
import SelectField from "./SelectField";
import DateField from "./DateField";
import CheckboxField from "./CheckboxField";
import Btn from "./Btn";

export default function ExamForm({ value, onChange, subjects, onSubmit, onCancel, submitLabel }) {
  const { isDesktop } = useLayout();
  const set = (patch) => onChange({ ...value, ...patch });

  return (
    <View>
      <Input label="Nome esame" value={value.name} onChangeText={(name) => set({ name })} placeholder="Es. Programmazione web" autoFocus />
      <View style={{ flexDirection: isDesktop ? "row" : "column", gap: isDesktop ? 16 : 0 }}>
        <View style={{ flex: 1 }}>
          <SelectField
            label="Materia"
            value={value.subjectId}
            options={subjects}
            getLabel={(t) => `${t.name}`}
            onSelect={(subjectId) => set({ subjectId })}
            placeholder="Seleziona materia"
            emptyMessage="Nessuna materia disponibile"
          />
        </View>
        <View style={{ flex: 1 }}>
          <DateField label="Data" value={value.date} onChange={(date) => set({ date })} />
        </View>
      </View>
      <CheckboxField label="Esame di recupero" value={!!value.isRetake} onChange={(isRetake) => set({ isRetake })} />
      <View style={{ flexDirection: "row", justifyContent: "flex-end", gap: 10, marginTop: 6 }}>
        <Btn label="Annulla" variant="ghost" onPress={onCancel} style={{ paddingHorizontal: 22 }} />
        <Btn label={submitLabel} onPress={onSubmit} style={{ paddingHorizontal: 28 }} />
      </View>
    </View>
  );
}
