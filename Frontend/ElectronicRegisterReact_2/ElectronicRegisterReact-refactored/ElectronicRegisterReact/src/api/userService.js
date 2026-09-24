// src/api/userService.js
import axiosClient, { authHeader } from "./axiosClient";

export async function getUsers(search, token) {
  const path = search ? `/Users/byname/${search}` : "/Users";
  const res = await axiosClient.get(path, { headers: authHeader(token) });
  return res.data;
}

export async function registerUser(form, token) {
  const res = await axiosClient.post("/Auth/RegisterForAdmin", form, { headers: authHeader(token) });
  return res.data;
}

export async function updateUser(id, body, token) {
  const res = await axiosClient.put(`/Users/update/${id}`, body, { headers: authHeader(token) });
  return res.data;
}

export async function updateUserPassword(id, { oldPassword, newPassword }, token) {
  const res = await axiosClient.put(`/Users/updatepassword/${id}`, { oldPassword, newPassword }, { headers: authHeader(token) });
  return res.data;
}

export async function deleteUser(id, token) {
  const res = await axiosClient.delete(`/Users/${id}`, { headers: authHeader(token) });
  return res.data;
}
