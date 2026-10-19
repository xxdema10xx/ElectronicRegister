// src/components/ExamSubmissions.js
// Consegne di un esame per il docente: elenco con filtri e correzione (punti sulle risposte aperte, voto finale).
import { useCallback, useEffect, useState } from "react";
import { Modal, ScrollView, Text, TextInput, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Btn from "./Btn";
import Card from "./Card";
import Loader from "./Loader";
import EmptyState from "./EmptyState";
import StatusChip, { SUBMISSION_TONE } from "./StatusChip";
import { notify } from "../utils/dialogs";
import { QUESTION_TYPE, SUBMISSION_STATUS, SUBMISSION_STATUS_LABEL, SUBMIT_REASON, fromEnum, formatDateTime } from "../api/examContract";

const num = (v) => Number(String(v).replace(",", "."));
const fmt = (n) => (n === null || n === undefined ? "—" : String(Math.round(n * 100) / 100));

const FILTERS = [
  { key: "all", label: "Tutte" },
  { key: "pending_review", label: "In correzione" },
  { key: "failed_auto", label: "Non superate" },
  { key: "done", label: "Valutate", match: ["graded", "finalized"] },
  { key: "in_progress", label: "In corso" },
];
const REASON_LABEL = { manual: "consegna volontaria", time_expired: "tempo scaduto", violations: "troppe uscite dal test" };

function Header({ title, subtitle, onClose }) {
  return (
    <View style={{ flexDirection: "row", alignItems: "center", gap: 10, backgroundColor: C.white, paddingHorizontal: 16, paddingVertical: 12, borderBottomWidth: 1, borderBottomColor: C.border }}>
      <TouchableOpacity onPress={onClose}><Ionicons name="arrow-back" size={26} color={C.text} /></TouchableOpacity>
      <View style={{ flex: 1 }}>
        <Text style={{ color: C.text, fontSize: 16, fontWeight: "700" }} numberOfLines={1}>{title}</Text>
        {!!subtitle && <Text style={{ color: C.textMuted, fontSize: 12 }} numberOfLines={1}>{subtitle}</Text>}
      </View>
    </View>
  );
}

// ── Correzione di una consegna ──────────────────────────────────────────────────────────────
function SubmissionReview({ submissionId, api, onClose, onChanged }) {
  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [points, setPoints] = useState({});
  const [comments, setComments] = useState({});
  const [grade, setGrade] = useState("");
  const [reason, setReason] = useState("");

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const d = await api.getReview(submissionId);
      setData(d);
      setPoints(Object.fromEntries(d.answers.map(a => [a.answerId, a.awardedPoints === null || a.awardedPoints === undefined ? "" : String(a.awardedPoints)])));
      setComments(Object.fromEntries(d.answers.map(a => [a.answerId, a.teacherComment || ""])));
      const g = d.finalGrade ?? d.suggestedGrade;
      setGrade(g === null || g === undefined ? "" : String(g));
    } catch (e) { notify("Errore", e.message); onClose(); }
    finally { setLoading(false); }
  }, [submissionId, api]);
  useEffect(() => { load(); }, [load]);

  if (loading || !data) return <View style={{ flex: 1, backgroundColor: C.bg }}><Header title="Correzione" onClose={onClose} /><Loader /></View>;

  const status = fromEnum(data.status, SUBMISSION_STATUS);
  const editable = status === "pending_review" || status === "failed_auto" || status === "graded";
  const openAnswers = data.answers.filter(a => fromEnum(a.questionType, QUESTION_TYPE) === "open");
  const reviewedCount = openAnswers.filter(a => points[a.answerId] !== "").length;
  const suggested = data.suggestedGrade;
  const differs = grade !== "" && suggested !== null && suggested !== undefined && Math.round(num(grade) * 100) !== Math.round(suggested * 100);

  async function saveReview() {
    const payload = [];
    for (const a of openAnswers) {
      const v = points[a.answerId];
      if (v === "") continue;
      const n = num(v);
      if (isNaN(n) || n < 0 || n > a.maxPoints) return notify("Attenzione", `I punti per “${a.questionText.slice(0, 40)}” devono essere tra 0 e ${a.maxPoints}.`);
      payload.push({ answerId: a.answerId, awardedPoints: n, comment: comments[a.answerId]?.trim() || null });
    }
    if (!payload.length) return notify("Attenzione", "Assegna i punti ad almeno una risposta aperta.");
    setBusy(true);
    try { await api.reviewSubmission(submissionId, { answers: payload }); await load(); onChanged(); }
    catch (e) { notify("Errore", e.message); }
    finally { setBusy(false); }
  }

  async function finalize() {
    const n = grade === "" ? null : num(grade);
    if (n !== null && (isNaN(n) || n < 1 || n > 10)) return notify("Attenzione", "Il voto deve essere compreso tra 1 e 10.");
    if (differs && !reason.trim()) return notify("Attenzione", "Il voto è diverso da quello suggerito: indica il motivo.");
    setBusy(true);
    try {
      await api.finalizeSubmission(submissionId, { finalGrade: n, overrideReason: differs ? reason.trim() : null });
      await load(); onChanged();
    } catch (e) { notify("Errore", e.message); }
    finally { setBusy(false); }
  }

  return (
    <View style={{ flex: 1, backgroundColor: "#EEEBF9" }}>
      <Header title={`${data.studentLastName} ${data.studentFirstName}`} subtitle={data.examName} onClose={onClose} />
      <ScrollView contentContainerStyle={{ padding: 16, alignItems: "center" }} keyboardShouldPersistTaps="handled">
        <View style={{ width: "100%", maxWidth: 760 }}>
          <Card style={{ marginBottom: 12 }}>
            <View style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 8 }}>
              <StatusChip label={SUBMISSION_STATUS_LABEL[status]} tone={SUBMISSION_TONE[status]} />
              {data.violationCount > 0 && <Text style={{ color: C.danger, fontWeight: "600" }}>{data.violationCount} uscite dal test</Text>}
            </View>
            <Text style={[s.itemSub, { marginTop: 8 }]}>
              Consegnato il {formatDateTime(data.submittedAt) || "—"}
              {data.submitReason !== null && data.submitReason !== undefined ? ` · ${REASON_LABEL[fromEnum(data.submitReason, SUBMIT_REASON)] || ""}` : ""}
            </Text>
            <View style={{ flexDirection: "row", flexWrap: "wrap", gap: 8, marginTop: 12 }}>
              {[["Chiuse", fmt(data.closedScore)], ["Aperte (max)", fmt(data.openMaxPoints)], ["Aperte (assegnati)", fmt(data.openScore)],
                ["Totale", `${fmt(data.totalScore)} / ${fmt(data.maxScore)}`], ["Soglia", fmt(data.passingScore)], ["Voto suggerito", fmt(data.suggestedGrade)]].map(([l, v]) => (
                <View key={l} style={{ flexGrow: 1, minWidth: 110, alignItems: "center", paddingVertical: 8, borderRadius: 10, backgroundColor: C.bg }}>
                  <Text style={{ fontSize: 17, fontWeight: "800", color: C.text }}>{v}</Text>
                  <Text style={s.itemSub}>{l}</Text>
                </View>
              ))}
            </View>
            {status === "failed_auto" && (
              <Text style={{ color: C.danger, marginTop: 10 }}>
                Esame non superabile: i punti delle chiuse più il massimo delle aperte non raggiungono la soglia. Lo studente non vede il voto finché non lo definisci.
              </Text>
            )}
          </Card>

          {data.answers.map((a, i) => {
            const type = fromEnum(a.questionType, QUESTION_TYPE);
            return (
              <Card key={a.answerId} style={{ marginBottom: 12, borderLeftWidth: 5, borderLeftColor: type === "open" ? C.warning : a.isCorrect ? C.success : C.danger }}>
                <View style={{ flexDirection: "row", justifyContent: "space-between", gap: 10 }}>
                  <Text style={{ flex: 1, color: C.text, fontWeight: "600" }}>{i + 1}. {a.questionText}</Text>
                  <Text style={{ color: C.textMuted }}>{type === "open" ? `max ${a.maxPoints} pt` : `${fmt(a.awardedPoints ?? 0)} / ${a.maxPoints} pt`}</Text>
                </View>
                {type === "open" ? (
                  <View style={{ marginTop: 8 }}>
                    <Text style={{ color: a.openText ? C.text : C.textLight, backgroundColor: C.bg, borderRadius: 8, padding: 10 }}>
                      {a.openText || "Nessuna risposta"}
                    </Text>
                    {editable && (
                      <View style={{ flexDirection: "row", alignItems: "center", gap: 8, marginTop: 10, flexWrap: "wrap" }}>
                        <TextInput style={[s.input, { width: 80, textAlign: "center" }]} keyboardType="decimal-pad" placeholder="Punti" placeholderTextColor={C.textLight}
                          value={points[a.answerId] ?? ""} onChangeText={(v) => setPoints(p => ({ ...p, [a.answerId]: v }))} />
                        <Text style={{ color: C.textMuted }}>/ {a.maxPoints}</Text>
                        <TextInput style={[s.input, { flex: 1, minWidth: 160 }]} placeholder="Commento (facoltativo)" placeholderTextColor={C.textLight}
                          value={comments[a.answerId] ?? ""} onChangeText={(v) => setComments(p => ({ ...p, [a.answerId]: v }))} />
                      </View>
                    )}
                    {!editable && (a.awardedPoints !== null && a.awardedPoints !== undefined) && (
                      <Text style={[s.itemSub, { marginTop: 8 }]}>Punti assegnati: {a.awardedPoints}{a.teacherComment ? ` · ${a.teacherComment}` : ""}</Text>
                    )}
                  </View>
                ) : (
                  <View style={{ marginTop: 6 }}>
                    {a.options.map(o => (
                      <View key={o.id} style={{ flexDirection: "row", alignItems: "center", gap: 8, paddingVertical: 3 }}>
                        <Ionicons name={o.isSelected ? (type === "multiple" ? "checkbox" : "radio-button-on") : (type === "multiple" ? "square-outline" : "radio-button-off")}
                          size={20} color={o.isSelected ? (o.isCorrect ? C.success : C.danger) : C.textLight} />
                        <Text style={{ flex: 1, color: C.text }}>{o.text}</Text>
                        {o.isCorrect && <Text style={{ color: C.success, fontSize: 12 }}>giusta</Text>}
                      </View>
                    ))}
                  </View>
                )}
              </Card>
            );
          })}

          {editable && openAnswers.length > 0 && (
            <Btn label={`Salva correzione (${reviewedCount}/${openAnswers.length} aperte)`} variant="secondary" onPress={saveReview} loading={busy} style={{ marginBottom: 12 }} />
          )}

          {editable ? (
            <Card style={{ marginBottom: 32 }}>
              <Text style={{ color: C.text, fontWeight: "700", fontSize: 16, marginBottom: 4 }}>Voto finale</Text>
              <Text style={[s.itemSub, { marginBottom: 10 }]}>
                Suggerito: {fmt(suggested)}. Se scegli un voto diverso devi indicare il motivo. Dopo la conferma lo studente vede il voto.
              </Text>
              <View style={{ flexDirection: "row", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
                <TextInput style={[s.input, { width: 100, textAlign: "center" }]} keyboardType="decimal-pad" value={grade} onChangeText={setGrade} placeholder="1–10" placeholderTextColor={C.textLight} />
                {differs && (
                  <TextInput style={[s.input, { flex: 1, minWidth: 200 }]} value={reason} onChangeText={setReason} placeholder="Motivo del voto diverso" placeholderTextColor={C.textLight} />
                )}
              </View>
              <Btn label="Assegna il voto" variant="success" onPress={finalize} loading={busy} style={{ marginTop: 12 }} />
            </Card>
          ) : (
            <Card style={{ marginBottom: 32 }}>
              <Text style={{ color: C.text, fontWeight: "700" }}>{status === "finalized" ? `Voto assegnato: ${fmt(data.finalGrade)}` : "Consegna non ancora correggibile"}</Text>
              {!!data.finalizedAt && <Text style={s.itemSub}>Il {formatDateTime(data.finalizedAt)}</Text>}
            </Card>
          )}
        </View>
      </ScrollView>
    </View>
  );
}

