// src/hooks/useMicrosoftLogin.js
import * as AuthSession from "expo-auth-session";
import { MS_CLIENT_ID, MS_REDIRECT, msDiscovery } from "../config";

// Incapsula la richiesta OAuth verso Microsoft Entra ID: espone `request`,
// `response` e `promptAsync` come lo useAuthRequest di expo-auth-session,
// più il redirectUri già pronto e il codeVerifier necessario per lo scambio
// del code (vedi LoginScreen).
export function useMicrosoftLogin() {
  const redirectUri = AuthSession.makeRedirectUri(MS_REDIRECT);

  const [request, response, promptAsync] = AuthSession.useAuthRequest(
    {
      clientId: MS_CLIENT_ID,
      scopes: [`api://${MS_CLIENT_ID}/access_as_user`, "openid", "profile", "offline_access"],
      redirectUri,
      responseType: AuthSession.ResponseType.Code,
      usePKCE: true,
    },
    msDiscovery
  );

  async function exchangeCode(code) {
    return AuthSession.exchangeCodeAsync(
      {
        clientId: MS_CLIENT_ID,
        code,
        redirectUri,
        extraParams: { code_verifier: request.codeVerifier },
      },
      msDiscovery
    );
  }

  return { request, response, promptAsync, redirectUri, exchangeCode };
}
