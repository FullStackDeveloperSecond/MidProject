# 美食地圖｜MidProject 基礎專案

這個分支提供五人協作開發所需的共用基礎，只包含 ASP.NET Core MVC 架構、Entity、`AppDbContext`、Initial Migration 與開發用 SeedData。

目前不包含會員、餐廳、評論、檢舉、通知等功能模組的 Controller、ViewModel、Service 或 CRUD 頁面。

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
│   └── SeedData.cs       # Development Demo 資料
├── Migrations/           # Initial Migration 與 Model Snapshot
├── Models/               # 資料庫 Entity
├── Services/
│   └── PasswordHashService.cs
├── Views/                # 最小 MVC 啟動頁與共用 Layout
├── wwwroot/uploads/      # 圖片類型目錄骨架
└── Program.cs            # MVC、DbContext、SeedData 啟動設定

database/
├── MidProject_CreateDatabaseAndSchema.sql
├── MidProject_InitialCreate.sql
└── README_資料庫建置教學.md
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

也可使用環境變數：

```text
ConnectionStrings__DefaultConnection
```

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

Development 環境預設啟用 SeedData；當 `Members` 已經有資料時不會重複建立 Demo 資料。

## 驗證

```bash
dotnet build MidProject.sln --no-restore
dotnet ef migrations has-pending-model-changes \
  --project MidProject/MidProject.csproj \
  --startup-project MidProject/MidProject.csproj
```

## 開發邊界

- Entity、`AppDbContext`、Migration、`Program.cs` 與共用 Layout 由愷／Alex 統一控管。
- 組員從這個基礎建立自己模組的 Controller、ViewModel、Service 與 View。
- 主要資料採 Soft Delete，FK 預設使用 `DeleteBehavior.NoAction`。
- 狀態值在資料庫保存英文固定值，View 顯示繁體中文。
- 不要直接提交資料庫密碼、正式帳號或其他秘密設定。
