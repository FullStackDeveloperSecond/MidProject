BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727160000_StrengthenPointsTransactionRules'
)
BEGIN
    ALTER TABLE [PointsTransactions] WITH CHECK
        ADD CONSTRAINT [CK_PointsTransactions_AmountByType]
        CHECK (
            ([Type] = 'Redeem' AND [Amount] < 0)
            OR ([Type] = 'Earn' AND [Amount] > 0)
            OR ([Type] = 'AdminAdjust' AND [Amount] <> 0)
        );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727160000_StrengthenPointsTransactionRules'
)
BEGIN
    ALTER TABLE [PointsTransactions] WITH CHECK
        ADD CONSTRAINT [CK_PointsTransactions_CreatedByType]
        CHECK (
            ([Type] = 'AdminAdjust' AND [CreatedBy] IS NOT NULL)
            OR ([Type] IN ('Redeem', 'Earn') AND [CreatedBy] IS NULL)
        );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727160000_StrengthenPointsTransactionRules'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727160000_StrengthenPointsTransactionRules', N'8.0.22');
END;
GO

COMMIT;
GO