// ── Elenco delle consegne ───────────────────────────────────────────────────────────────────
export default function ExamSubmissions({ exam, api, onClose }) {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState("all");
  const [openId, setOpenId] = useState(null);

  const load = useCallback(async () => {
    setLoading(true);
    try { setItems(await api.listSubmissions(exam.id)); }
    catch (e) { notify("Errore", e.message); setItems([]); }
    finally { setLoading(false); }
  }, [api, exam.id]);
  useEffect(() => { load(); }, [load]);

  const statusOf = (it) => fromEnum(it.status, SUBMISSION_STATUS);
  const f = FILTERS.find(x => x.key === filter);
  const visible = items.filter(it => filter === "all" || (f.match ? f.match.includes(statusOf(it)) : statusOf(it) === filter));
  const count = (x) => items.filter(it => x.key === "all" || (x.match ? x.match.includes(statusOf(it)) : statusOf(it) === x.key)).length;

  return (
    <Modal visible animationType="slide" onRequestClose={openId ? () => setOpenId(null) : onClose}>
      {openId ? (
        <SubmissionReview submissionId={openId} api={api} onClose={() => setOpenId(null)} onChanged={load} />
      ) : (
        <View style={{ flex: 1, backgroundColor: C.bg }}>
          <Header title="Consegne" subtitle={exam.name} onClose={onClose} />
          <ScrollView horizontal showsHorizontalScrollIndicator={false} style={{ flexGrow: 0 }} contentContainerStyle={{ padding: 12, gap: 8 }}>
            {FILTERS.map(x => (
              <TouchableOpacity key={x.key} onPress={() => setFilter(x.key)}
                style={{ paddingVertical: 7, paddingHorizontal: 14, borderRadius: 18, borderWidth: 1, borderColor: filter === x.key ? C.primary : C.border, backgroundColor: filter === x.key ? C.primary : C.white }}>
                <Text style={{ color: filter === x.key ? C.white : C.text, fontWeight: "600" }}>{x.label} ({count(x)})</Text>
              </TouchableOpacity>
            ))}
          </ScrollView>

          {loading ? <Loader /> : visible.length === 0 ? <EmptyState message="Nessuna consegna" /> : (
            <ScrollView contentContainerStyle={{ padding: 16, paddingTop: 4, alignItems: "center" }}>
              <View style={{ width: "100%", maxWidth: 760 }}>
                {visible.map(it => {
                  const st = statusOf(it);
                  return (
                    <TouchableOpacity key={it.id} activeOpacity={0.7} onPress={() => setOpenId(it.id)}>
                      <Card style={{ marginBottom: 10, flexDirection: "row", alignItems: "center" }}>
                        <View style={{ flex: 1 }}>
                          <Text style={s.itemTitle}>{it.studentLastName} {it.studentFirstName}</Text>
                          <Text style={s.itemSub}>
                            {it.submittedAt ? `Consegnato il ${formatDateTime(it.submittedAt)}` : "Non ancora consegnato"}
                            {it.totalScore !== null && it.totalScore !== undefined ? ` · ${fmt(it.totalScore)}/${fmt(it.maxScore)}` : ""}
                            {it.violationCount > 0 ? ` · ${it.violationCount} uscite` : ""}
                          </Text>
                        </View>
                        <StatusChip label={SUBMISSION_STATUS_LABEL[st]} tone={SUBMISSION_TONE[st]} />
                        <Ionicons name="chevron-forward" size={20} color={C.textLight} style={{ marginLeft: 8 }} />
                      </Card>
                    </TouchableOpacity>
                  );
                })}
              </View>
            </ScrollView>
          )}
        </View>
      )}
    </Modal>
  );
}
