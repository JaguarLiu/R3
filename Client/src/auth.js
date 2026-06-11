const BASE = '/api';
let accessToken = null;
let onLogout = () => {};

// Drop the PWA's network-first cache of GET /api/* responses, so a different
// user logging in on a shared browser can't read the previous user's data.
function clearApiCache() {
  if (typeof caches !== 'undefined') caches.delete('api-cache').catch(() => {});
}

export const tokenStore = {
  get: () => accessToken,
  set: (t) => { accessToken = t; },
  clear: () => { accessToken = null; },
  onLogout: (cb) => { onLogout = cb; },
  fireLogout: () => { accessToken = null; clearApiCache(); onLogout(); },
};

async function authReq(path, body) {
  const res = await fetch(`${BASE}/auth${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: body ? JSON.stringify(body) : undefined,
  });
  const data = res.status === 204 ? null : await res.json().catch(() => null);
  if (!res.ok) throw new Error(data?.error || `HTTP ${res.status}`);
  return data;
}

export const auth = {
  register: (dto) => authReq('/register', dto).then((d) => { accessToken = d.accessToken; return d; }),
  login: (dto) => authReq('/login', dto).then((d) => { accessToken = d.accessToken; return d; }),
  refresh: () => authReq('/refresh').then((d) => { accessToken = d.accessToken; return d; }),
  logout: () => authReq('/logout').finally(() => tokenStore.fireLogout()),
  me: async () => {
    const res = await fetch(`${BASE}/auth/me`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      credentials: 'include',
    });
    return res.ok ? res.json() : null;
  },
  lineLoginUrl: `${BASE}/auth/line/start`,
};
