import React, { useState, useMemo, useEffect } from 'react';
import {
  Plus, Trash2, Calculator, CreditCard, Users,
  DollarSign, Calendar, Settings2,
  CheckCircle2, Circle, Edit3, Download,
  ChevronDown, ChevronUp, Sparkles, BrainCircuit, Loader2, Scale, Filter,
  ArrowLeft, FolderOpen,
} from 'lucide-react';
import { api } from './api.js';
import { brutalBorder, brutalShadowLg, brutalBtn } from './brutal.js';

const App = ({ onLogout }) => {
  // --- 1. State ---
  const [tripId, setTripId] = useState(null);
  const [view, setView] = useState('lobby');   // 'lobby' | 'setup' | 'workspace'
  const [trips, setTrips] = useState([]);
  const [loading, setLoading] = useState(true);
  const [editingId, setEditingId] = useState(null);
  const [settlementMode, setSettlementMode] = useState('direct');
  const [isAiLoading, setIsAiLoading] = useState(false);
  const [aiAnalysis, setAiAnalysis] = useState('');
  const [aiInputText, setAiInputText] = useState('');
  const [aiFeedback, setAiFeedback] = useState({ message: '', type: '' });

  const [filterPayer, setFilterPerson] = useState('all');
  const [filterDay, setFilterDay] = useState('all');

  const [collapsed, setCollapsed] = useState({ form: false, settlement: false, list: false, dashboard: false });

  const [tripConfig, setTripConfig] = useState({ title: '我的混亂記帳', days: 3 });
  const [participants, setParticipants] = useState(['小明', '阿花', '胖虎', '美美']);
  const [newParticipant, setNewParticipant] = useState('');
  const [expenses, setExpenses] = useState([]);

  const [newExpense, setNewExpense] = useState({
    day: '第 1 天', item: '', total: '',
    singlePayer: '小明', isMultiPayer: false, multiPayers: {},
    isCustomSplit: false, customSplits: {},
    selectedForSplit: ['小明', '阿花', '胖虎', '美美'],
  });

  // --- 2. Load trip list on mount (lobby is the landing) ---
  useEffect(() => { refreshTrips(); }, []);

  async function refreshTrips() {
    setLoading(true);
    try {
      setTrips(await api.listTrips());
    } catch (e) {
      setAiFeedback({ message: `載入列表失敗：${e.message}`, type: 'error' });
      setTrips([]);
    } finally {
      setLoading(false);
    }
  }

  function normalizeExpense(e) {
    return {
      id: e.id,
      day: e.day,
      item: e.item,
      total: Number(e.total),
      payers: e.payers || {},
      splits: e.splits || {},
    };
  }

  // --- 3. Smart logic ---
  const toggleCollapse = (k) => setCollapsed(p => ({ ...p, [k]: !p[k] }));

  const toggleSplitParticipant = (name) => {
    setNewExpense(prev => {
      const cur = prev.selectedForSplit || [];
      const next = cur.includes(name) ? cur.filter(n => n !== name) : [...cur, name];
      return { ...prev, selectedForSplit: next };
    });
  };

  useEffect(() => {
    let sum = 0;
    if (newExpense.isCustomSplit) {
      sum = Object.values(newExpense.customSplits || {}).reduce((a, b) => a + (parseFloat(b) || 0), 0);
    } else if (newExpense.isMultiPayer) {
      sum = Object.values(newExpense.multiPayers || {}).reduce((a, b) => a + (parseFloat(b) || 0), 0);
    }
    if (newExpense.isCustomSplit || newExpense.isMultiPayer) {
      const sumStr = sum === 0 ? '' : String(sum);
      if (sumStr !== newExpense.total) setNewExpense(prev => ({ ...prev, total: sumStr }));
    }
  }, [newExpense.multiPayers, newExpense.customSplits, newExpense.isMultiPayer, newExpense.isCustomSplit]);

  useEffect(() => {
    if (participants.length > 0 && !editingId) {
      const safe = (newExpense.selectedForSplit || []).filter(n => participants.includes(n));
      setNewExpense(prev => ({
        ...prev,
        singlePayer: participants.includes(prev.singlePayer) ? prev.singlePayer : (participants[0] || ''),
        selectedForSplit: safe.length > 0 ? safe : [...participants],
        day: prev.day || '第 1 天',
      }));
    }
  }, [participants, editingId, view]);

  const isImbalanced = useMemo(() => {
    if (newExpense.isMultiPayer && newExpense.isCustomSplit) {
      const p = Object.values(newExpense.multiPayers || {}).reduce((a, b) => a + (parseFloat(b) || 0), 0);
      const s = Object.values(newExpense.customSplits || {}).reduce((a, b) => a + (parseFloat(b) || 0), 0);
      return Math.abs(p - s) > 0.5;
    }
    return false;
  }, [newExpense]);

  // --- 4. Derived ---
  const summary = useMemo(() => {
    const stats = {};
    participants.forEach(p => stats[p] = { spent: 0, paid: 0 });
    expenses.forEach(exp => {
      Object.entries(exp.payers || {}).forEach(([n, a]) => { if (stats[n]) stats[n].paid += Number(a) || 0; });
      Object.entries(exp.splits || {}).forEach(([n, a]) => { if (stats[n]) stats[n].spent += Number(a) || 0; });
    });
    return stats;
  }, [expenses, participants]);

  const filteredExpenses = useMemo(() => {
    let list = expenses;
    if (filterPayer !== 'all') list = list.filter(e => Object.keys(e.payers || {}).includes(filterPayer));
    if (filterDay !== 'all') list = list.filter(e => String(e.day) === String(filterDay));
    return list;
  }, [expenses, filterPayer, filterDay]);

  const filterStats = useMemo(() => {
    const totalSpent = filteredExpenses.reduce((s, e) => s + (Number(e.total) || 0), 0);
    return { totalSpent };
  }, [filteredExpenses]);

  const settlementsResult = useMemo(() => {
    if (participants.length === 0) return { list: [], treasurer: null };
    const balances = participants.map(p => ({ name: p, amount: (summary[p]?.paid || 0) - (summary[p]?.spent || 0) }));
    const list = [];
    if (settlementMode === 'direct') {
      let cr = balances.filter(b => b.amount > 0).sort((a, b) => b.amount - a.amount).map(b => ({ ...b }));
      let dr = balances.filter(b => b.amount < 0).map(b => ({ ...b, amount: Math.abs(b.amount) })).sort((a, b) => b.amount - a.amount);
      let i = 0, j = 0;
      while (i < dr.length && j < cr.length) {
        const pay = Math.min(dr[i].amount, cr[j].amount);
        if (pay > 0.5) list.push({ from: dr[i].name, to: cr[j].name, amount: Math.round(pay) });
        dr[i].amount -= pay; cr[j].amount -= pay;
        if (dr[i].amount <= 0.1) i++; if (cr[j].amount <= 0.1) j++;
      }
      return { list, treasurer: null };
    } else {
      let treasurer = participants[0]; let maxP = -1;
      participants.forEach(p => { if ((summary[p]?.paid || 0) > maxP) { maxP = summary[p].paid; treasurer = p; } });
      balances.forEach(b => {
        if (b.name === treasurer) return;
        const amt = Math.round(b.amount);
        if (amt < 0) list.push({ from: b.name, to: treasurer, amount: Math.abs(amt) });
        else if (amt > 0) list.push({ from: treasurer, to: b.name, amount: amt });
      });
      return { list, treasurer };
    }
  }, [summary, participants, settlementMode]);

  // --- 5. Helpers ---
  function buildSplits(total, isCustom, customSplits, selected) {
    const splits = {};
    if (isCustom) {
      Object.entries(customSplits || {}).forEach(([n, v]) => splits[n] = Number(v) || 0);
      participants.forEach(p => splits[p] = splits[p] || 0);
    } else {
      const sel = (selected && selected.length) ? selected : participants;
      const avg = Math.floor(total / (sel.length || 1));
      const rem = total % (sel.length || 1);
      participants.forEach(p => splits[p] = sel.includes(p) ? avg + (sel.indexOf(p) < rem ? 1 : 0) : 0);
    }
    return splits;
  }

  // --- 6. Actions ---
  function startNewTrip() {
    setTripId(null);
    setTripConfig({ title: '我的混亂記帳', days: 3 });
    setParticipants(['小明', '阿花', '胖虎', '美美']);
    setExpenses([]);
    setEditingId(null);
    setAiFeedback({ message: '', type: '' });
    setView('setup');
  }

  async function openTrip(id) {
    setLoading(true);
    try {
      const trip = await api.getTrip(id);
      setTripId(trip.id);
      setTripConfig({ title: trip.title, days: trip.days });
      setParticipants(trip.participants.map(p => p.name));
      setExpenses((trip.expenses || []).map(normalizeExpense));
      setEditingId(null);
      setAiFeedback({ message: '', type: '' });
      setView('workspace');
    } catch (e) {
      setAiFeedback({ message: `打開失敗：${e.message}`, type: 'error' });
    } finally {
      setLoading(false);
    }
  }

  async function handleDeleteTrip(id, ev) {
    ev.stopPropagation();
    if (!window.confirm('確定刪掉這個帳務？刪了就回不來囉！')) return;
    try {
      await api.deleteTrip(id);
      setTrips(prev => prev.filter(t => t.id !== id));
    } catch (e) {
      setAiFeedback({ message: `刪除失敗：${e.message}`, type: 'error' });
    }
  }

  async function backToLobby() {
    setView('lobby');
    await refreshTrips();
  }

  async function handleCompleteSetup() {
    if (participants.length === 0) return;
    try {
      if (tripId) {
        await api.updateTrip(tripId, { title: tripConfig.title, days: tripConfig.days, participants });
      } else {
        const created = await api.createTrip({ title: tripConfig.title, days: tripConfig.days, participants });
        setTripId(created.id);
      }
      setView('workspace');
    } catch (e) {
      setAiFeedback({ message: `儲存失敗：${e.message}`, type: 'error' });
    }
  }

  async function handleSave() {
    const totalNum = parseFloat(newExpense.total);
    if (!newExpense.item || isNaN(totalNum) || isImbalanced) return;

    const splits = buildSplits(totalNum, newExpense.isCustomSplit, newExpense.customSplits, newExpense.selectedForSplit);
    const payers = newExpense.isMultiPayer
      ? Object.fromEntries(Object.entries(newExpense.multiPayers).map(([k, v]) => [k, Number(v) || 0]))
      : { [newExpense.singlePayer || participants[0]]: totalNum };

    const dto = { day: newExpense.day, item: newExpense.item, total: totalNum, payers, splits };
    try {
      if (editingId) {
        const updated = await api.updateExpense(tripId, editingId, dto);
        setExpenses(prev => prev.map(e => e.id === editingId ? normalizeExpense(updated) : e));
      } else {
        const created = await api.addExpense(tripId, dto);
        setExpenses(prev => [...prev, normalizeExpense(created)]);
      }
      setEditingId(null);
      setCollapsed(prev => ({ ...prev, form: true, list: false }));
      setNewExpense(prev => ({ ...prev, item: '', total: '', multiPayers: {}, customSplits: {}, selectedForSplit: [...participants] }));
      setAiFeedback({ message: '', type: '' });
    } catch (e) {
      setAiFeedback({ message: `儲存失敗：${e.message}`, type: 'error' });
    }
  }

  async function handleDelete(id) {
    try {
      await api.deleteExpense(tripId, id);
      setExpenses(prev => prev.filter(x => x.id !== id));
    } catch (e) {
      setAiFeedback({ message: `刪除失敗：${e.message}`, type: 'error' });
    }
  }

  async function handleAiAnalysis() {
    if (expenses.length === 0) return;
    setIsAiLoading(true);
    try {
      const { text } = await api.aiAnalyze(tripId);
      setAiAnalysis(text || '分析中...大腦過熱啦！');
    } catch {
      setAiAnalysis('糟糕！機器人被香蕉皮滑倒了。');
    } finally {
      setIsAiLoading(false);
    }
  }

  async function handleAiBatchParse() {
    if (!aiInputText.trim()) return;
    setIsAiLoading(true); setAiFeedback({ message: '', type: '' });
    try {
      const created = await api.aiParse(tripId, aiInputText);
      setExpenses(prev => [...prev, ...created.map(normalizeExpense)]);
      setAiInputText('');
      setAiFeedback({ message: `塞入了 ${created.length} 筆！`, type: 'success' });
      setCollapsed(prev => ({ ...prev, list: false }));
    } catch (e) {
      // Backend may return JSON like {"error":"unknown_names","names":[...]}
      let msg = e.message || '解析失敗啦';
      try {
        const j = JSON.parse(msg);
        if (j.error === 'unknown_names') msg = `找不到這些傢伙：${(j.names || []).join(', ')}`;
        else if (j.error === 'no_expenses_parsed') msg = '看不懂你在寫什麼啦！';
        else if (j.message) msg = j.message;
      } catch { /* not JSON */ }
      setAiFeedback({ message: msg, type: 'error' });
    } finally {
      setIsAiLoading(false);
    }
  }

  function handleExport() {
    let csv = '﻿';
    const header = ['項目', '', '總計', '支付'];
    participants.forEach(p => header.push(p));
    csv += header.join(',') + '\n';
    const days = [...new Set(expenses.map(e => String(e.day)))].sort((a, b) =>
      (parseInt(a.replace(/[^0-9]/g, '')) || 0) - (parseInt(b.replace(/[^0-9]/g, '')) || 0));
    days.forEach(day => {
      csv += `${day},,,,\n`;
      expenses.filter(e => String(e.day) === day).forEach(e => {
        const row = [`"${e.item}"`, '', e.total, Object.keys(e.payers || {}).join('+')];
        participants.forEach(p => row.push(e.splits?.[p] || 0));
        csv += row.join(',') + '\n';
      });
      csv += ',,,,\n';
    });
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url; link.download = `${tripConfig.title}_結算報表.csv`; link.click();
  }

  function startEdit(exp) {
    setEditingId(exp.id); setCollapsed(prev => ({ ...prev, form: false }));
    setNewExpense({
      day: String(exp.day), item: exp.item, total: String(exp.total),
      singlePayer: Object.keys(exp.payers || {})[0] || participants[0],
      isMultiPayer: Object.keys(exp.payers || {}).length > 1,
      multiPayers: { ...(exp.payers || {}) },
      isCustomSplit: false,
      customSplits: { ...(exp.splits || {}) },
      selectedForSplit: Object.entries(exp.splits || {}).filter(([_, v]) => Number(v) > 0).map(([k]) => k),
    });
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  // --- 7. Render --- (brutal* tokens imported from ./brutal.js)
  if (loading) {
    return (
      <div className="min-h-screen bg-blue-400 flex items-center justify-center text-black font-black text-3xl">
        <Loader2 className="animate-spin mr-3" size={40} strokeWidth={3} /> 載入中...
      </div>
    );
  }

  if (view === 'lobby') {
    return (
      <div className="min-h-screen bg-blue-400 p-4 md:p-8 font-sans text-black overflow-x-hidden pb-20">
        <div className="max-w-5xl mx-auto space-y-8">
          <header className={`flex items-center justify-between gap-4 bg-yellow-400 p-6 ${brutalBorder} ${brutalShadowLg} rotate-1`}>
            <h1 className="text-3xl md:text-4xl font-black uppercase tracking-widest drop-shadow-[2px_2px_0px_white]">我的帳務</h1>
            <button onClick={onLogout} className="text-sm text-slate-600 underline font-black">登出</button>
          </header>

          {aiFeedback.message && aiFeedback.type === 'error' && (
            <div className={`p-4 bg-red-400 text-white font-black ${brutalBorder} shadow-[4px_4px_0px_0px_black]`}>{aiFeedback.message}</div>
          )}

          <button onClick={startNewTrip}
            className={`w-full bg-green-400 py-6 text-2xl font-black tracking-widest flex items-center justify-center gap-3 ${brutalBtn}`}>
            <Plus strokeWidth={4} size={32} /> 開新帳務
          </button>

          {trips.length === 0 ? (
            <div className={`bg-white p-10 text-center ${brutalBorder} ${brutalShadowLg} -rotate-1`}>
              <FolderOpen size={56} strokeWidth={3} className="mx-auto mb-4" />
              <p className="text-2xl font-black tracking-widest">還沒有任何帳務，開一個來敗家吧！</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
              {trips.map((t, idx) => (
                <div key={t.id} onClick={() => openTrip(t.id)}
                  className={`relative bg-white p-6 cursor-pointer ${brutalBorder} ${brutalShadowLg} ${idx % 2 ? '-rotate-1' : 'rotate-1'} hover:bg-cyan-100 transition-all`}>
                  {t.isOwner && (
                    <button onClick={(ev) => handleDeleteTrip(t.id, ev)}
                      className={`absolute -top-3 -right-3 p-2 bg-red-500 text-white ${brutalBtn}`} aria-label="刪除帳務">
                      <Trash2 size={20} strokeWidth={3} />
                    </button>
                  )}
                  <h2 className="text-2xl font-black tracking-widest break-words pr-6">{t.title}</h2>
                  <div className="mt-4 flex flex-wrap gap-2">
                    <span className="text-sm font-black bg-yellow-300 px-2 border-2 border-black -rotate-2">{t.days} 天</span>
                    <span className="text-sm font-black bg-pink-300 px-2 border-2 border-black rotate-2">{new Date(t.createdAt).toLocaleDateString()}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    );
  }

  if (view === 'setup') {
    return (
      <div className="min-h-screen bg-blue-400 flex items-center justify-center p-6 font-sans text-black">
        <div className={`bg-white w-full max-w-lg rounded-2xl ${brutalBorder} ${brutalShadowLg} overflow-hidden -rotate-1`}>
          <div className="bg-yellow-400 p-8 border-b-4 border-black text-center relative">
            <button onClick={() => (tripId ? setView('workspace') : backToLobby())}
              className={`absolute left-4 top-4 bg-white p-2 ${brutalBtn}`} aria-label="返回">
              <ArrowLeft size={24} strokeWidth={3} />
            </button>
            <h1 className="text-4xl font-black flex items-center justify-center gap-3 rotate-2 tracking-widest">
              <Calendar size={36} strokeWidth={3} /> {tripId ? '改設定!!' : '初始設定!!'}
            </h1>
          </div>
          <div className="p-8 space-y-8">
            <div className="space-y-4">
              <label className="block text-xl font-black tracking-widest">旅程名稱：</label>
              <input type="text" className={`w-full p-4 bg-white text-xl font-black focus:outline-none focus:bg-pink-200 ${brutalBorder} shadow-[4px_4px_0px_0px_rgba(0,0,0,1)]`}
                value={tripConfig.title} onChange={e => setTripConfig({ ...tripConfig, title: e.target.value })} />
              <label className="block text-xl font-black tracking-widest pt-4">玩幾天？ ({tripConfig.days} 天)</label>
              <input type="range" min="1" max="14" className="w-full h-4 bg-black appearance-none rounded-full outline-none cursor-pointer"
                value={tripConfig.days} onChange={e => setTripConfig({ ...tripConfig, days: parseInt(e.target.value) })} />
            </div>
            <div className="pt-6 border-t-4 border-black border-dashed">
              <label className="block text-xl font-black mb-4 tracking-widest">都有誰去？ ({participants.length})</label>
              <div className="flex gap-2 mb-4">
                <input type="text" className={`flex-1 p-3 bg-white text-lg font-black outline-none focus:bg-cyan-200 ${brutalBorder} shadow-[4px_4px_0px_0px_rgba(0,0,0,1)]`}
                  placeholder="輸入名字..." value={newParticipant} onChange={e => setNewParticipant(e.target.value)}
                  onKeyDown={e => { if (e.key === 'Enter' && newParticipant.trim()) { setParticipants([...participants, newParticipant.trim()]); setNewParticipant(''); } }} />
                <button onClick={() => { if (newParticipant.trim()) { setParticipants([...participants, newParticipant.trim()]); setNewParticipant(''); } }}
                  className={`bg-green-400 px-6 font-black text-2xl ${brutalBtn}`}><Plus strokeWidth={4} /></button>
              </div>
              <div className="flex flex-wrap gap-3 max-h-40 overflow-y-auto overflow-x-hidden p-2">
                {participants.map((p, idx) => (
                  <div key={p} className={`bg-pink-400 px-4 py-2 text-xl font-black flex items-center gap-2 ${brutalBorder} shadow-[3px_3px_0px_0px_rgba(0,0,0,1)] ${idx % 2 ? '-rotate-2' : 'rotate-2'}`}>
                    {p} <Trash2 size={20} strokeWidth={3} className="cursor-pointer hover:text-white" onClick={() => setParticipants(prev => prev.filter(x => x !== p))} />
                  </div>
                ))}
              </div>
            </div>
            <button onClick={handleCompleteSetup} disabled={participants.length === 0}
              className={`w-full bg-blue-500 text-white py-5 text-2xl tracking-widest font-black disabled:opacity-50 disabled:bg-gray-400 ${brutalBtn} mt-4`}>
              出發啦！
            </button>
          </div>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-blue-400 p-3 md:p-8 font-sans text-black overflow-x-hidden pb-20">
      <div className="max-w-6xl mx-auto space-y-6 md:space-y-10">

        <header className={`flex flex-col md:flex-row items-center justify-between gap-4 bg-yellow-400 p-6 md:p-8 ${brutalBorder} ${brutalShadowLg} rotate-1`}>
          <div className="flex flex-col sm:flex-row items-center gap-5">
            <div className={`bg-pink-500 p-4 ${brutalBorder} shadow-[4px_4px_0px_0px_black] sm:-rotate-6`}><Calculator size={32} strokeWidth={3} className="text-white" /></div>
            <div className="sm:-rotate-1 text-center sm:text-left">
              <h1 className="text-3xl md:text-5xl font-black uppercase tracking-widest drop-shadow-[2px_2px_0px_white]">{tripConfig.title}</h1>
              <p className="text-lg font-black bg-white inline-block px-2 border-2 border-black mt-2">
                <Calendar size={16} strokeWidth={3} className="inline mr-1 -mt-1" /> {tripConfig.days} 天 • {participants.length} 人混戰
              </p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <button onClick={backToLobby} className={`bg-white font-black text-sm flex items-center gap-2 px-4 py-3 ${brutalBtn} hover:bg-gray-200 -rotate-2`}>
              <ArrowLeft size={20} strokeWidth={3} /> 返回列表
            </button>
            <button onClick={() => setView('setup')} className={`bg-white font-black text-sm flex items-center gap-2 px-4 py-3 ${brutalBtn} hover:bg-gray-200 rotate-2`}>
              <Settings2 size={20} strokeWidth={3} /> 設定
            </button>
            <button onClick={onLogout} className="text-sm text-slate-500 underline">登出</button>
          </div>
        </header>

        <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 items-start">
          <div className="lg:col-span-4 space-y-6">
            <div className={`bg-white ${brutalBorder} ${brutalShadowLg} ${editingId ? 'bg-yellow-200' : ''}`}>
              <button onClick={() => toggleCollapse('form')} className="w-full p-6 flex items-center justify-between bg-green-400 border-b-4 border-black">
                <h2 className="text-2xl font-black flex items-center gap-2 tracking-widest">
                  {editingId ? <Edit3 strokeWidth={3} /> : <Plus strokeWidth={4} />} {editingId ? '修改這筆！' : '買了什麼？'}
                </h2>
                {collapsed.form ? <ChevronDown size={28} strokeWidth={3} /> : <ChevronUp size={28} strokeWidth={3} />}
              </button>
              {!collapsed.form && (
                <div className="p-6 space-y-6">
                  {!editingId && (
                    <div className={`bg-cyan-300 p-5 ${brutalBorder} shadow-[4px_4px_0px_0px_black] rotate-1 space-y-3`}>
                      <div className="flex justify-between font-black uppercase tracking-widest text-sm">
                        <span className="flex items-center gap-2"><Sparkles size={18} strokeWidth={3} /> AI 魔法輸入框</span>
                      </div>
                      <textarea className={`w-full p-3 ${brutalBorder} bg-white text-lg font-black outline-none h-24 focus:bg-yellow-100`}
                        placeholder="阿花買香蕉 100元..." value={aiInputText} onChange={e => setAiInputText(e.target.value)} />
                      {aiFeedback.message && <div className={`p-3 font-black text-sm ${brutalBorder} ${aiFeedback.type === 'error' ? 'bg-red-400 text-white' : 'bg-green-400'}`}>{aiFeedback.message}</div>}
                      <button onClick={handleAiBatchParse} disabled={isAiLoading || !aiInputText.trim()} className={`w-full bg-pink-500 text-white py-3 text-lg font-black disabled:opacity-50 ${brutalBtn}`}>
                        {isAiLoading ? <Loader2 className="animate-spin mx-auto" /> : '施法解析 ✨'}
                      </button>
                    </div>
                  )}

                  <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                    <div>
                      <label className="text-lg font-black block mb-2 tracking-widest">哪一天？</label>
                      <select className={`w-full p-3 ${brutalBorder} bg-white font-black text-lg outline-none focus:bg-yellow-200 shadow-[2px_2px_0px_0px_black] cursor-pointer`}
                        value={newExpense.day} onChange={e => setNewExpense({ ...newExpense, day: e.target.value })}>
                        {Array.from({ length: tripConfig.days }, (_, i) => { const d = `第 ${i + 1} 天`; return <option key={i} value={d}>{d}</option>; })}
                        <option value="其他">其他</option>
                      </select>
                    </div>
                    <div>
                      <label className="text-lg font-black block mb-2 tracking-widest">多少錢？</label>
                      <input type="number" className={`w-full p-3 ${brutalBorder} font-black text-lg outline-none shadow-[2px_2px_0px_0px_black] ${(newExpense.isMultiPayer || newExpense.isCustomSplit) ? 'bg-gray-300 text-gray-600' : 'bg-white focus:bg-yellow-200'}`}
                        placeholder="$$$" readOnly={newExpense.isMultiPayer || newExpense.isCustomSplit} value={newExpense.total}
                        onChange={e => setNewExpense({ ...newExpense, total: e.target.value })} />
                    </div>
                  </div>

                  <div>
                    <label className="text-lg font-black block mb-2 tracking-widest">買啥？</label>
                    <input type="text" className={`w-full p-4 ${brutalBorder} bg-white text-xl font-black outline-none focus:bg-yellow-200 shadow-[4px_4px_0px_0px_black]`}
                      placeholder="香蕉？大便？" value={newExpense.item} onChange={e => setNewExpense({ ...newExpense, item: e.target.value })} />
                  </div>

                  <div className={`p-4 border-4 border-black border-dashed ${newExpense.isMultiPayer ? 'bg-pink-100' : 'bg-white'}`}>
                    <div className="flex justify-between items-center mb-4">
                      <span className="text-xl font-black bg-yellow-300 px-2 border-2 border-black rotate-2">誰付錢？</span>
                      <button onClick={() => setNewExpense({ ...newExpense, isMultiPayer: !newExpense.isMultiPayer })} className={`px-3 py-1 font-black ${brutalBtn} ${newExpense.isMultiPayer ? 'bg-pink-400' : 'bg-white'}`}>
                        {newExpense.isMultiPayer ? '多人一起' : '單一土豪'}
                      </button>
                    </div>
                    {!newExpense.isMultiPayer ? (
                      <select className={`w-full p-3 ${brutalBorder} bg-white font-black text-lg outline-none shadow-[2px_2px_0px_0px_black]`}
                        value={newExpense.singlePayer || participants[0] || ''} onChange={e => setNewExpense({ ...newExpense, singlePayer: e.target.value })}>
                        {participants.map(p => <option key={p} value={p}>{p}</option>)}
                      </select>
                    ) : (
                      <div className="space-y-3">{participants.map(p => (
                        <div key={p} className="flex justify-between items-center font-black text-lg">
                          <span>{p}</span>
                          <input type="number" className={`w-24 p-2 ${brutalBorder} text-right font-black shadow-[2px_2px_0px_0px_black] outline-none`}
                            placeholder="0" value={newExpense.multiPayers[p] || ''} onChange={e => setNewExpense({ ...newExpense, multiPayers: { ...newExpense.multiPayers, [p]: e.target.value } })} />
                        </div>
                      ))}</div>
                    )}
                  </div>

                  <div className={`p-4 border-4 border-black border-dashed ${newExpense.isCustomSplit ? 'bg-orange-100' : 'bg-white'}`}>
                    <div className="flex flex-col sm:flex-row sm:justify-between items-start sm:items-center mb-4 gap-3">
                      <span className="text-xl font-black bg-cyan-300 px-2 border-2 border-black -rotate-2">幫誰付？</span>
                      <button onClick={() => setNewExpense({ ...newExpense, isCustomSplit: !newExpense.isCustomSplit })}
                        className={`px-3 py-1 font-black w-full sm:w-auto ${brutalBtn} ${newExpense.isCustomSplit ? 'bg-orange-400' : 'bg-white'}`}>
                        {newExpense.isCustomSplit ? '自己填' : '平分啦'}
                      </button>
                    </div>
                    {!newExpense.isCustomSplit ? (
                      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">{participants.map(p => {
                        const isSel = (newExpense.selectedForSplit || []).includes(p);
                        return <button key={p} onClick={() => toggleSplitParticipant(p)}
                          className={`p-3 font-black text-lg ${brutalBtn} flex items-center justify-center gap-2 ${isSel ? 'bg-blue-500 text-white' : 'bg-white'}`}>
                          {isSel ? <CheckCircle2 strokeWidth={3} /> : <Circle strokeWidth={3} />} {p}
                        </button>;
                      })}</div>
                    ) : (
                      <div className="space-y-3">{participants.map(p => (
                        <div key={p} className="flex justify-between items-center font-black text-lg">
                          <span>{p}</span>
                          <input type="number" className={`w-24 p-2 ${brutalBorder} text-right font-black shadow-[2px_2px_0px_0px_black] outline-none`}
                            placeholder="0" value={newExpense.customSplits[p] || ''} onChange={e => setNewExpense({ ...newExpense, customSplits: { ...newExpense.customSplits, [p]: e.target.value } })} />
                        </div>
                      ))}</div>
                    )}
                  </div>

                  {isImbalanced && (
                    <div className={`p-4 bg-red-500 text-white text-lg font-black flex items-center gap-3 ${brutalBorder} shadow-[4px_4px_0px_0px_black] animate-pulse`}>
                      <Scale strokeWidth={3} /> 錢對不上啦！算錯了！
                    </div>
                  )}

                  <button onClick={handleSave} disabled={!newExpense.item || !newExpense.total || isImbalanced}
                    className={`w-full py-5 text-2xl font-black disabled:opacity-40 disabled:bg-gray-400 tracking-widest ${brutalBtn} ${editingId ? 'bg-yellow-400' : 'bg-green-400'}`}>
                    {editingId ? '改好了！' : '記下來！'}
                  </button>
                  {editingId && <button onClick={() => { setEditingId(null); setNewExpense({ ...newExpense, item: '', total: '' }); }}
                    className="w-full font-black text-lg py-2 mt-2 underline decoration-4 underline-offset-4">不改了</button>}
                </div>
              )}
            </div>
          </div>

          <div className="lg:col-span-8 space-y-6">
            <div className={`bg-white ${brutalBorder} ${brutalShadowLg} overflow-hidden`}>
              <div className="p-6 bg-blue-500 border-b-4 border-black flex flex-col sm:flex-row justify-between gap-4">
                <h2 className="font-black text-2xl text-white flex items-center gap-3 drop-shadow-[2px_2px_0px_black] tracking-widest">
                  <Users size={28} strokeWidth={3} /> 花錢流水帳 ({expenses.length})
                </h2>
                <div className="flex flex-wrap items-center gap-3">
                  <div className={`flex items-center gap-2 bg-yellow-300 px-3 py-2 ${brutalBorder} shadow-[2px_2px_0px_0px_black]`}>
                    <Calendar size={18} strokeWidth={3} />
                    <select className="bg-transparent font-black outline-none cursor-pointer" value={filterDay} onChange={e => setFilterDay(e.target.value)}>
                      <option value="all">全天</option>
                      {Array.from({ length: tripConfig.days }, (_, i) => <option key={i} value={`第 ${i + 1} 天`}>{`第 ${i + 1} 天`}</option>)}
                      <option value="其他">其他</option>
                    </select>
                  </div>
                  <div className={`flex items-center gap-2 bg-pink-300 px-3 py-2 ${brutalBorder} shadow-[2px_2px_0px_0px_black]`}>
                    <Filter size={18} strokeWidth={3} />
                    <select className="bg-transparent font-black outline-none cursor-pointer" value={filterPayer} onChange={e => setFilterPerson(e.target.value)}>
                      <option value="all">全體</option>
                      {participants.map(p => <option key={p} value={p}>{p}</option>)}
                    </select>
                  </div>
                </div>
              </div>
              {!collapsed.list && (
                <div>
                  {(filterPayer !== 'all' || filterDay !== 'all') && (
                    <div className="px-6 py-4 bg-yellow-200 border-b-4 border-black flex justify-between items-center font-black text-xl">
                      <span>過濾中：{filterDay !== 'all' ? filterDay : '全部'} / {filterPayer !== 'all' ? filterPayer : '全部成員'}</span>
                      <span className="text-3xl text-pink-600 drop-shadow-[2px_2px_0px_black]">${filterStats.totalSpent.toLocaleString()}</span>
                    </div>
                  )}
                  <div className="overflow-x-auto scrollbar-hide">
                    <table className="w-full text-left min-w-[750px] divide-y-4 divide-black">
                      <thead className="bg-white border-b-4 border-black text-xl font-black uppercase">
                        <tr>
                          <th className="px-6 py-4 border-r-4 border-black">天/項目</th>
                          <th className="px-6 py-4 border-r-4 border-black text-right">多少錢</th>
                          <th className="px-6 py-4 border-r-4 border-black text-center">金主</th>
                          <th className="px-6 py-4 border-r-4 border-black text-center">分攤者</th>
                          <th className="px-6 py-4 text-center">動作</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y-4 divide-black bg-white font-black text-lg">
                        {filteredExpenses.map((e, i) => (
                          <tr key={e.id} className={`hover:bg-cyan-100 ${editingId === e.id ? 'bg-yellow-200' : i % 2 === 0 ? 'bg-white' : 'bg-gray-50'}`}>
                            <td className="px-6 py-4 border-r-4 border-black">
                              <div className="text-blue-600 bg-blue-100 inline-block px-2 border-2 border-black text-sm mb-1 -rotate-1">{e.day}</div>
                              <div className="text-2xl">{e.item}</div>
                            </td>
                            <td className="px-6 py-4 text-right text-3xl border-r-4 border-black">${Number(e.total).toLocaleString()}</td>
                            <td className="px-6 py-4 text-center border-r-4 border-black">
                              <div className="flex flex-wrap justify-center gap-2">
                                {Object.keys(e.payers || {}).map(n => (
                                  <span key={n} className={`px-3 py-1 ${brutalBorder} shadow-[2px_2px_0px_0px_black] ${n === filterPayer ? 'bg-pink-500 text-white rotate-2' : 'bg-yellow-300 -rotate-2'}`}>{n}</span>
                                ))}
                              </div>
                            </td>
                            <td className="px-6 py-4 text-center border-r-4 border-black">
                              <div className="flex flex-wrap justify-center gap-2">
                                {Object.entries(e.splits || {}).map(([n, a]) => Number(a) > 0 && (
                                  <span key={n} className={`px-2 py-1 bg-white ${brutalBorder} text-sm rotate-1`}>{n}</span>
                                ))}
                              </div>
                            </td>
                            <td className="px-6 py-4 text-center">
                              <div className="flex justify-center gap-3">
                                <button onClick={() => startEdit(e)} className={`p-3 bg-blue-400 text-white ${brutalBtn}`}><Edit3 size={24} strokeWidth={3} /></button>
                                <button onClick={() => handleDelete(e.id)} className={`p-3 bg-red-500 text-white ${brutalBtn}`}><Trash2 size={24} strokeWidth={3} /></button>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>

            <div className={`bg-white ${brutalBorder} ${brutalShadowLg} overflow-hidden`}>
              <button onClick={() => toggleCollapse('dashboard')} className="w-full p-6 border-b-4 border-black bg-pink-400 flex items-center justify-between">
                <h2 className="text-2xl font-black flex items-center gap-3 drop-shadow-[2px_2px_0px_white] tracking-widest"><DollarSign strokeWidth={4} size={32} /> 終極大看板</h2>
                {collapsed.dashboard ? <ChevronDown size={28} strokeWidth={3} /> : <ChevronUp size={28} strokeWidth={3} />}
              </button>
              {!collapsed.dashboard && (
                <div>
                  <div className="p-4 bg-yellow-300 border-b-4 border-black flex justify-end">
                    <button onClick={handleExport} className={`flex items-center gap-2 bg-green-500 px-6 py-3 text-lg font-black tracking-widest ${brutalBtn}`}>
                      <Download strokeWidth={3} /> 載出 Excel
                    </button>
                  </div>
                  <div className="overflow-x-auto scrollbar-hide">
                    <table className="w-full text-lg border-collapse min-w-[500px]">
                      <thead className="bg-white border-b-4 border-black font-black uppercase">
                        <tr><th className="px-6 py-5 text-left border-r-4 border-black w-40 text-xl">比一比</th>
                          {participants.map(p => <th key={p} className="px-6 py-5 text-center text-xl">{p}</th>)}</tr>
                      </thead>
                      <tbody className="divide-y-4 divide-black font-black">
                        <tr className="bg-gray-100">
                          <td className="px-6 py-5 border-r-4 border-black">總計應付 (花掉)</td>
                          {participants.map(p => <td key={p} className="px-6 py-5 text-center text-2xl">${(summary[p]?.spent || 0).toLocaleString()}</td>)}
                        </tr>
                        <tr className="bg-white">
                          <td className="px-6 py-5 border-r-4 border-black">先支出 (墊付)</td>
                          {participants.map(p => <td key={p} className="px-6 py-5 text-center text-2xl text-blue-600">${(summary[p]?.paid || 0).toLocaleString()}</td>)}
                        </tr>
                        <tr className="bg-yellow-400">
                          <td className="px-6 py-8 border-r-4 border-black text-2xl">最後結果</td>
                          {participants.map(p => {
                            const bal = (summary[p]?.paid || 0) - (summary[p]?.spent || 0);
                            return (
                              <td key={p} className={`px-6 py-8 text-center border-l-4 border-black border-dashed ${bal < 0 ? 'bg-red-400' : bal > 0 ? 'bg-green-400' : 'bg-yellow-400'}`}>
                                <div className={`text-4xl drop-shadow-[2px_2px_0px_black] ${bal !== 0 ? 'text-white' : ''}`}>
                                  {bal > 0 ? `+${bal.toLocaleString()}` : bal.toLocaleString()}
                                </div>
                                <div className="text-lg mt-2 bg-white inline-block px-3 border-2 border-black rotate-2 shadow-[2px_2px_0px_0px_black]">
                                  {bal < 0 ? '拿錢來! 💸' : bal > 0 ? '發大財! 🤑' : '扯平!'}
                                </div>
                              </td>
                            );
                          })}
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
              )}
            </div>

            {expenses.length > 0 && (
              <div className={`bg-cyan-400 p-8 ${brutalBorder} ${brutalShadowLg} -rotate-1`}>
                <div className="flex items-center justify-between mb-6">
                  <h2 className="text-3xl font-black flex items-center gap-3 tracking-widest drop-shadow-[2px_2px_0px_white] bg-yellow-400 px-4 py-2 border-4 border-black rotate-2">
                    <BrainCircuit size={32} strokeWidth={3} /> 機器人碎碎念
                  </h2>
                  <button onClick={handleAiAnalysis} disabled={isAiLoading} className={`bg-pink-500 text-white px-4 py-2 text-xl font-black ${brutalBtn}`}>
                    {isAiLoading ? <Loader2 className="animate-spin inline" /> : (aiAnalysis ? '再講一次' : '點我發功')}
                  </button>
                </div>
                <div className={`bg-white p-6 min-h-[150px] ${brutalBorder} shadow-[4px_4px_0px_0px_black] text-2xl leading-relaxed font-black rotate-1`}>
                  <p>{aiAnalysis || '點上面的按鈕，讓我看看你們買了什麼怪東西...'}</p>
                </div>
              </div>
            )}
          </div>
        </div>

        <div className={`bg-purple-500 ${brutalBorder} ${brutalShadowLg} rotate-1`}>
          <button onClick={() => toggleCollapse('settlement')} className="w-full p-6 flex items-center justify-between border-b-4 border-black bg-green-400">
            <h2 className="text-3xl font-black flex items-center gap-3 drop-shadow-[2px_2px_0px_white] tracking-widest"><CreditCard strokeWidth={4} size={36} /> 討債小幫手</h2>
            {collapsed.settlement ? <ChevronDown size={32} strokeWidth={4} /> : <ChevronUp size={32} strokeWidth={4} />}
          </button>
          {!collapsed.settlement && (
            <div className="p-8 space-y-6 bg-white">
              <div className="flex gap-4">
                <button onClick={() => setSettlementMode('direct')} className={`flex-1 py-4 text-2xl font-black tracking-widest ${brutalBtn} ${settlementMode === 'direct' ? 'bg-yellow-400' : 'bg-gray-200'}`}>大家互砍</button>
                <button onClick={() => setSettlementMode('centralized')} className={`flex-1 py-4 text-2xl font-black tracking-widest ${brutalBtn} ${settlementMode === 'centralized' ? 'bg-yellow-400' : 'bg-gray-200'}`}>統一收錢</button>
              </div>
              {settlementMode === 'centralized' && settlementsResult.treasurer && (
                <div className="text-center py-6 animate-bounce">
                  <div className="bg-pink-500 text-white px-8 py-4 text-4xl font-black inline-block border-4 border-black shadow-[6px_6px_0px_0px_black] -rotate-3">
                    大總管：{settlementsResult.treasurer}
                  </div>
                </div>
              )}
              {settlementsResult.list.length === 0 ? (
                <div className={`bg-yellow-200 p-8 text-center text-3xl font-black ${brutalBorder} shadow-[6px_6px_0px_0px_black] rotate-2`}>沒人欠錢啦！天下太平！</div>
              ) : (
                <div className="space-y-4 max-h-[400px] overflow-y-auto overflow-x-hidden px-2 py-2">
                  {settlementsResult.list.map((s, i) => (
                    <div key={i} className={`bg-cyan-300 p-6 flex flex-col md:flex-row items-center justify-between ${brutalBorder} shadow-[4px_4px_0px_0px_black] ${i % 2 ? '-rotate-1' : 'rotate-1'}`}>
                      <div className="text-2xl font-black flex items-center gap-4 flex-wrap justify-center">
                        <span className="bg-white px-4 py-2 border-2 border-black">{s.from}</span>
                        <span className="text-pink-600">➡️ 給 ➡️</span>
                        <span className={`px-4 py-2 border-2 border-black ${s.to === settlementsResult.treasurer ? 'bg-yellow-400' : 'bg-white'}`}>{s.to}</span>
                      </div>
                      <div className="text-4xl md:text-5xl font-black text-white drop-shadow-[3px_3px_0px_black] mt-4 md:mt-0">${s.amount.toLocaleString()}</div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
};

export default App;
