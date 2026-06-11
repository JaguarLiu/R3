import React, { useEffect, useState } from 'react';
import ReactDOM from 'react-dom/client';
import App from './app.jsx';
import Login from './Login.jsx';
import Landing from './Landing.jsx';
import { auth, tokenStore } from './auth.js';
import './index.css';

function Root() {
  const [status, setStatus] = useState('checking'); // 'checking' | 'anon' | 'authed'
  const [anonView, setAnonView] = useState('landing'); // 'landing' | 'login'

  useEffect(() => {
    tokenStore.onLogout(() => { setAnonView('landing'); setStatus('anon'); });
    auth.refresh().then(() => setStatus('authed')).catch(() => setStatus('anon'));
  }, []);

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

  return <App onLogout={() => auth.logout()} />;
}

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <Root />
  </React.StrictMode>,
);
