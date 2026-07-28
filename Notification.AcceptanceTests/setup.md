# Notifications 外部驗收設定

這個專案包含 27 個核准驗收準則。完整回歸套件執行 27 次、Targeted Change
執行 10 次、Impacted Regression 執行 13 次，共 50 個整合驗收執行。

驗收測試採 fail-closed：未提供獨立 driver、隔離資料庫 fixture 或可連線的測試站台時，
測試必須失敗，不得改用固定 JSON、實作內部 bypass 或直接讀取受測程式碼來宣告通過。

## 先決條件

- 隔離的 SQL Server 測試資料庫，不得使用開發者日常資料庫。
- 可啟停的 MidProject 測試站台。
- 一個獨立驗收 driver，可依 criterion 重設 fixture、操作 HTTP／瀏覽器／資料庫，
  並將直接觀察結果輸出成 JSON。
- 驗收日最新與前一主要版本的桌面 Chrome、Edge。
- AccountLogin 模式專用測試 Admin 帳密，由環境變數提供。

driver 的命令列契約：

```text
notification-acceptance-driver \
  --criterion AC-AUTH-001 \
  --base-url https://isolated-midproject.example \
  --fixture-manifest /absolute/path/to/fixture-manifest.json
```

標準輸出只能有一份 JSON：

```json
{
  "criterion_id": "AC-AUTH-001",
  "observations": {
    "non_admin_http_status": 403
  }
}
```

`observations` 可包含額外診斷欄位，但必須包含
`Scenarios/acceptance-scenarios.json` 對應準則的所有葉節點。不得輸出
`accepted` 或 `passed`，是否符合預期只由 xUnit 比對決定。

## 環境變數

```bash
export NOTIFICATION_ACCEPTANCE_DRIVER=/absolute/path/to/notification-acceptance-driver
export NOTIFICATION_BASE_URL=https://isolated-midproject.example
export NOTIFICATION_FIXTURE_MANIFEST=/absolute/path/to/verifier-fixture-manifest.json
export NOTIFICATION_ACCEPTANCE_LOGIN_EMAIL=admin@example.com
export NOTIFICATION_ACCEPTANCE_LOGIN_PASSWORD='test-only-password'
```

實際 fixture manifest 必須滿足
`fixtures/fixture-contract.json`，並只保存測試識別碼；帳密、Cookie、Token、Secret、
connection string 不得放入 manifest 或 driver 參數。

## 執行順序

先驗證靜態 protocol：

```bash
dotnet test Notification.AcceptanceTests/Notification.AcceptanceTests.csproj \
  --filter "Suite=Protocol"
```

再於每次 suite 前重建隔離資料庫與站台：

```bash
dotnet test Notification.AcceptanceTests/Notification.AcceptanceTests.csproj \
  --filter "Suite=TargetedChange"

dotnet test Notification.AcceptanceTests/Notification.AcceptanceTests.csproj \
  --filter "Suite=ImpactedRegression"

dotnet test Notification.AcceptanceTests/Notification.AcceptanceTests.csproj \
  --filter "Suite=FullProjectRegression"
```

每個 criterion 開始前，driver 必須依 fixture manifest 的 `reset_rule` 重設資料；
每個身分模式或啟動失敗案例必須啟動全新站台 process，且 process 生命週期內不得切換
`Notifications__IdentityMode` 或 fallback。

## 本機一般驗證與完整驗收的差異

`dotnet test MidProject.sln --filter "Suite=Protocol"` 可在沒有外部站台時驗證 manifest
與 fixture contract。完整 `dotnet test MidProject.sln` 會真正執行外部驗收；若上述環境
未備齊便會明確失敗，這是預期的安全行為。
