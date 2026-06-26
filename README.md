# R3 — 分帳不再吵架

出遊、聚餐的分帳神器。把「誰付了多少、誰要分攤」記下來，自動拆帳並算出**最少轉帳次數**。

R3 有兩個入口，共用同一套旅程 / 成員 / 花費資料：

1. **LINE 機器人** — 在群組裡用斜線指令與自然語言記帳。
2. **Web App（React PWA）** — 瀏覽器版完整管理介面，可安裝、可離線。

---

## 1. LINE 機器人

在 LINE 群組裡用指令操作，記帳訊息交給 **Gemini** 解析成「誰付」「誰分攤多少」。

### 指令

| 指令 | 說明 |
|---|---|
| `/旅程 <名稱>` | 建立並切換到新旅程 |
| `/旅程 列表` | 列出本群所有旅程 |
| `/切換 <名稱或 id>` | 切換作用中的旅程 |
| `/結算` | 計算目前旅程的花費與最少轉帳方案 |
| `/阿珊 <記帳內容>` | 記一筆花費（觸發詞預設 `/阿珊`，可由 `Line:TriggerKeyword` 設定） |

### 自然語言記帳

```
/阿珊 午餐 600 我付 大家平分
/阿珊 高鐵 1800 Alice 付 我跟 Bob 平分
/阿珊 飲料 240 我付 我跟 Alice 6:4
```

- 用 `@提及` 指定參與者，否則以發話者為付款人。
- 新名字會自動加入旅程成員。
- 每個群組同時只有一個「作用中」旅程，新記帳會記進它。

---

## 2. Web App（React PWA）

`Client/` 是 React + Vite + Tailwind 的單頁應用，採 **Neo-Brutalist** 視覺風格，是可安裝的 **PWA**（可加到主畫面、離線檢視）。

### 功能

- **登入 / 註冊** — Email + 密碼，或 **LINE Login**。
- **旅程管理** — 旅程列表、建立、編輯、刪除。建立新旅程時**自動把建立者本人放進分攤名單並鎖定**（不必手動加名字，也不能把自己刪掉）。
- **記帳** — 兩種方式：
  - 手動表單：天數、項目、金額、付款人（單一 / 多人）、分攤方式（平分 / 自訂）。
  - **AI 魔法輸入框**：貼上一段自然語言，Gemini 自動拆成多筆花費。
- **結算** — 兩種模式：「大家互砍」（最少轉帳）與「統一收錢」（指定大總管）。
- **大看板** — 每人墊付 / 應付 / 結餘一覽，並可**匯出 CSV**。
- **AI 碎碎念** — 讓 Gemini 對這趟花費給點評。
- **分享邀請（Magic Link）** — 旅程擁有者可產生一條分享連結：
  - 用 **LINE 分享**或**複製連結**丟給朋友。
  - 對方點連結、登入後，從**尚未被認領的名字**中挑一個認領（一人一名），即加入該旅程。
  - 連結可重置、有有效期限（`Share:LinkDays`，預設 7 天）。

### 權限模型

- 每個旅程有一位**擁有者**（owner）與多位**成員**（member）。
- 擁有者可編輯旅程、管理成員、產生分享連結；成員可檢視與記帳。
- LINE 與 Web 兩條路徑記的每一筆花費都會標記來源（`web` / `line`）與記帳者。

---

## 技術

- **後端**：ASP.NET Core Minimal API（.NET 10）+ PostgreSQL，JWT 驗證（access token + 旋轉式 refresh token），Gemini API 解析 / 點評。
- **前端**：React 18 + Vite + Tailwind CSS，`vite-plugin-pwa`（可安裝、離線快取）。

## 在本機跑起來

```bash
# 後端（http://localhost:5000）
dotnet restore
dotnet run

# 前端（http://localhost:5173，開發時 proxy 到後端）
cd Client
yarn install
yarn dev
```

本機需要一個 PostgreSQL，並在 `appsettings.json`（已 gitignore）填入必要設定：
`ConnectionStrings:Postgres`、`Line:ChannelSecret`、`Line:ChannelAccessToken`、`Gemini:ApiKey`、
`Jwt:SignKey`（≥ 32 bytes），以及 LINE Login 用的 `LineLogin:*`。詳見 `CLAUDE.md`。

```bash
docker run -d --name budpay-pg -p 5432:5432 \
  -e POSTGRES_USER=budpay -e POSTGRES_PASSWORD=budpay -e POSTGRES_DB=budpay \
  postgres:16
```

`dotnet publish` 會自動 build 前端並一起打包成單一服務（同源提供 SPA 與 API）。
