// src/api/axiosClient.js
import axios from "axios";
import { API_BASE } from "../config";

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
    const message =
      typeof data === "string" ? data :
      data?.message ? data.message :
      error.message || "Errore di rete";
    return Promise.reject(new Error(message));
  }
);

export function authHeader(token) {
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export default axiosClient;
