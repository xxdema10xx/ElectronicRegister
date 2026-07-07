// --- Configurazione MSAL ---
const msalConfig = {
  auth: {
    clientId: "df405eeb-4453-4f41-86d7-2a4af11446b6",
    authority: "https://login.microsoftonline.com/common",
    redirectUri: "http://localhost/ElectronicRegister/Frontend/ElectronicRegisterWeb/Authentication/login.html"
  },
  cache: {
    cacheLocation: "sessionStorage"
  }
};

const msalInstance = new msal.PublicClientApplication(msalConfig);

// --- Scope della tua API ---
const loginRequest = {
  scopes: ["api://df405eeb-4453-4f41-86d7-2a4af11446b6/access_as_user"],
  prompt: "select_account"
};

// --- Gestisci il ritorno dal redirect (v2: nessun .initialize() necessario) ---
msalInstance.handleRedirectPromise()
  .then((response) => {
    if (response) {
      handleLoginSuccess(response.accessToken);
    }
  })
  .catch((error) => {
    console.error("Errore nel redirect:", error);
  });

// --- Click sul bottone: avvia il login ---
document.getElementById("ms-login-btn").addEventListener("click", () => {
  msalInstance.loginRedirect(loginRequest);
});

// --- Cosa fare col token ottenuto ---
function handleLoginSuccess(accessToken) {
  console.log("TOKEN RICEVUTO:", accessToken);
  console.log("PAYLOAD DECODIFICATO:", JSON.parse(atob(accessToken.split('.')[1])));

  fetch(`${CONFIG.API_BASE}/api/auth/microsoft-login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ accessToken: accessToken })
  })
    .then(async (res) => {
      if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || `Errore ${res.status}`);
      }
      return res.json();
    })
    .then((data) => {
      localStorage.setItem('jwt', data.token);
      showSuccess();
      setTimeout(() => window.location.href = '../Dashboard/index.html', 1500);
    })
    .catch((err) => {
      console.error(err);
      showError('Accesso con Microsoft fallito: ' + err.message);
    });
}