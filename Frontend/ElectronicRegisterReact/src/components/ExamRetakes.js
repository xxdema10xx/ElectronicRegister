// src/components/ExamRetakes.js
// Pagina "Chi deve recuperare" (/esami/recuperi): studenti non in regola, con gli esami che li riguardano.
import { ScrollView, Text, View } from "react-native";
import { C, s } from "../constants/theme";
import Card from "./Card";
import EmptyState from "./EmptyState";
import Loader from "./Loader";
import RouteLink from "./RouteLink";
import StatusChip from "./StatusChip";
import { RETAKE_LABEL, RETAKE_TONE, retakeSummary } from "../api/examInsights";

const SCOPE = "non in regola su almeno un esame pubblicato o chiuso";

// failed: esami di cui non si sono lette le consegne. labelOf(exam), hrefOf(id) e onOpenExam(id) li dà ExamsScreen.
export default function ExamRetakes({ loading, groups, failed, labelOf, hrefOf, onOpenExam }) {
  if (loading) return <Loader />;
  return (
    <ScrollView contentContainerStyle={{ padding: 16, alignItems: "center" }}>
      <View style={{ width: "100%", maxWidth: 760 }}>
        <Text style={[s.itemSub, { marginBottom: 12 }]}>{failed ? `Studenti ${SCOPE}` : `${retakeSummary(groups)} · ${SCOPE}`}</Text>
        {failed > 0 && groups.length > 0 && (
          <View style={{ backgroundColor: "#FEF3C7", borderRadius: 10, padding: 12, marginBottom: 10 }}>
            <Text style={{ color: "#92400E" }}>Impossibile caricare le consegne di {failed === 1 ? "un esame" : `${failed} esami`}: l'elenco potrebbe essere incompleto.</Text>
          </View>
        )}
        {groups.length === 0 ? <EmptyState message={failed ? "Impossibile caricare le consegne" : "Nessuno studente da recuperare"} /> : groups.map(g => (
          <Card key={g.studentId} style={{ marginBottom: 10 }}>
            <Text style={s.itemTitle}>{g.lastName} {g.firstName}</Text>
            {g.exams.map(e => (
              <View key={e.id} style={{ flexDirection: "row", alignItems: "center", justifyContent: "space-between", gap: 8, marginTop: 6 }}>
                <RouteLink href={hrefOf(e.id)} onPress={() => onOpenExam(e.id)} style={{ flex: 1, minWidth: 0 }} accessibilityLabel={`Apri ${labelOf(e)}`}>
                  <Text style={{ color: C.primary, textDecorationLine: "underline" }} numberOfLines={1}>{labelOf(e)}</Text>
                </RouteLink>
                <StatusChip label={RETAKE_LABEL[e.reason] || e.reason} tone={RETAKE_TONE[e.reason]} />
              </View>
            ))}
          </Card>
        ))}
      </View>
    </ScrollView>
  );
}
