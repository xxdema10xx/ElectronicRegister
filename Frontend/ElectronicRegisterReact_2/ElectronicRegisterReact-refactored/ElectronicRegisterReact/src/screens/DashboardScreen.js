// src/screens/DashboardScreen.js
import { Image, ScrollView, Text, TouchableOpacity, View } from "react-native";
import { Ionicons } from "@expo/vector-icons";
import { C, s } from "../constants/theme";
import Badge from "../components/Badge";
import { useAuth } from "../hooks/useAuth";

export default function DashboardScreen({ navigate }) {
  const { user } = useAuth();
  const role = user?.role?.toLowerCase();

  const cards = [
    ...(role === "student" ? [
      { icon: <Ionicons name="ribbon-outline" size={32} color={C.footer} />, label: "I miei voti", screen: "grades" },
      { icon: <Ionicons name="library-outline" size={32} color={C.footer} />, label: "Materie", screen: "subjects" },
      { icon: <Ionicons name="pencil-outline" size={32} color={C.footer} />, label: "Professori", screen: "teachers" },
    ] : []),
    ...(role === "teacher" ? [
      { icon: <Ionicons name="ribbon-outline" size={32} color={C.footer} />, label: "Voti", screen: "grades" },
      { icon: <Ionicons name="school-outline" size={32} color={C.footer} />, label: "Studenti", screen: "students" },
      { icon: <Ionicons name="library-outline" size={32} color={C.footer} />, label: "Materie", screen: "subjects" },
      { icon: <Ionicons name="library-outline" size={32} color={C.footer} />, label: "Esami", screen: "exams"},
      { icon: <Ionicons name="library-outline" size={32} color={C.footer} />, label: "Test", screen: "tests"},
    ] : []),
    ...(role === "admin" ? [
      { icon: <Ionicons name="ribbon-outline" size={32} color={C.footer} />, label: "Voti", screen: "grades" },
      { icon: <Ionicons name="school-outline" size={32} color={C.footer} />, label: "Studenti", screen: "students" },
      { icon: <Ionicons name="pencil-outline" size={32} color={C.footer} />, label: "Professori", screen: "teachers" },
      { icon: <Ionicons name="library-outline" size={32} color={C.footer} />, label: "Materie", screen: "subjects" },
      { icon: <Ionicons name="people-circle-outline" size={32} color={C.footer} />, label: "Utenti", screen: "users" },
      { icon: <Ionicons name="library-outline" size={32} color={C.footer} />, label: "Esami", screen: "exams"},
    ] : []),
    { icon: <Ionicons name="person-circle-outline" size={32} color={C.footer} />, label: "Profilo", screen: "profile" },
  ];

  const name = user?.studentFirstName || user?.teacherFirstName || user?.email?.split(".")[0] || "Utente";

  return (
    <View style={{ flex: 1, backgroundColor: C.bg }}>
      <View style={[s.dashHeader, { position: "relative" }]}>
        <View style={{ flex: 1, marginRight: 8 }}>
          <Text style={s.dashWelcome}>Benvenuto,</Text>
          <Text style={s.dashName}>{name}</Text>
        </View>
        <Badge text={role} role={role} />
        <View pointerEvents="none" style={{ position: "absolute", top: -40, left: 0, right: 0, alignItems: "center" }}>
          <Image source={require("../../assets/images/logoitsTrim.png")} style={{ width: 150, height: 100 }} resizeMode="contain" />
        </View>
      </View>
      <ScrollView contentContainerStyle={{ padding: 16 }}>
        <View style={s.dashGrid}>
          {cards.map(c => (
            <TouchableOpacity key={c.screen} style={s.dashCard} onPress={() => navigate(c.screen)}>
              <Text style={s.dashCardIcon}>{c.icon}</Text>
              <Text style={s.dashCardLabel}>{c.label}</Text>
            </TouchableOpacity>
          ))}
        </View>
      </ScrollView>
    </View>
  );
}
