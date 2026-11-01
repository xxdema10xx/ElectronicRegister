// Configurazione dell'app. Non contiene segreti: i valori si leggono dal file .env (vedi .env.example),
// che è ignorato da git. Questo file è tracciato nel repository.

import { Platform } from "react-native";

// URL del backend ElectronicRegisterAPI, comprensivo di "/api" (vedi docs/README.md §4).
// Su web in sviluppo, se è impostato EXPO_PUBLIC_DEV_PROXY, si passa dal proxy di Metro (metro.config.js)
// con un percorso relativo: stessa origine, quindi niente blocchi CORS.
const useDevProxy = Platform.OS === "web" && __DEV__ && !!process.env.EXPO_PUBLIC_DEV_PROXY;
export const API_BASE = useDevProxy
  ? "/api"
  : process.env.EXPO_PUBLIC_API_BASE || "http://localhost:5257/api";

// Login Microsoft (Entra ID): ID applicazione e ID directory (tenant) della registrazione app su Azure
export const MS_CLIENT_ID = process.env.EXPO_PUBLIC_MS_CLIENT_ID || "";
const MS_TENANT_ID = process.env.EXPO_PUBLIC_MS_TENANT_ID || "common";

// Opzioni per AuthSession.makeRedirectUri. Deve coincidere con l'URI registrato su Entra ID:
// su web http://localhost:8081/auth, su nativo electronicregister://auth (schema in app.json)
export const MS_REDIRECT = { scheme: "electronicregister", path: "auth" };

export const msDiscovery = {
  authorizationEndpoint: `https://login.microsoftonline.com/${MS_TENANT_ID}/oauth2/v2.0/authorize`,
  tokenEndpoint: `https://login.microsoftonline.com/${MS_TENANT_ID}/oauth2/v2.0/token`,
};
