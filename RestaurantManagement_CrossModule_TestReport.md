# 餐廳管理：跨模組 RestaurantID 篩選與功能測試報告

測試日期：2026-07-24
適用分支：`feature/kefan-restaurant`

## 1. 改寫內容

### 餐廳 → 評論管理

- 餐廳詳細頁的「查看評論」改為傳送精確條件：`/Reviews?RestaurantID={餐廳ID}`，不再以餐廳名稱關鍵字搜尋。
- 評論模組已在 Controller、Service、Repository 串接 `restaurantId`，最後以 `Review.RestaurantID == restaurantId` 篩選。
- 評論頁的頁籤、篩選送出、欄位排序與分頁連結都會保留 `RestaurantID`，避免操作後意外回到全站評論。

### 餐廳 → 檢舉管理

- 餐廳詳細頁的「查看檢舉」改為傳送：`/Reports?RestaurantID={餐廳ID}`。
- 檢舉管理以 RestaurantID 進行精確關聯篩選，包含：
  1. 直接以餐廳為目標的檢舉。
  2. 屬於該餐廳之評論的檢舉。
  3. 屬於該餐廳之餐廳圖片或評論圖片的檢舉。
- 原有的檢舉篩選、排序與分頁已會保留 `RestaurantID`。

### 餐廳一覽「新增餐廳」無反應

- 原因：篩選條件經 AJAX 更新後，`_IndexContent` 會重建「新增餐廳」按鈕；原先綁在舊按鈕的 click 事件一併消失。
- 修正：將事件綁定放入每次 AJAX 內容更新後都會執行的 `afterRender`，並以 `data-bound` 防止重複綁定。

## 2. 實際進行方式

1. 在餐廳詳細頁，由 Razor 直接產生帶有 `RestaurantID` 的評論與檢舉連結。
2. 評論模組將該參數經 Controller → Service → Repository 傳遞，於資料庫查詢階段疊加精確 RestaurantID 條件。
3. 檢舉模組在既有 `ReportQueryParams.RestaurantID` 的基礎上，補齊評論與圖片關聯的 RestaurantID 判斷。
4. 在評論清單頁，將 RestaurantID 回填到 ViewModel，並由頁籤、篩選、排序、分頁連結帶回同一條件。
5. 在餐廳一覽 AJAX 重新渲染後重新綁定新增按鈕，確保更新後的 DOM 節點仍可開啟新增表單。

## 3. 其他組員驗收方法

### 評論管理（Terry）

1. 進入任一餐廳詳細頁，點選「查看評論」。
2. 確認網址包含 `RestaurantID=<該餐廳ID>`。
3. 確認列表中的每一筆評論都屬於該餐廳，而非名稱相近的其他餐廳。
4. 再操作星等、時間、頁籤、欄位排序或分頁；確認網址中的 `RestaurantID` 仍存在，結果未跳回全站資料。

### 檢舉管理（Yin）

1. 進入同一餐廳詳細頁，點選「查看檢舉」。
2. 確認網址包含 `RestaurantID=<該餐廳ID>`。
3. 確認結果只包含該餐廳本身、其評論或其圖片的檢舉；不應出現其他餐廳的關聯資料。
4. 再操作處理狀態、目標類型、分類、日期、排序或分頁；確認 `RestaurantID` 仍被保留。

## 4. 本次實測結果

- 未登入存取 `/Restaurants`：回傳 302，導向 `/Account/Login?ReturnUrl=%2FRestaurants`。
- 管理員登入後：可開啟餐廳一覽、詳細頁、新增表單、編輯表單、停用確認表單與已停用餐廳的還原表單。
- 篩選排序後再點「新增餐廳」：可正常開啟新增表單，瀏覽器 console 無錯誤。
- 餐廳 #5：詳細頁連結正確為 `/Reviews?RestaurantID=5` 與 `/Reports?RestaurantID=5`。
- 餐廳 #5 的評論結果：目前測試資料中的列表皆為「綠意蔬食」。
- 餐廳 #5 的檢舉結果：可取得餐廳、評論、圖片三種關聯目標，且排序／分頁連結均保留 `RestaurantID=5`。
- 圖片上傳系統依工作範圍未納入本次功能測試。
