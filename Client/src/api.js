import { tokenStore, auth } from './auth.js';

const BASE = '/api';

async function rawReq(path, opts) {
  const token = tokenStore.get();
  return fetch(`${BASE}${path}`, {
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(opts.headers || {}),
    },
    credentials: 'include',
    ...opts,
  });
}

async function req(path, opts = {}) {
  let res = await rawReq(path, opts);
  if (res.status === 401) {
    try { await auth.refresh(); }
    catch { tokenStore.fireLogout(); throw new Error('unauthorized'); }
    res = await rawReq(path, opts);
  }
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `HTTP ${res.status}`);
  }
  if (res.status === 204) return null;
  return res.json();
}

export const api = {
  listTrips: () => req('/trips'),
  getTrip: (id) => req(`/trips/${id}`),
  createTrip: (dto) => req('/trips', { method: 'POST', body: JSON.stringify(dto) }),
  updateTrip: (id, dto) => req(`/trips/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
  deleteTrip: (id) => req(`/trips/${id}`, { method: 'DELETE' }),

  addExpense: (tripId, dto) =>
    req(`/trips/${tripId}/expenses`, { method: 'POST', body: JSON.stringify(dto) }),
  updateExpense: (tripId, id, dto) =>
    req(`/trips/${tripId}/expenses/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
  deleteExpense: (tripId, id) =>
    req(`/trips/${tripId}/expenses/${id}`, { method: 'DELETE' }),

  aiAnalyze: (tripId) => req(`/ai/analyze/${tripId}`, { method: 'POST' }),
  aiParse: (tripId, text) =>
    req(`/ai/parse/${tripId}`, { method: 'POST', body: JSON.stringify({ text }) }),

  addMember: (tripId, email) =>
    req(`/trips/${tripId}/members`, { method: 'POST', body: JSON.stringify({ email }) }),
  removeMember: (tripId, userId) =>
    req(`/trips/${tripId}/members/${userId}`, { method: 'DELETE' }),
};
