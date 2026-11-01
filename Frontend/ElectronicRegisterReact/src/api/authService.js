// src/api/authService.js
// Accesso in due passi (docs/AUTH.md): login → ticket (5 min) → scelta istituto → token istituzionale (15 min).
import axiosClient, { authHeader, ticketHeader } from "./axiosClient";
import { session } from "./session";

// ── Identità ────────────────────────────────────────────────────────────────────────────────
export async function login(email, password) {
  const res = await axiosClient.post("/Auth/identity/login", { email, password });
  session.setTicket(res.data.ticket, res.data.expiresInSeconds);
  return res.data; // { ticket, expiresInSeconds }
}

export async function loginWithMicrosoft(msAccessToken) {
  const res = await axiosClient.post("/Auth/identity/microsoft-login", { accessToken: msAccessToken });
  session.setTicket(res.data.ticket, res.data.expiresInSeconds);
  return res.data;
}

// Il backend accetta solo email e password: nome e cognome appartengono all'anagrafica dell'istituto
export async function register({ email, password }) {
  const res = await axiosClient.post("/identity/accounts", { email: email.trim(), password });
  return res.data; // userId
}

// ── Istituti ────────────────────────────────────────────────────────────────────────────────
export async function getInstitutions(ticket) {
  const res = await axiosClient.get("/Auth/institutions", { headers: ticketHeader(ticket) });
  return Array.isArray(res.data) ? res.data : [];
}

export async function selectInstitution(ticket, choice) {
  const res = await axiosClient.post("/Auth/select-institution",
    { institutionId: choice.institutionId, membershipId: choice.membershipId },
    { headers: ticketHeader(ticket) });
  session.setAccess(res.data.token, res.data.expiresInSeconds,
    { institutionId: choice.institutionId, membershipId: choice.membershipId, name: choice.name });
  return res.data; // { token, expiresInSeconds }
}

export async function getContext(token) {
  const res = await axiosClient.get("/Auth/context", { headers: authHeader(token) });
  return res.data; // { userId, institutionId, membershipId, roles }
}

// ── Account (richiede il ticket) ────────────────────────────────────────────────────────────
export async function getAccount(ticket) {
  const res = await axiosClient.get("/identity/account", { headers: ticketHeader(ticket) });
  return res.data; // { userId, email, displayName, hasLocalPassword }
}

export async function updateDisplayName(ticket, displayName) {
  await axiosClient.put("/identity/account", { displayName: displayName.trim() }, { headers: ticketHeader(ticket) });
}

// Revoca tutti i token dell'utente: dopo serve un nuovo login
export async function changePassword(ticket, currentPassword, newPassword) {
  await axiosClient.put("/identity/account/password", { currentPassword, newPassword }, { headers: ticketHeader(ticket) });
}

// Ticket valido per le operazioni sull'account: quello corrente o uno nuovo con le credenziali in memoria
export async function freshTicket() {
  if (session.ticket) return session.ticket;
  if (session.credentials) return (await login(session.credentials.email, session.credentials.password)).ticket;
  throw new Error("Per questa operazione esci e accedi di nuovo.");
}

// ── Profilo dell'utente nell'istituto ───────────────────────────────────────────────────────
const ROLE_PRIORITY = [["InstitutionAdmin", "admin"], ["Staff", "staff"], ["Teacher", "teacher"], ["Student", "student"]];
export const primaryRole = (roles = []) => ROLE_PRIORITY.find(([r]) => roles.includes(r))?.[1] || "student";

// Costruisce l'oggetto `user` usato dalle schermate: ruolo principale, istituto, profilo docente/studente.
export async function buildUser(token, account) {
  const ctx = await getContext(token);
  const roles = ctx.roles || [];
  const user = {
    id: ctx.userId,
    email: account?.email || session.credentials?.email || "",
    displayName: account?.displayName || null,
    roles,
    role: primaryRole(roles),
    institutionId: ctx.institutionId,
    membershipId: ctx.membershipId,
    institutionName: session.institution?.name || "",
  };
  const headers = { headers: authHeader(token) };
  // Il profilo docente/studente esiste solo se collegato alla membership: senza, le funzioni "mie" non sono disponibili
  if (roles.includes("Teacher")) {
    try {
      const t = (await axiosClient.get("/institution/teachers/me", headers)).data;
      Object.assign(user, { teacherId: t.id, teacherFirstName: t.firstName, teacherLastName: t.lastName });
    } catch { /* profilo docente non collegato */ }
  }
  if (roles.includes("Student")) {
    try {
      const st = (await axiosClient.get("/institution/students/me", headers)).data;
      Object.assign(user, { studentId: st.id, studentFirstName: st.firstName, studentLastName: st.lastName });
    } catch { /* profilo studente non collegato */ }
  }
  return user;
}
