# BudPay

LINE 群組記帳機器人。出遊、聚餐時把「誰付了多少、誰要分攤」直接打在群裡,機器人會自動拆帳並算出最少轉帳次數。

## 使用場景

在 LINE 群組裡 @ 機器人,用自然語言記帳:

```
@記帳 午餐 600 我付 大家平分
@記帳 高鐵 1800 Alice 付 我跟 Bob 平分
@記帳 飲料 240 我付 我跟 Alice 6:4
```

訊息會交給 Gemini 解析,拆成「誰付」「誰分攤多少」,寫進當下旅程。

## 指令

- `/旅程` — 查看當前旅程
- `/新增旅程 <名稱> <成員1> <成員2>...` — 開新旅程
- `/切換 <旅程名稱>` — 切換旅程
- `/結算` — 計算當下旅程的總花費與最少轉帳方案

## Web 介面

`Client/` 是 React + Vite + Tailwind 的單頁應用,提供瀏覽器版的旅程管理介面:

- 檢視旅程列表、成員、所有花費明細
- 手動新增 / 編輯 / 刪除花費(不必透過 LINE)
- 即時看到結算結果與最少轉帳方案

適合在 LINE 上快速記錄、回到電腦上整理或修正細節。

```bash
cd Client
yarn install
yarn dev        # http://localhost:5173,呼叫後端 API
```

## 技術

- 後端:ASP.NET Core Minimal API (.NET 10) + PostgreSQL + Gemini API
- 前端:React 18 + Vite + Tailwind CSS


