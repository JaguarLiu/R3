import React from 'react';
import {
  Calculator, Sparkles, CreditCard, Users, Calendar, Scale,
  Download, BrainCircuit, ArrowRight, MessageCircle, LogIn,
} from 'lucide-react';
import { brutalBorder, brutalShadow, brutalShadowLg, brutalShadowSm, brutalBtn } from './brutal.js';

// Neo-Brutalist landing page (see DESIGN.md). Entry point for anonymous visitors;
// the login button hands off to <Login> via onLogin.
export default function Landing({ onLogin }) {
  const reveal = (delay, rot = 0) => ({ animationDelay: `${delay}ms`, '--bud-rot': `${rot}deg` });

  const steps = [
    {
      n: '1', tilt: 'rotate-2', fill: 'bg-green-400', icon: Calendar,
      title: '開一趟旅程', body: 'LINE 打「/旅程 沖繩」開團,或在網頁建立並拉進成員。一個群一個旅程,各玩各的。',
    },
    {
      n: '2', tilt: '-rotate-1', fill: 'bg-cyan-300', icon: Sparkles,
      title: '隨手記一筆', body: '「/記帳 午餐 600 我付 大家平分」直接打字,AI 自動拆成誰付、誰分攤。網頁也能手動。',
    },
    {
      n: '3', tilt: 'rotate-1', fill: 'bg-pink-400', icon: CreditCard,
      title: '一鍵討債', body: '打「/結算」算出最少轉帳次數,誰該給誰多少一目了然。再也不用群組吵架。',
    },
  ];

  const features = [
    { icon: Sparkles, label: 'AI 解析記帳', fill: 'bg-yellow-300' },
    { icon: Scale, label: '最少轉帳', fill: 'bg-green-300' },
    { icon: Users, label: '多人分攤', fill: 'bg-pink-300' },
    { icon: BrainCircuit, label: '阿珊碎碎念', fill: 'bg-cyan-300' },
    { icon: Download, label: 'CSV 匯出', fill: 'bg-orange-300' },
    { icon: Calendar, label: '依天數整理', fill: 'bg-purple-300' },
  ];

  return (
    <div className="min-h-screen bg-blue-400 text-black font-sans overflow-x-hidden pb-16 selection:bg-pink-400 selection:text-black">

      {/* ---- Top bar ---- */}
      <nav className="max-w-6xl mx-auto px-4 md:px-8 pt-5 flex items-center justify-between">
        <div className="flex items-center gap-3 bud-up" style={reveal(0)}>
          <img src="/favicon.png" alt="阿珊"
            className={`w-[4.5rem] h-[4.5rem] bg-white object-cover rounded-full ${brutalBorder} ${brutalShadowSm} bud-wobble`} />
          <span className="text-2xl md:text-3xl font-black tracking-widest drop-shadow-[2px_2px_0px_white]">阿珊</span>
        </div>
        <button onClick={onLogin}
          className={`bg-white px-5 py-3 font-black text-base md:text-lg tracking-widest flex items-center gap-2 ${brutalBtn} bud-up`}
          style={reveal(80)}>
          <LogIn size={20} strokeWidth={3} /> 登入
        </button>
      </nav>

      {/* ---- Hero ---- */}
      <header className="max-w-6xl mx-auto px-4 md:px-8 pt-10 md:pt-16 grid lg:grid-cols-2 gap-10 items-center">
        <div>
          <div className={`inline-block bg-pink-500 text-white px-3 py-1 text-sm font-black tracking-widest uppercase ${brutalBorder} ${brutalShadowSm} bud-up`}
            style={reveal(160, -2)}>
            出遊・聚餐・合租必備
          </div>
          <h1 className="mt-5 text-5xl md:text-7xl font-black uppercase leading-[0.95] tracking-tight bud-up"
            style={reveal(220)}>
            <span className="drop-shadow-[3px_3px_0px_white]">分攤拆帳</span>{' '}
            <span className="bg-yellow-400 px-2 inline-block -rotate-1 border-4 border-black">交給</span><br />
            <span className="bg-black text-yellow-300 px-3 inline-block rotate-1 mt-2">阿珊</span>
          </h1>
          <p className="mt-6 text-xl md:text-2xl font-black leading-relaxed bg-white inline-block px-3 py-2 border-2 border-black bud-up"
            style={reveal(280)}>
            把「誰付了多少、誰要分攤」直接打在 LINE 群裡,<br className="hidden md:block" />
            AI自動拆帳,算出<span className="text-pink-600">最佳</span>方案。
          </p>
          <div className="mt-8 flex flex-wrap gap-4 bud-up" style={reveal(340)}>
            <button onClick={onLogin}
              className={`bg-green-400 px-8 py-5 text-2xl font-black tracking-widest flex items-center gap-3 ${brutalBtn}`}>
              開始記帳 <ArrowRight strokeWidth={4} />
            </button>
            <div className={`bg-white px-5 py-5 text-lg font-black flex items-center gap-2 ${brutalBorder} ${brutalShadow} rotate-1`}>
              <MessageCircle size={22} strokeWidth={3} className="text-green-600" /> 或在 LINE 群 @記帳
            </div>
          </div>
        </div>

        {/* Mascot stage — 阿珊 */}
        <div className="relative flex justify-center bud-up" style={reveal(260)}>
           <div className="relative rotate-2 pt-14 md:pt-16">    
            <img src="/r3_cool.png" alt="阿珊"
              className="w-full object-contain" />

            {/* 頭上的對話泡泡 */}
            <div className={`absolute top-0 left-1/2 -translate-x-1/2 bg-white px-4 py-2 text-[24px] font-black whitespace-nowrap ${brutalBorder} ${brutalShadowSm} -rotate-2`}>
              任誰都逃不過我的眼睛
              <span className="absolute left-1/2 -bottom-2 -translate-x-1/2 w-4 h-4 bg-white border-b-4 border-r-4 border-black rotate-45" />
            </div>
          </div>
        </div>
      </header>

      {/* ---- How to use ---- */}
      <section className="max-w-6xl mx-auto px-4 md:px-8 pt-20 md:pt-28">
        <hr className="border-0 border-t-4 border-dashed border-black mb-14 md:mb-20" />
        <h2 className="text-4xl md:text-5xl font-black uppercase tracking-widest text-center drop-shadow-[2px_2px_0px_white] bud-up" style={reveal(0)}>
          <span className="bg-white px-4 py-1 inline-block -rotate-1 border-4 border-black">怎麼玩?</span>
        </h2>
        <div className="mt-12 grid md:grid-cols-3 gap-8">
          {steps.map((s, i) => (
            <div key={s.n}
              className={`bg-white ${brutalBorder} ${brutalShadowLg} p-7 bud-up`}
              style={reveal(120 + i * 120, i === 0 ? 1 : i === 1 ? -1 : 1)}>
              <div className="flex items-center justify-between">
                <span className={`${s.fill} w-14 h-14 flex items-center justify-center text-3xl font-black ${brutalBorder} ${brutalShadowSm} ${s.tilt}`}>
                  {s.n}
                </span>
                <s.icon size={40} strokeWidth={3} />
              </div>
              <h3 className="mt-5 text-2xl font-black tracking-widest">{s.title}</h3>
              <p className="mt-3 text-lg font-black leading-relaxed text-gray-800">{s.body}</p>
            </div>
          ))}
        </div>
      </section>

      {/* ---- Two doors ---- */}
      <section className="max-w-6xl mx-auto px-4 md:px-8 pt-20 md:pt-28">
        <div className="grid md:grid-cols-2 gap-8">
          <div className={`bg-green-400 ${brutalBorder} ${brutalShadowLg} p-8 bud-up`} style={reveal(0, -1)}>
            <div className="flex items-center gap-3">
              <MessageCircle size={36} strokeWidth={3} />
              <h3 className="text-3xl font-black tracking-widest uppercase">LINE 機器人</h3>
            </div>
            <p className="mt-4 text-lg font-black leading-relaxed">在群組裡用講話的方式記帳,出門在外最快。</p>
            <div className={`mt-5 bg-black text-green-300 p-4 font-black text-base md:text-lg ${brutalShadowSm} space-y-1`}>
              <div>&gt; /旅程 沖繩</div>
              <div>&gt; /記帳 燒肉 3200 阿明付 大家分</div>
              <div>&gt; /結算</div>
            </div>
          </div>
          <div className={`bg-cyan-300 ${brutalBorder} ${brutalShadowLg} p-8 bud-up`} style={reveal(120, 1)}>
            <div className="flex items-center gap-3">
              <Calculator size={36} strokeWidth={3} />
              <h3 className="text-3xl font-black tracking-widest uppercase">網頁介面</h3>
            </div>
            <p className="mt-4 text-lg font-black leading-relaxed">回到電腦慢慢整理:編輯每一筆、過濾、看大看板、匯出 Excel。</p>
            <div className="mt-5 flex flex-wrap gap-3">
              {['手動 / AI 兩種記法', '每人 paid / spent / balance', '直接 & 統一兩種結算'].map((t, i) => (
                <span key={t} className={`bg-white px-3 py-2 font-black ${brutalBorder} ${brutalShadowSm} ${i % 2 ? '-rotate-2' : 'rotate-2'}`}>{t}</span>
              ))}
            </div>
          </div>
        </div>
      </section>

      {/* ---- Feature chips ---- */}
      <section className="max-w-6xl mx-auto px-4 md:px-8 pt-20 md:pt-28">
        <div className="flex flex-wrap justify-center gap-4">
          {features.map((f, i) => (
            <div key={f.label}
              className={`${f.fill} px-5 py-3 text-lg font-black flex items-center gap-2 ${brutalBorder} ${brutalShadow} bud-up ${i % 2 ? '-rotate-2' : 'rotate-2'}`}
              style={reveal(i * 70, i % 2 ? -2 : 2)}>
              <f.icon size={22} strokeWidth={3} /> {f.label}
            </div>
          ))}
        </div>
      </section>

      {/* ---- Bottom CTA ---- */}
      <section className="max-w-4xl mx-auto px-4 md:px-8 pt-20 md:pt-28">
        <div className={`bg-purple-500 ${brutalBorder} ${brutalShadowLg} p-10 md:p-14 text-center -rotate-1`}>
          <h2 className="text-4xl md:text-6xl font-black uppercase tracking-widest text-white drop-shadow-[3px_3px_0px_black]">
            開始分攤吧!
          </h2>
          <p className="mt-4 text-xl font-black text-white">免安裝,登入就能用。讓阿珊幫你算到天下太平。</p>
          <button onClick={onLogin}
            className={`mt-8 bg-yellow-400 px-10 py-6 text-2xl md:text-3xl font-black tracking-widest inline-flex items-center gap-3 ${brutalBtn} rotate-1`}>
            <LogIn strokeWidth={4} /> 登入 / 註冊
          </button>
        </div>
        <p className="text-center mt-10 font-black text-black/70 tracking-widest">
          阿珊是你出遊聚餐必備良藥
        </p>
      </section>
    </div>
  );
}
