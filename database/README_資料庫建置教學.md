# MidProject｜SQL Server 資料庫建置教學

> 用途：在另一台電腦建立 SQL Server 資料庫與資料表。  
> 適用：Windows + SQL Server / SQL Server Express / Docker SQL Server / 遠端 SQL Server。  
> 目前只建立資料庫結構，SeedData 尚未完成。

---

## 1. 你需要的檔案

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

備用檔案：

```text
database/MidProject_InitialCreate.sql
```

這份只建立 Schema，不會建立資料庫；使用前要自己先建立並切到 MidProjectDb。

---

## 2. Windows + SSMS 操作方式

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
__EFMigrationsHistory
```

---

## 3. Azure Data Studio 操作方式

如果你用 Azure Data Studio：

```text
1. Connect 到 SQL Server
2. File → Open File
3. 開啟 MidProject_CreateDatabaseAndSchema.sql
4. 按 Run
```

---

## 4. Docker SQL Server 操作方式

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

## 5. 專案連線字串設定

目前程式讀取的連線字串代名是：

```text
AlexConnectionString
```

位置：

```text
MidProject/appsettings.json
```

目前範例：

```json
{
  "ConnectionStrings": {
    "AlexConnectionString": "Server=localhost;Database=MidProjectDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;MultipleActiveResultSets=true"
  }
}
```

如果你的 SQL Server 是 Express，可能改成：

```json
"AlexConnectionString": "Server=.\\SQLEXPRESS;Database=MidProjectDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

如果你用 Windows Authentication：

```json
"AlexConnectionString": "Server=localhost;Database=MidProjectDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

如果你用 SQL Server 帳密：

```json
"AlexConnectionString": "Server=localhost;Database=MidProjectDb;User Id=sa;Password=你的密碼;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

---

## 6. 跑 ASP.NET Core 專案

在 repo 根目錄執行：

```bash
dotnet build
dotnet run --project MidProject/MidProject.csproj
```

如果成功，瀏覽器開啟終端機顯示的網址。

---

## 7. 常見問題

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

目前正常，因為 SeedData 尚未完成。

下一階段會建立：

```text
SeedData.cs
```

到時候會補：

```text
Admin 帳號
會員假資料
餐廳假資料
評論 / 檢舉 / 通知假資料
圖片路徑資料
```
