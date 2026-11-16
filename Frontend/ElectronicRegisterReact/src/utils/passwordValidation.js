// src/utils/passwordValidation.js

// Stesse regole del server (PasswordRules del backend): almeno 8 caratteri, al massimo 72 byte UTF-8 e
// almeno uno dei caratteri speciali ammessi. Va richiamata a ogni tasto, così gli errori spariscono non
// appena l'utente corregge il campo.
export const PASSWORD_SPECIALS = "!@#$%^&*()_-+=<>?/[]{}";
const utf8Bytes = (s) => new TextEncoder().encode(s).length;

export function getPasswordErrors(newPwd, confirmPwd, oldPwd) {
  const errors = {};
  if (newPwd) {
    if (newPwd.length < 8) errors.newPassword = "La password deve avere almeno 8 caratteri.";
    else if (utf8Bytes(newPwd) > 72) errors.newPassword = "La password è troppo lunga (al massimo 72 byte: circa 72 lettere senza accenti).";
    else if (![...newPwd].some(c => PASSWORD_SPECIALS.includes(c))) errors.newPassword = `La password deve contenere almeno un carattere speciale tra ${PASSWORD_SPECIALS}`;
    else if (oldPwd && newPwd === oldPwd) errors.newPassword = "La nuova password deve essere diversa da quella attuale.";
  }
  if (confirmPwd && newPwd && confirmPwd !== newPwd) {
    errors.confirmPassword = "Le password non coincidono";
  }
  return errors;
}
