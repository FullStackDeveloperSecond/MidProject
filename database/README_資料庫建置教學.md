# MidProject｜SQL Server 資料庫建置教學

> 用途：在另一台電腦建立 SQL Server 資料庫與資料表。  
> 適用：Windows + SQL Server / SQL Server Express / Docker SQL Server / 遠端 SQL Server。  
> Initial Migration 與 Development SeedData 均已建立。Migration 是 Schema 的主要來源，SQL 檔提供無法使用 EF CLI 時的替代方案。

---

## 1. 建議方式：EF Core Migration

先依專案根目錄 `README.md` 設定 `DefaultConnection`，再執行：

```bash
dotnet tool restore
dotnet ef database update \
  --project MidProject/MidProject.csproj \
  --startup-project MidProject/MidProject.csproj
```

這會依 `MidProject/Migrations` 建立或更新資料庫。

只驗證 Model Snapshot 是否與目前 Entity／`AppDbContext` 一致、不連線或更新資料庫時，
可執行：

```bash
./scripts/verify-migration-drift.sh
```

### 已有資料庫的組員：新增 Reports.Category

拉到包含 `20260715153559_AddCategoryToReports` 的版本後，建議直接執行：

```bash
dotnet restore MidProject.sln
dotnet tool restore
dotnet ef database update --project MidProject/MidProject.csproj --startup-project MidProject/MidProject.csproj
```

這個 Migration 會依序：

```text
1. 新增 Reports.Category nvarchar(10)，暫時允許 NULL。
2. 將既有資料回填為「未分類」。
3. 將 Category 改為 NOT NULL。
4. 寫入 __EFMigrationsHistory，之後不會重複執行。
```

無法使用 EF CLI 時，請在目前的 `MidProjectDb` 執行：

```text
database/20260715153559_AddCategoryToReports.sql
```

EF CLI 和這份 SQL 二選一即可，不要兩種方式都執行。更新後可以用以下 SQL 驗證：

```sql
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Reports'
  AND COLUMN_NAME = 'Category';
```

預期結果為 `Category / nvarchar / 10 / NO`。

### 已有資料庫的組員：新增 Members.WarningCount

拉到包含 `20260718074506_AddWarningCountToMembers` 的版本後，在專案根目錄執行：

```powershell
dotnet restore MidProject.sln
dotnet tool restore
dotnet ef database update --project MidProject/MidProject.csproj --startup-project MidProject/MidProject.csproj
```

Migration 會新增 `Members.WarningCount int NOT NULL DEFAULT 0`，並建立
`CK_Members_WarningCount`，避免寫入負數。

無法使用 EF CLI 時，請在既有的 `MidProjectDb` 執行：

```text
database/20260718074506_AddWarningCountToMembers.sql
```

更新後可以用以下 SQL 驗證：

```sql
SELECT WarningCount
FROM Members;
```

既有會員的初始值為 `0`。未來執行警告動作時，應在同一個資料庫交易中將
`WarningCount` 增加 1。

### 已有資料庫的組員：新增點數商城

點數商城包含兩個連續 Migration：

```text
20260723023637_AddAvatarFrameRedemptionTables
20260723064747_FixPointsStoreMappings
```

建議直接執行 EF CLI，兩個 Migration 會依序套用：

```bash
dotnet ef database update \
  --project MidProject/MidProject.csproj \
  --startup-project MidProject/MidProject.csproj
```

無法使用 EF CLI 時，請在既有 `MidProjectDb` 依序執行，不能顛倒：

```text
1. database/20260723023637_AddAvatarFrameRedemptionTables.sql
2. database/20260723064747_FixPointsStoreMappings.sql
```

第一份會建立 `AvatarFrames`、`MemberAvatarFrames`、`PointsTransactions`，
並新增 `Members.EquippedFrameID`、相關 FK、索引與 Check Constraints。
第二份會補上點數商城查詢索引及 `CK_AvatarFrames_PointsPrice`。

EF CLI 與 SQL 腳本二選一即可，不要重複執行兩種方式。

### 已有資料庫的組員：檢舉結果多收件者通知

拉到包含 `20260727121449_AllowPerRecipientReportNotifications` 的版本後，
建議直接執行 EF CLI。這個 Migration 會把通知的檢舉來源唯一索引改為
`ReportID + Outcome + MemberID`，讓檢舉成立時可分別通知檢舉者與被檢舉內容擁有者，
同時避免同一收件者收到重複的同結果通知。

無法使用 EF CLI 時，請在既有 `MidProjectDb` 執行：

```text
database/20260727121449_AllowPerRecipientReportNotifications.sql
```

EF CLI 與 SQL 腳本二選一即可，不要重複執行兩種方式。

### 已有資料庫的組員：強化點數異動規則

拉到包含 `20260727160000_StrengthenPointsTransactionRules` 的版本後，先確認
既有資料沒有違反新規則：

```sql
SELECT *
FROM PointsTransactions
WHERE NOT (
    ([Type] = 'Redeem' AND [Amount] < 0 AND [CreatedBy] IS NULL)
    OR ([Type] = 'Earn' AND [Amount] > 0 AND [CreatedBy] IS NULL)
    OR ([Type] = 'AdminAdjust' AND [Amount] <> 0 AND [CreatedBy] IS NOT NULL)
);
```

查詢應為零筆；若有資料，請先依實際來源修正，不要刪除歷史紀錄。接著執行 EF CLI，
或在既有 `MidProjectDb` 執行：

```text
database/20260727160000_StrengthenPointsTransactionRules.sql
```

