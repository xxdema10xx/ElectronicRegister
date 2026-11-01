// src/screens/InstitutionPickerScreen.js
// Dopo il login: l'utente è membro di più istituti e sceglie in quale entrare (GET /Auth/institutions).
import { Image, SafeAreaView, ScrollView, StatusBar, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Card from "../components/Card";
import { useLayout } from "../hooks/useLayout";

const ROLE_LABEL = { InstitutionAdmin: "Amministratore", Staff: "Segreteria", Teacher: "Docente", Student: "Studente" };

export default function InstitutionPickerScreen({ institutions, onSelect, onBack }) {
  const { isDesktop } = useLayout();

  return (
    <SafeAreaView style={s.authBg}>
      <StatusBar barStyle="light-content" backgroundColor={s.authBg.backgroundColor} />
      <ScrollView contentContainerStyle={[{ flexGrow: 1 }, isDesktop && { justifyContent: "center", alignItems: "center", padding: 24 }]}>
        <View style={[s.authHeader, isDesktop && s.authHeaderDesktop]}>
          <Image source={require("../../assets/images/logoits.png")} style={isDesktop ? { width: 100, height: 66 } : { width: 150, height: 100 }} />
          <Text style={[s.authTitle, isDesktop && { fontSize: 20 }]}>Electronic Register</Text>
        </View>
        <View style={[s.authBody, isDesktop && s.authBodyDesktop]}>
          <Card>
            <Text style={s.cardTitle}>Scegli l'istituto</Text>
            {institutions.map(i => (
              <TouchableOpacity key={i.membershipId} activeOpacity={0.7} onPress={() => onSelect(i)}
                style={{ flexDirection: "row", alignItems: "center", paddingVertical: 12, borderBottomWidth: 1, borderBottomColor: C.border }}>
                <View style={s.avatar}><Ionicons name="business-outline" size={22} color="#1D4ED8" /></View>
                <View style={{ flex: 1, marginLeft: 12 }}>
                  <Text style={s.itemTitle}>{i.name}</Text>
                  <Text style={s.itemSub}>{(i.roles || []).map(r => ROLE_LABEL[r] || r).join(" · ")}</Text>
                </View>
                <Ionicons name="chevron-forward" size={20} color={C.textLight} />
              </TouchableOpacity>
            ))}
            <TouchableOpacity onPress={onBack} style={{ marginTop: 14, alignItems: "center" }}>
              <Text style={{ color: s.authBg.backgroundColor, fontWeight: "600" }}>Torna al login</Text>
            </TouchableOpacity>
          </Card>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}
