# 美食地圖｜Kefan 餐廳模組（Restaurants / Tags）

本檔案說明 `feature/kefan-restaurant` 分支這次新增的內容：餐廳管理與餐廳標籤管理模組。建立在 `README.md` 描述的共用基礎（Entity、`AppDbContext`、Migration、`Program.cs`、共用 `Layout`）之上，**完全沒有修改共用基礎本身**，只新增 Kefan 負責範圍內的 Controller、ViewModel、Service、Repository、View。

對應規格書：`23_Kefan餐廳模組MVP開發規格書_組員版.md`。

## 開發範圍

負責：

```text
Restaurants 餐廳管理
Tags 餐廳標籤管理
RestaurantTags 餐廳與標籤關聯
BusinessHours 營業時間
```

不負責、也沒有修改：

```text
Entity / AppDbContext / Migration / Program.cs 的核心邏輯
共用 Layout（_Layout.cshtml 只做了最小加法：在既有 navbar 加 4 個導覽連結）
會員 / 評論 / 檢舉 / 通知模組
圖片上傳底層（本次為串接既有 Images/RestaurantImages schema 的陽春版暫代方案）
```

## 新增的專案結構

```text
MidProject/
├── Controllers/
│   ├── RestaurantsController.cs   # 一覽/詳細/新增/編輯/停用/停用清單/解除停用
│   └── TagsController.cs          # 標籤一覽/新增/停用切換
├── Models/ViewModels/
│   ├── Restaurants/                # RestaurantIndex/Detail/Form/Deleted ViewModel、
│   │                                # BusinessHours 表單與 JSON 序列化 Helper、
│   │                                # RestaurantOptions（台灣縣市/行政區對照表）
│   └── Tags/                       # TagsIndexViewModel、TagCardViewModel
├── Repositories/
│   ├── IRepositories/              # IRestaurantRepository、ITagRepository
│   ├── RestaurantRepository.cs
│   └── TagRepository.cs
├── Services/
│   ├── IServices/                  # IRestaurantService、ITagService、IImageUploadService
│   ├── RestaurantService.cs
│   ├── TagService.cs
│   └── ImageUploadService.cs       # 陽春版檔案上傳（不裁切/不轉檔）
├── Views/
│   ├── Restaurants/                # Index / Details / Deleted / _FormPartial
│   └── Tags/                       # Index
└── wwwroot/
    ├── css/restaurant-admin.css
    └── js/restaurant-admin.js
```

`Program.cs` 只多了 5 行 DI 註冊（`AddScoped<IRestaurantRepository, ...>` 等），沒有動到既有的 DbContext / SeedData / 路由設定。

## 功能說明

### 餐廳一覽（`/Restaurants`）

- 統計卡片：餐廳總數、平均星級、評論總數、停用中數量。
- 搜尋（名稱/地址/備註關鍵字）、縣市、行政區（依縣市分組的 `<optgroup>`）、標籤、排序（最新新增/星級最高/評論最多），皆為一般 GET 查詢字串，可加書籤分享。
- 分頁（每頁 10 筆）。
- 操作欄只有「查看」（眼睛圖示，實際 `<a>` 連結）與「編輯」（鉛筆圖示）兩個按鈕；**只有眼睛圖示會導到詳細頁**，點列表其他空白處不會有反應。

### 新增 / 編輯餐廳

- 真正的彈出視窗（Bootstrap 風格 Modal + AJAX，非整頁跳轉）：`GET /Restaurants/CreateForm`、`GET /Restaurants/EditForm/{id}` 回傳表單片段，`POST /Restaurants/Create`、`POST /Restaurants/Edit/{id}` 驗證失敗時回傳同一片段（含錯誤訊息），成功則回傳 JSON 導向詳細頁。
- 縣市／行政區為**連動下拉**：選縣市後才會出現對應的行政區選項（`RestaurantOptions.CityDistricts`，涵蓋台灣 22 縣市、368 個行政區的真實資料），避免存出「台北市／左營區」這種對不上的組合。
- 標籤：pill 按鈕多選（純 CSS `:checked + label`，不需額外 JS 就能顯示選取狀態），至少需選一個。
- 營業時間：7 天各自可設定公休/營業、每天可有多個時段（如午晚兩段制），可「套用週一至全週」。前端用一個隱藏欄位傳遞 JSON（`HoursJson`），避免動態新增/刪除時段時傳統 `List<T>` 索引綁定容易出錯的問題。
- 圖片：封面圖（單張）＋環境圖（多張），陽春版上傳（僅檢查副檔名 jpg/png/webp 與 5MB 上限，不裁切、不轉 WebP），存到 `wwwroot/uploads/{ImageType}/`，寫入既有的 `Images`/`RestaurantImages` schema，未來由愷/Alex 的正式圖片介面取代。

