// src/api/userService.js
// Membri dell'istituto (solo amministratore, docs/API.md §5–6). Gli account si creano con la registrazione;
// l'ingresso nell'istituto avviene tramite invito, quindi qui si gestiscono ruoli e sospensioni dei membri esistenti.
import * as Crypto from "expo-crypto";
import axiosClient, { authHeader } from "./axiosClient";

const newEventId = () => globalThis.crypto?.randomUUID?.() ?? Crypto.randomUUID();
const PAGE = 100;

// Tutti i membri (il server non restituisce il totale: si legge finché la pagina non è piena)
export async function getMembers(token, { status, role, search } = {}) {
  const params = {};
  if (status) params.status = status;
  if (role) params.role = role;
  if (search?.trim()) params.search = search.trim().slice(0, 100);
  const out = [];
  for (let pageNumber = 1; ; pageNumber++) {
    const res = await axiosClient.get("/institution/admin/members", { headers: authHeader(token), params: { ...params, pageNumber, pageSize: PAGE } });
    const items = Array.isArray(res.data) ? res.data : [];
    out.push(...items);
    if (items.length < PAGE) return out;
  }
}

// Compatibilità con la dashboard (conteggio utenti)
export const getUsers = (_search, token) => getMembers(token);

// La revisione attesa dal server non è nel dettaglio del membro: si ricava dall'ultimo evento di audit
// (revisionAfter). Senza eventi si parte da 0. Vedi docs/OPEN_QUESTIONS.md Q-06.
async function currentRevision(membershipId, token) {
  const res = await axiosClient.get("/institution/admin/audit", { headers: authHeader(token), params: { target: membershipId, page: 1, size: 100 } });
  const events = Array.isArray(res.data) ? res.data : [];
  return events.reduce((max, e) => Math.max(max, Number(e.revisionAfter) || 0), 0);
}

// action: "AddRole" | "RemoveRole" | "Suspend" | "Reactivate"; role solo per AddRole/RemoveRole
export async function mutateMember(membershipId, { action, role, reason }, token) {
  const body = { eventId: newEventId(), expectedRevision: await currentRevision(membershipId, token), action, reason: reason.trim() };
  if (action === "AddRole" || action === "RemoveRole") body.role = role;
  const res = await axiosClient.post(`/institution/admin/members/${membershipId}/mutations`, body, { headers: authHeader(token) });
  return res.data; // { eventId, revision, status, roles }
}
