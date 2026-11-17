// jest.setup.js — configurazione comune dei test (vedi __tests__/README.md)
import { Alert } from "react-native";

// UUID deterministici per gli eventId (calendario, membri, violazioni)
jest.mock("expo-crypto", () => ({ randomUUID: jest.fn(() => "00000000-0000-4000-8000-000000000000") }));

// Icone: l'app importa @expo/vector-icons, che però è solo una dipendenza interna di expo (non dichiarata
// in package.json) e Jest non la trova. Nei test basta il nome dell'icona, utile anche per cercarla.
jest.mock("@expo/vector-icons", () => {
  const { Text } = require("react-native");
  const Icon = ({ name, testID }) => <Text testID={testID || `icon-${name}`}>{""}</Text>;
  return { Ionicons: Icon, MaterialIcons: Icon, MaterialCommunityIcons: Icon };
}, { virtual: true });

// Margini dello schermo: mock ufficiale della libreria (senza, SafeAreaProvider non mostra i figli)
jest.mock("react-native-safe-area-context", () => require("react-native-safe-area-context/jest/mock").default);

// Selettore di file: ogni test decide cosa "sceglie" l'utente
jest.mock("expo-document-picker", () => ({ getDocumentAsync: jest.fn() }));

// Login Microsoft: nei test non c'è un browser di sistema
jest.mock("expo-web-browser", () => ({ maybeCompleteAuthSession: jest.fn(), openAuthSessionAsync: jest.fn() }));
jest.mock("./src/hooks/useMicrosoftLogin", () => ({
  useMicrosoftLogin: () => ({ request: {}, response: null, promptAsync: jest.fn(), exchangeCode: jest.fn() }),
}));

// Alert di sistema: sull'app nativa non c'è un modulo nativo nei test, si registra solo la chiamata
beforeEach(() => {
  jest.spyOn(Alert, "alert").mockImplementation(() => {});
});