### 餐廳詳細頁

- 圖片（封面 + 環境圖，含燈箱檢視／環境圖 Gallery）、基本資訊、營業時間（多時段清楚分行顯示，如 `11:00–14:00` / `17:00–21:00`）、評分摘要（連往 `/Reviews?restaurantId=`）、座標、檢舉摘要（連往 `/Reports?restaurantId=`，路由由其他組員之後建置）。
- 標籤顯示為可點擊按鈕，點擊會導到餐廳一覽並套用該標籤篩選。**已停用的標籤不會出現**在任何餐廳的標籤列表中（一覽、停用清單、詳細頁皆然）。
- 停用餐廳會多顯示「停用資訊」卡片（時間/執行者/原因），操作按鈕改為「編輯」「解除停用」。

### 停用 / 解除停用

- 停用需填寫原因，寫入 `IsDeleted`/`DeletedAt`/`DeletedBy`/`DeleteReason`。
- 只做軟刪除與解除停用，**本階段不做永久刪除**（依規格書 8.6，`Restaurants` 被多張表關聯，永久刪除容易遇到 FK 限制）。

### 餐廳標籤一覽（`/Tags`）

- 顯示啟用中／停用中標籤，各自附上目前使用中的餐廳數（計算時排除已停用餐廳）。
- 新增標籤（若同名標籤已停用，會直接重新啟用而非產生重複資料）、停用/啟用切換。

## 台灣縣市/行政區資料

`Restaurants.City`/`District` 本身是純文字欄位（非外鍵表），因此新增的是**程式碼端的參考資料**（`RestaurantOptions.CityDistricts`），沒有新增資料庫表、沒有動 Entity/Migration：

- 22 縣市、368 個行政區的完整對照表。
- 餐廳一覽篩選：行政區下拉用 `<optgroup>` 依縣市分組。
- 新增/編輯表單：行政區為即時連動下拉（見上）。

另外資料庫裡已經追加了 40 筆分布在多個縣市的假餐廳資料（含標籤、營業時間），供展示與測試篩選/分頁使用。這是**單次執行的資料匯入**（獨立小工具直接 INSERT 到既有資料表，未包含在此分支程式碼中），不是遷移或程式邏輯的一部分；如果在別的環境重新建置資料庫，不會自動出現這 40 筆資料。

## 已知限制

```text
圖片上傳為陽春版（不裁切、不轉檔），等待正式圖片介面。
不支援永久刪除餐廳（依規格書刻意排除）。
SeedData.cs 原有的示範餐廳圖片路徑本身無對應實體檔案（404），非本次異動造成。
評分摘要 / 檢舉摘要卡片上的連結導向 Reviews/Reports 尚未建置的路由，屬預留串接點。
```

## 本機開發設定

連線字串統一讀 `DefaultConnection`，比照根目錄 `README.md` 的規則，**不寫入 Git 追蹤的 appsettings.json**。除了 README 提到的 User Secrets 方式，本機測試時也可以用專案內建但被 `.gitignore` 排除的 `appsettings.Development.local.json`（`Program.cs` 已加一行 `AddJsonFile(..., optional: true)` 讀取它），內容格式：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=MidProjectDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

這個檔案不受 ASPNETCORE_ENVIRONMENT 或啟動方式影響，比 User Secrets 更不容易因為不同啟動路徑（`dotnet run` vs. IDE 偵錯）讀不到設定。
