BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718074506_AddWarningCountToMembers'
)
BEGIN
    ALTER TABLE [Members] ADD [WarningCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718074506_AddWarningCountToMembers'
)
BEGIN
    EXEC(N'ALTER TABLE [Members] ADD CONSTRAINT [CK_Members_WarningCount] CHECK ([WarningCount] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718074506_AddWarningCountToMembers'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260718074506_AddWarningCountToMembers', N'8.0.22');
END;
GO

COMMIT;
GO
