// src/components/ExamTests.js
// Test di un esame: elenco dentro la card dell'esame e creazione con l'editor di quiz.
import { useCallback, useEffect, useState } from "react";
import { Platform, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Btn from "./Btn";
import TestViewer from "./TestViewer";
import TestBuilder, { newTestDraft, summarize, validateTestDraft } from "./TestBuilder";
import { getTests, createTest, deleteTest } from "../api/testService";
import { notify, confirmAction } from "../utils/dialogs";
import { DEMO_TESTS } from "../data/examsMock";

// La bozza sopravvive al refresh (F5) su web grazie a sessionStorage.
const draftKey = (examId) => `testDraft2:${examId}`;
const draftStore = {
  get(examId) {
    try { return Platform.OS === "web" ? JSON.parse(window.sessionStorage.getItem(draftKey(examId))) : null; }
    catch { return null; }
  },
  set(examId, d) {
    try { if (Platform.OS === "web") window.sessionStorage.setItem(draftKey(examId), JSON.stringify(d)); } catch {}
  },
  clear(examId) {
    try { if (Platform.OS === "web") window.sessionStorage.removeItem(draftKey(examId)); } catch {}
  },
};

export default function ExamTests({ exam, token, canWrite }) {
  const [expanded, setExpanded] = useState(() => !!draftStore.get(exam.id));
  // Esami di prova: i test sono fissi e il salvataggio/eliminazione avvengono solo in locale
  const [tests, setTests] = useState(() => exam.demo ? (DEMO_TESTS[exam.id] || []) : []);
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [viewing, setViewing] = useState(null); // test aperto in sola lettura
  const [draft, setDraft] = useState(() => draftStore.get(exam.id));
  const [editing, setEditing] = useState(() => !!draftStore.get(exam.id)); // editor aperto

  const load = useCallback(async () => {
    if (exam.demo) return;
    setLoading(true);
    try {
      const data = await getTests("", token);
      const all = Array.isArray(data) ? data : data ? [data] : [];
      setTests(all.filter(t => t.examId === exam.id));
    } catch { setTests([]); } // se l'elenco non è disponibile si mostra "nessun test"
    finally { setLoading(false); }
  }, [token, exam.id, exam.demo]);

  useEffect(() => { if (expanded) load(); }, [expanded, load]);

  function updateDraft(d) { setDraft(d); d ? draftStore.set(exam.id, d) : draftStore.clear(exam.id); }

  function newTest() {
    if (!draft) updateDraft(newTestDraft());
    setExpanded(true);
    setEditing(true);
  }

  async function save() {
    const err = validateTestDraft(draft);
    if (err) return notify("Attenzione", err);
    if (exam.demo) {
      setTests(prev => [...prev, {
        id: `demo-local-${Date.now()}`, examId: exam.id, name: draft.title.trim(), description: draft.description.trim(),
        numberOfStudents: Number(draft.numberOfStudents), ...summarize(draft),
        questions: draft.questions.map(q => ({ ...q, points: Number(q.points) || 0 })), status: "Confirmed",
      }]);
      updateDraft(null); setEditing(false);
      return;
    }
    setSaving(true);
    try {
      await createTest({
        name: draft.title.trim(), description: draft.description.trim(),
        examId: exam.id, subjectId: exam.subjectId,
        numberOfStudents: Number(draft.numberOfStudents),
        ...summarize(draft),
        questions: draft.questions.map(q => ({ ...q, points: Number(q.points) || 0 })),
        status: "Confirmed",
      }, token);
      updateDraft(null); setEditing(false); load();
    } catch (e) { notify("Errore", e.message); }
    finally { setSaving(false); }
  }

  function discard() {
    confirmAction("Conferma", "Eliminare la bozza del test?", () => { updateDraft(null); setEditing(false); }, "Elimina");
  }

  function removeTest(id) {
    confirmAction("Conferma", "Eliminare il test?", async () => {
      if (exam.demo) return setTests(prev => prev.filter(t => t.id !== id));
      try { await deleteTest(id, token); load(); }
      catch { notify("Errore", "Il test non puo' essere eliminato."); }
    });
  }

  return (
    <View style={{ marginTop: 10, borderTopWidth: 1, borderTopColor: C.border, paddingTop: 8 }}>
      <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between" }}>
        <TouchableOpacity onPress={() => setExpanded(e => !e)} style={{ flexDirection: "row", alignItems: "center", gap: 6 }}>
          <Ionicons name={expanded ? "chevron-down" : "chevron-forward"} size={20} color={C.text} />
          <Text style={{ color: C.text, fontWeight: "600" }}>Test{expanded && !loading ? ` (${tests.length})` : ""}</Text>
        </TouchableOpacity>
        {canWrite && <Btn label="+ Test" onPress={newTest} style={s.smBtn} textStyle={s.smBtnText} />}
      </View>

      {draft && !editing && (
        <TouchableOpacity onPress={() => setEditing(true)} style={{ marginTop: 8 }}>
          <Text style={{ color: C.danger }}>Test in preparazione: “{draft.title || "senza titolo"}” — tocca per riprenderlo</Text>
        </TouchableOpacity>
      )}

      {expanded && (
        loading ? <Text style={{ color: C.textMuted, marginTop: 8 }}>Caricamento…</Text> :
        tests.length === 0 ? <Text style={{ color: C.textMuted, marginTop: 8 }}>Nessun test per questo esame</Text> :
        tests.map(test => (
          <View key={String(test.id)} style={{ flexDirection: "row", alignItems: "center", marginTop: 8 }}>
            <TouchableOpacity onPress={() => setViewing(test)} activeOpacity={0.7} style={{ flex: 1, flexDirection: "row", alignItems: "center" }}>
              <Ionicons name="document-text-outline" size={22} color={C.text} />
              <View style={{ flex: 1, marginLeft: 8 }}>
                <Text style={s.itemTitle}>{test.name}</Text>
                <Text style={s.itemSub}>
                  {[test.numberOfStudents != null && `${test.numberOfStudents} studenti`,
                    test.totalQuestions != null && `${test.totalQuestions} domande`,
                    test.totalPoints != null && `${test.totalPoints} punti`].filter(Boolean).join(" · ")}
                </Text>
              </View>
              <Ionicons name="chevron-forward" size={20} color={C.textLight} />
            </TouchableOpacity>
            {canWrite && (
              <TouchableOpacity onPress={() => removeTest(test.id)}>
                <Ionicons name="trash-outline" size={26} color={C.footer} />
              </TouchableOpacity>
            )}
          </View>
        ))
      )}

      <TestViewer test={viewing} contextLabel={`Esame: ${exam.name}`} onClose={() => setViewing(null)} />

      <TestBuilder visible={editing && !!draft} contextLabel={`Esame: ${exam.name}`} draft={draft}
        onChange={updateDraft} onSave={save} saving={saving} onClose={() => setEditing(false)} onDiscard={discard} />
    </View>
  );
}
