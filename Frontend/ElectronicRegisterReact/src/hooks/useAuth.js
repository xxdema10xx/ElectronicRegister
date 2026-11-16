// src/hooks/useAuth.js
// Questo file permette a QUALSIASI schermata di sapere "chi è l'utente loggato?" senza
// passarsi i dati a mano di componente in componente. (vedi GUIDA §4.3, useContext)

// createContext crea la "bacheca" condivisa; useContext serve a leggerla.
import { createContext, useContext } from "react";

// La bacheca vera e propria. Parte vuota (null): sarà App.js a riempirla con token, user, logout, updateUser.
// "export" = la rende disponibile agli altri file (App.js la usa per pubblicare i dati).
export const AuthContext = createContext(null);

// Scorciatoia: invece di scrivere useContext(AuthContext) ovunque, le schermate scrivono useAuth().
// Restituisce l'oggetto { token, user, logout, updateUser } pubblicato da App.js.
export function useAuth() {
  return useContext(AuthContext);
}
