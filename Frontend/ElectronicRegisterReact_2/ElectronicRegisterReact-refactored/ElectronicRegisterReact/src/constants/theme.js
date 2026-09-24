// src/constants/theme.js
import { StyleSheet } from "react-native";

export const C = {
  primary: "#154f78",
  primaryDk: "#4338CA",
  secondary: "#7C3AED",
  success: "#10B981",
  danger: "#EF4444",
  warning: "#F59E0B",
  bg: "#F8FAFC",
  card: "#FFFFFF",
  footer: "#154f78",
  border: "#E2E8F0",
  text: "#1E293B",
  textMuted: "#64748B",
  textLight: "#94A3B8",
  white: "#FFFFFF",
  studentBadge: "#DBEAFE",
  teacherBadge: "#D1FAE5",
  adminBadge: "#FEE2E2",
};

export const ROLE_COLOR = { student: C.studentBadge, teacher: C.teacherBadge, admin: C.adminBadge };
export const ROLE_TEXT = { student: "#1D4ED8", teacher: "#065F46", admin: "#991B1B" };

export const s = StyleSheet.create({
  // Auth
  authBg: { flex: 1, backgroundColor: C.primary },
  authHeader: { alignItems: "center", paddingTop: 60, paddingBottom: 32 },
  authLogo: { fontSize: 48, marginBottom: 8 },
  authTitle: { fontSize: 26, fontWeight: "800", color: C.white },
  authSubtitle: { fontSize: 14, color: "rgba(255,255,255,0.7)", marginTop: 4 },
  authBody: { backgroundColor: C.bg, borderTopLeftRadius: 28, borderTopRightRadius: 28, padding: 24, flexGrow: 1 },
  // Card
  card: { backgroundColor: C.card, borderRadius: 14, padding: 16, shadowColor: "#000", shadowOpacity: 0.06, shadowRadius: 8, shadowOffset: { width: 0, height: 2 }, elevation: 3 },
  cardTitle: { fontSize: 18, fontWeight: "700", color: C.text, marginBottom: 16 },
  // Inputs
  label: { fontSize: 13, fontWeight: "600", color: C.textMuted, marginBottom: 6 },
  input: { backgroundColor: C.bg, borderWidth: 1, borderColor: C.border, borderRadius: 10, padding: 12, fontSize: 15, color: C.text, height: 48 },
  inputError: { borderColor: C.danger, borderWidth: 1.5 },
  errorText: { color: C.danger, fontSize: 12, marginTop: 4 },
  // Select field
  selectBox: { backgroundColor: C.bg, borderWidth: 1, borderColor: C.border, borderRadius: 10, padding: 12, flexDirection: "row", alignItems: "center", justifyContent: "space-between", height: 48 },
  selectOption: { flexDirection: "row", alignItems: "center", justifyContent: "space-between", paddingVertical: 14, paddingHorizontal: 4, borderBottomWidth: 1, borderBottomColor: C.border },
  selectOptionActive: { backgroundColor: "#EFF6FF" },
  selectOptionText: { fontSize: 15, color: C.text },
  selectOptionTextActive: { color: C.primary, fontWeight: "700" },
  // Buttons
  btn: { borderRadius: 10, padding: 14, alignItems: "center", justifyContent: "center", flexDirection: "row" },
  btnText: { fontSize: 15, fontWeight: "700" },
  smBtn: { paddingVertical: 6, paddingHorizontal: 12, borderRadius: 8 },
  smBtnText: { fontSize: 13 },
  // Badge
  badge: { paddingHorizontal: 10, paddingVertical: 4, borderRadius: 20 },
  badgeText: { fontSize: 12, fontWeight: "700", textTransform: "uppercase" },
  // Dashboard
  dashHeader: { backgroundColor: C.primary, padding: 20, paddingTop: 16, flexDirection: "row", alignItems: "center", justifyContent: "space-between" },
  dashWelcome: { color: "rgba(255,255,255,0.7)", fontSize: 13 },
  dashName: { color: C.white, fontSize: 20, fontWeight: "800" },
  dashGrid: { flexDirection: "row", flexWrap: "wrap", gap: 12 },
  dashCard: { width: "47%", backgroundColor: C.card, borderRadius: 14, padding: 20, alignItems: "center", shadowColor: "#000", shadowOpacity: 0.05, shadowRadius: 6, elevation: 2 },
  dashCardIcon: { fontSize: 32, marginBottom: 8 },
  dashCardLabel: { fontSize: 14, fontWeight: "600", color: C.text, textAlign: "center" },
  // Top/Tab bar
  topBar: { flexDirection: "row", alignItems: "center", justifyContent: "center", paddingHorizontal: 16, paddingVertical: 14 },
  topBarTitle: { color: C.white, fontSize: 24, fontWeight: "700" },
  tabBar: { flexDirection: "row" },
  tab: { flex: 1, alignItems: "center", paddingVertical: 8 },
  tabLabel: { fontSize: 11, color: C.textLight, marginTop: 2 },
  tabLabelActive: { color: C.primary, fontWeight: "700" },
  // List items
  screenPad: { padding: 16, paddingBottom: 0 },
  sectionHeader: { flexDirection: "row", alignItems: "center", justifyContent: "space-between", marginBottom: 12 },
  sectionTitle: { fontSize: 18, fontWeight: "800", color: C.text },
  itemTitle: { fontSize: 15, fontWeight: "600", color: C.text },
  itemSub: { fontSize: 13, color: C.textMuted, marginTop: 2 },
  actionIcon: { fontSize: 18 },
  avatar: { width: 44, height: 44, borderRadius: 22, backgroundColor: "#DBEAFE", alignItems: "center", justifyContent: "center" },
  avatarText: { fontWeight: "700", color: "#1D4ED8", fontSize: 15 },
  // Grade circle
  gradeCircle: { width: 48, height: 48, borderRadius: 24, alignItems: "center", justifyContent: "center" },
  gradeValue: { color: C.white, fontWeight: "800", fontSize: 16 },
  // Profile
  profileHeader: { alignItems: "center", paddingVertical: 24 },
  profileAvatar: { width: 72, height: 72, borderRadius: 36, backgroundColor: C.primary, alignItems: "center", justifyContent: "center", marginBottom: 12 },
  profileAvatarText: { color: C.white, fontSize: 28, fontWeight: "800" },
  profileName: { fontSize: 20, fontWeight: "800", color: C.text, marginBottom: 4 },
  profileEmail: { fontSize: 14, color: C.textMuted, marginBottom: 10 },
  profileRow: { flexDirection: "row", justifyContent: "space-between", paddingVertical: 8, borderBottomWidth: 1, borderBottomColor: C.border },
  profileKey: { fontSize: 14, color: C.textMuted },
  profileVal: { fontSize: 14, color: C.text, fontWeight: "600", maxWidth: "60%" },
  // Modal
  modalOverlay: { zIndex: 1000, flex: 1, backgroundColor: "rgba(0,0,0,0.45)", justifyContent: "flex-end" },
  modalCard: { zIndex: 1000, backgroundColor: C.card, borderTopLeftRadius: 24, borderTopRightRadius: 24, padding: 24, maxHeight: "85%" },
  // Form modal (aggiungi/modifica voto, studente, materia, utente, profilo…): centrato invece che
  // ancorato in basso, così non finisce sotto la barra di navigazione del telefono.
  formModalOverlay: { flex: 1, backgroundColor: "rgba(0,0,0,0.45)", justifyContent: "center", alignItems: "center", padding: 20 },
  formModalCard: { backgroundColor: C.card, borderRadius: 20, padding: 24, width: "100%", maxWidth: 420, maxHeight: "80%" },
  modalHeader: { flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginBottom: 20 },
  modalTitle: { fontSize: 18, fontWeight: "700", color: C.text },
  modalClose: { fontSize: 20, color: C.textMuted },
  // Role selector
  roleBtn: { flex: 1, paddingVertical: 8, borderRadius: 8, borderWidth: 1, borderColor: C.border, alignItems: "center" },
  roleBtnActive: { backgroundColor: C.primary, borderColor: C.primary },
  roleBtnText: { fontSize: 13, color: C.textMuted, fontWeight: "600" },
  roleBtnTextActive: { color: C.white },
  // Empty / Loader
  emptyState: { flex: 1, alignItems: "center", justifyContent: "center", paddingTop: 60 },
  emptyIcon: { fontSize: 40, marginBottom: 12 },
  emptyText: { fontSize: 15, color: C.textMuted },
  loaderWrap: { flex: 1, alignItems: "center", justifyContent: "center" },
});
