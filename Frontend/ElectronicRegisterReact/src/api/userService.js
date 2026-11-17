// src/api/userService.js
// Membri dell'istituto (solo amministratore): elenco, ruoli, sospensioni. I nuovi account li crea l'istituto
// (accountService.js), che li rende anche membri.
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

// Dettaglio di un membro: { membershipId, userId, email, status, roles, joinedAt, revision }
export async function getMember(membershipId, token) {
  const res = await axiosClient.get(`/institution/admin/members/${membershipId}`, { headers: authHeader(token) });
  return res.data;
}

// revision del DTO del membro = security generation della membership, cioè il valore che le mutazioni
// confrontano con expectedRevision (docs/api/MEMBERSHIP_ADMIN_FRONTEND_CONTRACTS.md).
function revisionOf(member) {
  const revision = Number(member?.revision);
  if (member?.revision === undefined || member?.revision === null || !Number.isFinite(revision)) {
    throw new Error("Il server non ha restituito la revisione del membro: impossibile modificarlo.");
  }
  return revision;
}

// action: "AddRole" | "RemoveRole" | "Suspend" | "Reactivate"; role solo per AddRole/RemoveRole.
// expectedRevision: revisione del membro appena letto (dettaglio aperto nel dialog); se manca si rilegge
// il membro con GET /institution/admin/members/{id} subito prima della mutazione.
export async function mutateMember(membershipId, { action, role, reason, expectedRevision }, token) {
  const revision = expectedRevision ?? revisionOf(await getMember(membershipId, token));
  const body = { eventId: newEventId(), expectedRevision: revision, action, reason: reason.trim() };
  if (action === "AddRole" || action === "RemoveRole") body.role = role;
  const res = await axiosClient.post(`/institution/admin/members/${membershipId}/mutations`, body, { headers: authHeader(token) });
  return res.data; // { eventId, revision, status, roles }
}
