## 本次修改

請簡短列出這個 PR 修改了什麼。

- 新增點數商城後台：頭像外框商品管理（新增／編輯／上下架／軟刪除／復原）+ 兌換紀錄查詢，側邊欄新增「點數商城」導覽群組
- 依 `Terry_缺失清單.md` 修正 **Points Store／AvatarFrames** 的 P1/P2 問題：
  - 管理員 Claim 解析失敗時拒絕寫入；Toggle／Delete／Restore 對不存在的商品回傳 `404`
  - Rarity 限制為 `Common`／`Rare`／`Limited`，且驗證錯誤訊息會正確顯示給管理員
  - 新增商品的圖片與商品資料包在同一個 DB Transaction；編輯換圖時舊圖會被軟刪除
  - 刪除確認文案改成符合實際「可復原」的軟刪除行為
  - 兌換紀錄列表：`page <= 0` 正規化為 1；起訖日期顛倒會顯示驗證訊息；月統計改用 DB 端投影，不整月讀進記憶體
  - `SortOrder` 接上表單與排序；相關 Service/Repository 改用共用 `ITaipeiClock`
- 修復合併 `dev` 後 `AppDbContext.cs` 殘留重複 entity 設定，導致 `/AvatarFrames` 開啟就是 500（`ImageID1` 影子欄位）
- 依清單修正 **Reviews** 模組 T-R-01～T-R-09：
  - `ReviewsController` 加 `[Authorize(Roles="Admin")]`；刪除稽核改用真實登入管理員 ID，不再寫死 `1`
  - 評論詳情頭像連結從不存在的路徑改成 `AdminMembers/Edit`
  - 修掉 5 個 nullable 警告；還原契約明確寫入註解（清空刪除欄位、不留稽核）
  - 軟刪除／還原跟餐廳統計重算包進同一個 DB Transaction
  - 評論列表新增 `restaurantId` 精確篩選（排序/換頁/其他篩選都會保留）
  - 改用共用 `ITaipeiClock`
  - 新增 `Reviews.Tests` 專案（EF Core InMemory），8 個測試覆蓋列表/詳情/軟刪除/還原/圖片刪除/統計重算

---

## 對應 Issue

無對應 Issue（本次修改依 `Terry_缺失清單.md`（Reviews／Points Store／AvatarFrames 三個章節）的 review 結果進行）

---

## 測試方式

實際測試步驟：

1. 在 repo 根目錄執行 `dotnet build`，應該 0 錯誤
2. 執行 `dotnet test Reviews.Tests`，應該 8/8 通過
3. 啟動專案，以管理員帳號登入後台
4. 進入 `/Reviews`：可以用 Tab／星等／時間／關鍵字篩選、排序、換頁；點任一則評論進詳情頁，頭像跟名字都應該連到 `AdminMembers/Edit`
5. 在評論詳情頁點「軟刪除這則評論」→ 確認刪除 → 應顯示「已刪除」，餐廳的評論數/平均分數應該同步減少；再點「還原這則評論」→ 應恢復顯示，餐廳統計應該加回來
6. 在網址加 `?restaurantId=<某個真實的餐廳ID>` → 應該只顯示該餐廳的評論；再疊加星等篩選，結果應該同時符合兩個條件
7. 登出後直接訪問 `/Reviews`，應該被導回登入頁（不能匿名存取）
8. 進入 `/AvatarFrames`：點「＋ 新增商品」不選圖片直接送出 → 應顯示「請上傳外框圖片。」；選分類時只能選一般/稀有/限定
9. 編輯任一商品，把「排序值」改成比別人小的數字並儲存 → 回列表後應該排到最前面
10. 刪除一個商品 → 確認彈窗跟說明文字都是「可復原」語氣 → 到「已刪除商品」頁面應該看得到，點「復原」應該恢復（狀態為已下架）
11. 進入 `/PointsStoreRedemptions`，把起始日期設得比結束日期晚 → 應顯示驗證訊息；網址加 `?page=-5` → 應正常顯示第 1 頁不噴錯

---

## 是否修改資料庫 / Model

- [ ] 否
- [x] 是，說明：

  新增 3 張表：`AvatarFrames`、`MemberAvatarFrames`、`PointsTransactions`（migration 已由 Alex／愷併入 `dev`）；`Members` 表新增 `EquippedFrameID`（nullable，尚未有動作使用它）；`Images.ImageType` 的 check constraint 新增 `AvatarFrame` 允許值。以上都是既有 migration 帶入的變更，這次 PR 本身沒有新增/修改 migration，只修正了 `AppDbContext.cs` 裡跟這些表相關的重複設定。Reviews 模組的修改沒有動到資料庫結構。

---

## 是否影響其他模組

- [ ] 否
- [x] 是，說明：

  1. `Images` 表新增 `AvatarFrame` 允許值、`Members` 表新增 `EquippedFrameID`：都是純新增，不影響既有查詢/顯示。
  2. `ReviewDisplayHelpers.GetReportStatusLabel` 的文字調整會影響評論詳情頁「檢舉紀錄」卡片的顯示文字。
  3. `IReviewService.GetReviewListAsync` 跟 `IReviewRepository.GetFilteredReviewsAsync` 新增了 `restaurantId` 參數（有預設值，不影響既有呼叫方式）；`ReviewDisplayHelpers.FormatRelative` 改成需要多傳一個 `now` 參數——確認過這兩個方法目前只有 Reviews 模組自己的 View 在用，不影響其他模組。
  4. Solution 新增 `Reviews.Tests` 專案並加進 `MidProject.sln`，純新增不影響現有專案的建置。

---

## 備註

尚未完成、已知問題、需要 Reviewer 注意的地方。

- **會員端「實際兌換／裝備外框」尚未實作**（目前只有後台管理功能），所以 Redeem 方向驗證、跨表交易一致性、裝備所有權驗證目前都還沒有東西可以驗證，也沒有對應的保護
- 沒有正式文件寫下「這輪只做後台」的範圍邊界，目前只在 PR 說明跟對話紀錄裡提過
- `database/` 資料夾的手動 SQL 腳本還沒補上這次新增的 Points Store schema，需要 Alex 協助
- 共用 Demo 種子資料尚未納入 `SeedData.cs`（需要 Alex 整合，避免跟現有「Members 已有資料就整個跳過」的邏輯衝突）
- 真實圖片上傳（JPG/PNG/WebP、5MB 限制）只驗證了副檔名/大小限制邏輯與錯誤訊息，沒有實際跑過完整檔案儲存流程
- Reviews 的還原動作目前不會記錄「誰在什麼時候還原的」，這是刻意決定（見 `ReviewService.RestoreAsync` 上方註解），需要的話要新增欄位

🤖 Generated with [Claude Code](https://claude.com/claude-code)
