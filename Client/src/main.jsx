import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import App from './app.jsx';
import Login from './Login.jsx';
import Landing from './Landing.jsx';
import JoinClaim from './JoinClaim.jsx';
import { auth, tokenStore } from './auth.js';
import './index.css';

// magic link 帶 ?join=<token>。存進 sessionStorage 以撐過 LINE Login 全頁回跳（/）。
function readJoinToken() {
  const t = new URLSearchParams(window.location.search).get('join');
  if (t) sessionStorage.setItem('pendingJoin', t);
  return sessionStorage.getItem('pendingJoin');
}

function Root() {
  const [status, setStatus] = useState('checking'); // 'checking' | 'anon' | 'authed'
  const [anonView, setAnonView] = useState('landing'); // 'landing' | 'login'
  const [pendingJoin, setPendingJoin] = useState(() => readJoinToken());
  const [initialTripId, setInitialTripId] = useState(null);

  useEffect(() => {
    tokenStore.onLogout(() => { setAnonView('landing'); setStatus('anon'); });
    auth.refresh().then(() => setStatus('authed')).catch(() => setStatus('anon'));
  }, []);

  function finishJoin(tripId) {
    sessionStorage.removeItem('pendingJoin');
    window.history.replaceState({}, '', '/');
    setInitialTripId(tripId ?? null);
    setPendingJoin(null);
  }

  if (status === 'checking') {
    return (
      <div className="min-h-screen bg-blue-400 flex items-center justify-center text-black font-black text-3xl tracking-widest">
        載入中...
      </div>
    );
  }

  if (status === 'anon') {
    return anonView === 'login'
      ? <Login onAuthed={() => setStatus('authed')} onBack={() => setAnonView('landing')} />
      : <Landing onLogin={() => setAnonView('login')} />;
  }

  if (pendingJoin) {
    return <JoinClaim token={pendingJoin} onJoined={finishJoin} onCancel={() => finishJoin(null)} />;
  }

  return <App onLogout={() => auth.logout()} initialTripId={initialTripId} />;
}

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <Root />
  </React.StrictMode>,
);
