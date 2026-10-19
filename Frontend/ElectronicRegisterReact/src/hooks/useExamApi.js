// src/hooks/useExamApi.js
import { useMemo } from "react";
import { useAuth } from "./useAuth";
import { createExamApi } from "../api/examApi";

// API degli esami legata all'utente collegato (vedi src/api/examApi.js)
export function useExamApi() {
  const { token, user } = useAuth();
  return useMemo(() => createExamApi(token, user), [token, user]);
}
