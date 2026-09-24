// src/api/authService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function login(email, password) {
  const res = await axiosClient.post("/Auth/login", { email, password });
  return res.data;
}

export async function register(form) {
  const res = await axiosClient.post("/Auth/register", form);
  return res.data;
}

export async function loginWithMicrosoft(msAccessToken) {
  const res = await axiosClient.post("/Auth/microsoft-login", { accessToken: msAccessToken });
  return res.data;
}

export async function getMe(token) {
  const res = await axiosClient.get("/Auth/me", { headers: authHeader(token) });
  return res.data;
}
