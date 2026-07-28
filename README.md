# 美食地圖｜MidProject 基礎專案

這是美食地圖後台管理系統，包含會員、餐廳、評論、檢舉、通知與點數商城模組，
採 ASP.NET Core MVC、Entity Framework Core 與 SQL Server。

## 技術

- .NET 8
- ASP.NET Core MVC
- Entity Framework Core 8
- SQL Server
- Razor View / Bootstrap 5

## 專案結構

```text
MidProject/
├── Controllers/          # 僅保留最小 HomeController
├── Data/
│   ├── AppDbContext.cs   # DbSet、關聯、索引與限制
│   ├── AppDbContextFactory.cs # EF CLI Design-time DbContext
│   ├── SeedData.cs       # Development Demo 資料
│   └── PointsStoreDemoSeeder.cs # 點數商城獨立 Demo 資料
├── Migrations/           # Initial Migration 與 Model Snapshot
├── Models/               # 資料庫 Entity
├── Services/
│   └── PasswordHashService.cs
├── Views/                # 最小 MVC 啟動頁與共用 Layout
├── wwwroot/uploads/      # 圖片類型目錄骨架
└── Program.cs            # MVC、DbContext、SeedData 啟動設定

database/
├── 20260715153559_AddCategoryToReports.sql # 已有資料庫單獨升級用
├── 20260718074506_AddWarningCountToMembers.sql # 已有資料庫新增會員警告次數
├── 20260723023637_AddAvatarFrameRedemptionTables.sql # 點數商城資料表
├── 20260723064747_FixPointsStoreMappings.sql # 點數商城索引與限制修正
├── 20260727160000_StrengthenPointsTransactionRules.sql # 點數異動規則
├── 20260728070110_AddTagSortOrder.sql # 餐廳標籤排序
├── MidProject_CreateDatabaseAndSchema.sql
├── MidProject_InitialCreate.sql
└── README_資料庫建置教學.md

Notification.AcceptanceTests/
├── Scenarios/acceptance-scenarios.json # 27 個核准準則／50 次 suite 執行
├── fixtures/fixture-contract.json      # 獨立驗收環境契約
└── setup.md                            # 外部 driver 與執行方式
```

## 第一次啟動

### 1. 還原套件與工具

```bash
dotnet restore MidProject.sln
dotnet tool restore
```

### 2. 設定本機連線字串

專案統一讀取 `DefaultConnection`。連線字串不得寫入 Git 追蹤的 `appsettings.json`。

建議使用 .NET User Secrets：

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=MidProjectDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true" \
  --project MidProject/MidProject.csproj
```

確認本機已設定但不輸出到 Git：

```bash
dotnet user-secrets list --project MidProject/MidProject.csproj
```

也可使用環境變數：

```text
ConnectionStrings__DefaultConnection
```

若 SQL 密碼或其他憑證曾提交到 Git，從設定檔刪除並不能使舊憑證失效；必須另外在 SQL Server 端輪替該憑證，再更新每位開發者的 User Secrets。

### 3. 建立／更新資料庫

```bash
dotnet ef database update \
  --project MidProject/MidProject.csproj \
  --startup-project MidProject/MidProject.csproj
```

若不使用 EF CLI，也可以依照 [資料庫建置教學](database/README_資料庫建置教學.md) 執行 SQL 腳本。

### 4. 啟動

```bash
dotnet run --project MidProject/MidProject.csproj
```

Development 環境預設啟用模組化 SeedData；各模組使用固定自然鍵獨立補齊 Demo
資料，重複啟動不會新增相同資料。點數商城 Demo 由獨立 Seeder 建立商品、持有紀錄與
成對的點數異動。

### 圖片上傳規則

- 接受 JPEG、PNG、WebP，單檔最多 5MB、長寬最多 4096×4096。
- 伺服器會辨識實際格式、完整解碼並重新輸出；副檔名偽裝、損毀與多幀圖片會被拒絕。
- runtime 圖片存放於 `MidProject/wwwroot/uploads/`，不納入 Git。
- Git 只保留各目錄 `.gitkeep`、預設會員頭像及兩張評論 Demo 圖片。
- 餐廳與外框商品的圖片異動使用資料庫交易；資料庫失敗會移除本次新檔，
  換圖後的舊檔只會在確認沒有餐廳、評論、會員、商品或檢舉引用時清理。

## 驗證

```bash
dotnet build MidProject.sln --no-restore
dotnet test MidProject.sln --no-build --filter "Suite=Protocol"
./scripts/verify-migration-drift.sh
```

Notifications 的 50 次完整整合驗收需要獨立站台、fixture 與外部 driver，設定方式見
[Notification.AcceptanceTests/setup.md](Notification.AcceptanceTests/setup.md)。

Migration drift 腳本直接比較執行時 model 與最新 Snapshot，不建立 Web Host、不執行
SeedData、不需要本機 SQL Server，也不會修改資料庫；命令以非 0 結束即代表兩者不一致。

PR 與 `dev` push 會由 `.github/workflows/ci.yml` 自動執行 solution build、
Notifications protocol tests 與 Migration drift。

Notifications 的 Information 以上操作日誌會寫入 `MidProject/App_Data/logs/` 的每日
JSONL 分段檔，每段上限 20MB，僅保留台灣日期最近 14 天；目錄、天數與單檔上限可由
`NotificationLogging` 設定覆寫。

## 開發邊界

- Entity、`AppDbContext`、Migration、`Program.cs` 與共用 Layout 由愷／Alex 統一控管。
- 組員從這個基礎建立自己模組的 Controller、ViewModel、Service 與 View。
- 主要資料採 Soft Delete，FK 預設使用 `DeleteBehavior.NoAction`。
- 狀態值在資料庫保存英文固定值，View 顯示繁體中文。
- 不要直接提交資料庫密碼、正式帳號或其他秘密設定。
