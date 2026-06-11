import React, { useState } from 'react';
import { Loader2, LogIn, UserPlus, ArrowLeft } from 'lucide-react';
import { auth } from './auth.js';
import { brutalBorder, brutalShadowLg, brutalShadowSm, brutalBtn } from './brutal.js';

// Neo-Brutalist auth screen (see DESIGN.md). onBack returns to <Landing>.
export default function Login({ onAuthed, onBack }) {
  const [mode, setMode] = useState('login'); // 'login' | 'register'
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const isLogin = mode === 'login';
  const inputCls = `w-full p-4 bg-white text-lg font-black outline-none focus:bg-yellow-200 ${brutalBorder} ${brutalShadowSm}`;

  async function submit(e) {
    e.preventDefault();
    setError(''); setBusy(true);
    try {
      if (mode === 'register') await auth.register({ email, password, displayName });
      else await auth.login({ email, password });
      onAuthed();
    } catch (err) {
      setError(prettyError(err.message));
    } finally {
      setBusy(false);
    }
  }

  function prettyError(msg) {
    const map = {
      invalid_credentials: '帳號或密碼不對啦!',
      email_taken: '這個 Email 有人用過了!',
      weak_password: '密碼太短,至少 8 個字!',
      invalid_email: 'Email 看起來怪怪的...',
      display_name_required: '總得有個名字吧?',
    };
    return map[msg] || msg || '出事啦!';
  }

  return (
    <div className="min-h-screen bg-blue-400 flex items-center justify-center p-4 font-sans text-black selection:bg-pink-400">
      <div className="w-full max-w-md bud-up" style={{ '--bud-rot': '-1deg' }}>
        {onBack && (
          <button onClick={onBack}
            className={`mb-5 bg-white px-4 py-2 font-black text-sm tracking-widest inline-flex items-center gap-2 ${brutalBtn} rotate-1`}>
            <ArrowLeft size={18} strokeWidth={3} /> 回首頁
          </button>
        )}

        <div className={`bg-white ${brutalBorder} ${brutalShadowLg} overflow-hidden`}>
          {/* Header */}
          <div className="bg-yellow-400 p-6 border-b-4 border-black flex items-center gap-4">
            <img src="/r3_icon.png" alt="R3"
              className={`w-16 h-16 bg-white object-contain ${brutalBorder} ${brutalShadowSm} -rotate-3`} />
            <div className="-rotate-1">
              <h1 className="text-3xl font-black tracking-widest uppercase drop-shadow-[2px_2px_0px_white]">
                {isLogin ? '歡迎回來!' : '加入我們!'}
              </h1>
              <p className="text-base font-black bg-white inline-block px-2 border-2 border-black mt-1">R3 記帳</p>
            </div>
          </div>

          {/* Form */}
          <form onSubmit={submit} className="p-6 md:p-8 space-y-5">
            {error && (
              <div className={`bg-red-500 text-white p-3 font-black text-base ${brutalBorder} ${brutalShadowSm} animate-pulse`}>
                {error}
              </div>
            )}

            {mode === 'register' && (
              <div>
                <label className="block text-lg font-black tracking-widest mb-2">你的名字</label>
                <input className={inputCls} placeholder="例如:阿明" value={displayName}
                  onChange={(e) => setDisplayName(e.target.value)} required />
              </div>
            )}

            <div>
              <label className="block text-lg font-black tracking-widest mb-2">Email</label>
              <input className={inputCls} type="email" placeholder="you@example.com" value={email}
                onChange={(e) => setEmail(e.target.value)} required />
            </div>

            <div>
              <label className="block text-lg font-black tracking-widest mb-2">密碼</label>
              <input className={inputCls} type="password" placeholder="至少 8 個字" value={password}
                onChange={(e) => setPassword(e.target.value)} required minLength={8} />
            </div>

            <button type="submit" disabled={busy}
              className={`w-full py-5 text-2xl font-black tracking-widest flex items-center justify-center gap-3 disabled:opacity-50 disabled:bg-gray-400 ${brutalBtn} ${isLogin ? 'bg-green-400' : 'bg-pink-500 text-white'}`}>
              {busy ? <Loader2 className="animate-spin" strokeWidth={3} />
                : isLogin ? <><LogIn strokeWidth={4} /> 登入</>
                : <><UserPlus strokeWidth={4} /> 註冊</>}
            </button>

            {/* Divider */}
            <div className="flex items-center gap-3 py-1">
              <div className="flex-1 border-t-4 border-black border-dashed" />
              <span className="font-black text-sm tracking-widest">或</span>
              <div className="flex-1 border-t-4 border-black border-dashed" />
            </div>

            <a href={auth.lineLoginUrl}
              className={`w-full py-4 text-xl font-black tracking-widest flex items-center justify-center gap-2 text-white bg-[#06C755] ${brutalBtn}`}>
              使用 LINE 登入
            </a>

            <button type="button" onClick={() => { setError(''); setMode(isLogin ? 'register' : 'login'); }}
              className="w-full font-black text-base tracking-widest pt-2 underline decoration-4 underline-offset-4 hover:text-pink-600">
              {isLogin ? '還沒有帳號? 點我註冊!' : '已經有帳號了? 點我登入!'}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
