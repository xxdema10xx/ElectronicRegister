// src/utils/passwordValidation.js

// Controlla i requisiti di sicurezza della nuova password e la coerenza con
// la conferma e con la password attuale. Va richiamata a ogni tap così gli
// errori spariscono non appena l'utente corregge il campo.
export function getPasswordErrors(newPwd, confirmPwd, oldPwd) {
  const errors = {};
  if (newPwd) {
    const missing = [];
    if (newPwd.length < 8) missing.push("almeno 8 caratteri");
    if (!/[a-z]/.test(newPwd)) missing.push("una lettera minuscola");
    if (!/[A-Z]/.test(newPwd)) missing.push("una lettera maiuscola");
    if (!/[0-9]/.test(newPwd)) missing.push("un numero");
    if (!/[!@#$%^&*()_\-+=<>?/[\]{}]/.test(newPwd)) missing.push("un carattere speciale");
    if (missing.length > 0) {
      errors.newPassword = `La password deve contenere ${missing.join(", ")}`;
    } else if (oldPwd && newPwd === oldPwd) {
      errors.newPassword = "La nuova password deve essere diversa da quella attuale";
    }
  }
  if (confirmPwd && newPwd && confirmPwd !== newPwd) {
    errors.confirmPassword = "Le password non coincidono";
  }
  return errors;
}