新限制會保證兌換為負數、獲得點數為正數、管理員調整不得為零，且只有管理員調整
必須記錄 `CreatedBy`。

---

## 2. 替代方式：SQL 腳本

請使用這個完整腳本：

```text
database/MidProject_CreateDatabaseAndSchema.sql
```

這份會做：

```text
1. 如果沒有 MidProjectDb，就建立資料庫
2. USE MidProjectDb
3. 建立 EF Core Migration 產生的所有資料表 / FK / Index / Check Constraints
4. 寫入 __EFMigrationsHistory
```

歷史 Initial Migration 備份：

```text
database/MidProject_InitialCreate.sql
```

這份只包含最初版本 Schema，不含後續通知、會員警告次數或點數商城變更，
不應用來建立目前最新版資料庫。

---

## 3. Windows + SSMS 操作方式

### Step 1：開啟 SQL Server Management Studio

登入你的 SQL Server。

常見本機 Server Name：

```text
localhost
.
.\SQLEXPRESS
(localdb)\MSSQLLocalDB  ← 只有 Windows LocalDB 可用
```

---

### Step 2：開啟 SQL 檔

在 SSMS：

```text
File → Open → File...
```

選擇：

```text
MidProject_CreateDatabaseAndSchema.sql
```

---

### Step 3：執行

按：

```text
Execute
```

或快捷鍵：

```text
F5
```

---

### Step 4：確認資料庫

執行後左側 Object Explorer 應該看到：

```text
Databases
└── MidProjectDb
    └── Tables
```

應該有這些表：

```text
Members
UserLevels
Restaurants
BusinessHours
Tags
RestaurantTags
Images
RestaurantImages
Reviews
ReviewImages
FavoriteFolders
Favorites
Reports
Notifications
AvatarFrames
MemberAvatarFrames
PointsTransactions
__EFMigrationsHistory
```

---

## 4. Azure Data Studio 操作方式

如果你用 Azure Data Studio：

```text
1. Connect 到 SQL Server
2. File → Open File
3. 開啟 MidProject_CreateDatabaseAndSchema.sql
4. 按 Run
```

---

## 5. Docker SQL Server 操作方式

如果你在另一台電腦用 Docker 跑 SQL Server，可以先啟動：

```bash
docker run -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=Your_password123" \
  -p 1433:1433 \
  --name midproject-sqlserver \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

如果是 Apple Silicon Mac，可能需要：

```bash
--platform linux/amd64
```

完整例子：

```bash
docker run -e "ACCEPT_EULA=Y" \
  -e "SA_PASSWORD=Your_password123" \
  -p 1433:1433 \
  --name midproject-sqlserver \
  --platform linux/amd64 \
  -d mcr.microsoft.com/mssql/server:2022-latest
```

等 SQL Server 啟動後，用 SSMS / Azure Data Studio 連：

```text
Server: localhost,1433
User: sa
Password: Your_password123
Trust Server Certificate: true
```

再執行：

```text
MidProject_CreateDatabaseAndSchema.sql
```

---

## 6. 專案連線字串設定

程式統一讀取的連線字串名稱是：

```text
DefaultConnection
```

請不要將實際連線字串寫入 `appsettings.json`。建議使用 .NET User Secrets：

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost,1433;Database=MidProjectDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true" \
  --project MidProject/MidProject.csproj
```

如果你的 SQL Server 是 Express，可能改成：

```json
"Server=.\\SQLEXPRESS;Database=MidProjectDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

如果你用 Windows Authentication：

```json
"Server=localhost;Database=MidProjectDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

如果你用 SQL Server 帳密：

```json
"Server=localhost;Database=MidProjectDb;User Id=sa;Password=你的密碼;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

---

## 7. SeedData 與專案啟動

在 repo 根目錄執行：

```bash
dotnet build MidProject.sln
dotnet run --project MidProject/MidProject.csproj
```

Development 環境預設執行 `SeedData.InitializeAsync`：

```text
1. 會員等級、會員、餐廳、評論、收藏、檢舉與通知分模組檢查並補齊。
2. 各模組使用固定自然鍵，重複執行不會新增相同 Demo 資料。
3. PointsStoreDemoSeeder 獨立建立三種稀有度商品，以及成對的持有與扣點紀錄。
4. SeedData 不會建立 Schema；必須先套用 Migration 或 SQL 腳本。
```

如果成功，瀏覽器開啟終端機顯示的網址。

---

## 8. 常見問題

### Q1：出現 Login failed for user 'sa'

代表 SQL Server 帳號或密碼錯。

檢查：

```text
User Id
Password
SQL Server 是否允許 SQL Authentication
```

---

### Q2：出現 certificate / SSL 錯誤

連線字串確認有：

```text
TrustServerCertificate=True
```

---

### Q3：找不到資料庫 MidProjectDb

請先執行：

```text
MidProject_CreateDatabaseAndSchema.sql
```

不要只執行 `MidProject_InitialCreate.sql`，除非你已經自己建立資料庫。

---

### Q4：資料表有了，但沒有測試資料

先確認目前環境為 `Development`，且 `appsettings.Development.json` 中：

```json
"SeedData": {
  "Enabled": true
}
```

接著重新啟動專案。若 `Members` 已經存在資料，SeedData 會刻意跳過，避免重複新增。

---

### Q5：出現 `DefaultConnection is not configured`

代表尚未設定本機連線字串。請執行本文件第 6 節的 `dotnet user-secrets set`，或設定環境變數：

```text
ConnectionStrings__DefaultConnection
```
