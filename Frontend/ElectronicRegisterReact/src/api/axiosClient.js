// src/api/axiosClient.js
import axios from "axios";
import { API_BASE } from "../appConfig";

const axiosClient = axios.create({
  baseURL: API_BASE,
  headers: { "Content-Type": "application/json" },
});

// Normalizza gli errori di rete/HTTP in un semplice Error con un messaggio
// leggibile, così tutte le schermate possono continuare a fare
// `Alert.alert("Errore", e.message)` esattamente come prima.
axiosClient.interceptors.response.use(
  (res) => res,
  (error) => {
    const data = error.response?.data;
    const status = error.response?.status;
    // ProblemDetails del backend: il testo utile è in "detail" (i 404 restano "status code 404", come si aspettano le schermate)
    const problem = data && typeof data === "object" ? (data.detail || data.title || null) : null;
    const message =
      (typeof data === "string" && data) ? data :
      data?.message ? data.message :
      (problem && status !== 404) ? problem :
      error.message || "Errore di rete";
    return Promise.reject(Object.assign(new Error(message), { status, problem }));
  }
);

export function authHeader(token) {
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export default axiosClient;
