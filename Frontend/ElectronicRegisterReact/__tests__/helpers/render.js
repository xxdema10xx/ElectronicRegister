// Helper condivisi dai test delle schermate.
import { Alert, View } from "react-native";
import { act, fireEvent, render, screen } from "@testing-library/react-native";
import { AuthContext } from "../../src/hooks/useAuth";
import { DialogHost } from "../../src/components/AppDialog";

export const USERS = {
  admin: { id: "u-admin", role: "admin", roles: ["InstitutionAdmin"], email: "admin@scuola.it", displayName: "Admin" },
  staff: { id: "u-staff", role: "staff", roles: ["Staff"], email: "segreteria@scuola.it", displayName: "Segreteria" },
  teacher: { id: "u-teacher", role: "teacher", roles: ["Teacher"], email: "docente@scuola.it", teacherId: "t1", teacherFirstName: "Anna", teacherLastName: "Neri" },
  student: { id: "u-student", role: "student", roles: ["Student"], email: "studente@scuola.it", studentId: "s1", studentFirstName: "Mario", studentLastName: "Rossi" },
};

// Rende `ui` con un utente collegato e il contenitore dei dialog dell'app (notify / confirmAction)
export function renderAs(role, ui, { user, token = "tok", logout = jest.fn(), updateUser = jest.fn() } = {}) {
  const value = { token, user: user || USERS[role], logout, updateUser };
  const utils = render(
    <AuthContext.Provider value={value}>
      <View style={{ flex: 1 }}>
        {ui}
        <DialogHost />
      </View>
    </AuthContext.Provider>
  );
  return { ...utils, auth: value };
}

// Preme un pulsante del dialog dell'app (es. "Elimina" in una conferma)
export async function pressDialog(label) {
  const matches = await screen.findAllByText(label);
  await act(async () => { fireEvent.press(matches.at(-1)); });
}

// Ultima chiamata ad Alert.alert (schermate che usano l'Alert di sistema)
export const lastAlert = () => Alert.alert.mock.calls[Alert.alert.mock.calls.length - 1];

// Preme il pulsante `label` dell'ultimo Alert.alert (es. "Elimina" nella conferma)
export async function pressAlertButton(label) {
  const [, , buttons] = lastAlert();
  const button = buttons.find(b => b.text === label);
  await act(async () => { await button.onPress?.(); });
}

// Lascia completare le promesse pendenti (caricamenti in useEffect)
export const flush = () => act(async () => { await new Promise(r => setTimeout(r, 0)); });
