import React, { useEffect, useState } from 'react';
import { Loader2, ArrowLeft } from 'lucide-react';
import { api } from './api.js';
import { brutalBorder, brutalShadowLg, brutalBtn } from './brutal.js';

// 受邀者點 magic link 登入後的認領畫面。挑一個虛擬名字 → 成為成員 → onJoined(tripId)。
export default function JoinClaim({ token, onJoined, onCancel }) {
  const [info, setInfo] = useState(null);   // JoinInfo
  const [status, setStatus] = useState('loading'); // 'loading' | 'pick' | 'error'
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let alive = true;
    api.getJoinInfo(token)
      .then((d) => {
        if (!alive) return;
        if (d.alreadyMember) { onJoined(d.tripId); return; }
        setInfo(d); setStatus('pick');
      })
      .catch(() => { if (alive) { setError('這個連結失效或過期了'); setStatus('error'); } });
    return () => { alive = false; };
  }, [token]);

  async function claim(participantId) {
    setBusy(true); setError('');
    try {
      const r = await api.claimJoin(token, participantId);
      onJoined(r.tripId);
    } catch (e) {
      let msg = '認領失敗，請重試';
      try {
        const j = JSON.parse(e.message);
        if (j.error === 'already_claimed') msg = '這個名字剛被別人選走了，換一個吧！';
        else if (j.error === 'participant_not_found') msg = '找不到這個名字';
      } catch { /* not JSON */ }
      // 名字被搶 → 重抓可認領清單
      try { const fresh = await api.getJoinInfo(token); if (!fresh.alreadyMember) setInfo(fresh); } catch { /* ignore */ }
      setError(msg);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="min-h-screen bg-blue-400 flex items-center justify-center p-4 font-sans text-black">
      <div className={`bg-white w-full max-w-md ${brutalBorder} ${brutalShadowLg} -rotate-1`}>
        <div className="bg-yellow-400 p-6 border-b-4 border-black">
          <h1 className="text-3xl font-black tracking-widest drop-shadow-[2px_2px_0px_white]">
            {status === 'pick' ? `加入「${info.title}」` : 'R3 記帳'}
          </h1>
          {status === 'pick' && <p className="text-base font-black bg-white inline-block px-2 border-2 border-black mt-2">你是哪一位？</p>}
        </div>
        <div className="p-6 space-y-4">
          {status === 'loading' && (
            <div className="flex items-center justify-center py-10 text-2xl font-black">
              <Loader2 className="animate-spin mr-3" strokeWidth={3} /> 載入中...
            </div>
          )}

          {status === 'error' && (
            <>
              <div className={`bg-red-500 text-white p-4 font-black ${brutalBorder}`}>{error}</div>
              <button onClick={onCancel} className={`w-full bg-white py-3 font-black flex items-center justify-center gap-2 ${brutalBtn}`}>
                <ArrowLeft size={18} strokeWidth={3} /> 回首頁
              </button>
            </>
          )}

          {status === 'pick' && (
            <>
              {error && <div className={`bg-red-500 text-white p-3 font-black text-sm ${brutalBorder}`}>{error}</div>}
              {info.claimable.length === 0 ? (
                <div className={`bg-yellow-200 p-5 text-center font-black ${brutalBorder}`}>
                  名字都被選完啦！請帳務主人幫你新增一個。
                </div>
              ) : (
                <div className="grid grid-cols-1 gap-3">
                  {info.claimable.map((c) => (
                    <button key={c.participantId} disabled={busy} onClick={() => claim(c.participantId)}
                      className={`py-4 text-xl font-black bg-cyan-300 disabled:opacity-50 ${brutalBtn}`}>
                      我是 {c.name}
                    </button>
                  ))}
                </div>
              )}
              <button onClick={onCancel} className="w-full font-black text-sm pt-2 underline decoration-4 underline-offset-4">
                先不要，回首頁
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
