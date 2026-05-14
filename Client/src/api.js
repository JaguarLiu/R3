const BASE = '/api';

async function req(path, opts = {}) {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...(opts.headers || {}) },
    ...opts,
  });
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
};
