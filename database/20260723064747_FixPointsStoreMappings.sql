BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723064747_FixPointsStoreMappings'
)
BEGIN
    DROP INDEX [IX_PointsTransactions_MemberID] ON [PointsTransactions];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723064747_FixPointsStoreMappings'
)
BEGIN
    CREATE INDEX [IX_PointsTransactions_MemberID_CreatedAt] ON [PointsTransactions] ([MemberID], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723064747_FixPointsStoreMappings'
)
BEGIN
    CREATE INDEX [IX_PointsTransactions_Type] ON [PointsTransactions] ([Type]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723064747_FixPointsStoreMappings'
)
BEGIN
    CREATE INDEX [IX_AvatarFrames_IsActive] ON [AvatarFrames] ([IsActive]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723064747_FixPointsStoreMappings'
)
BEGIN
    CREATE INDEX [IX_AvatarFrames_IsDeleted] ON [AvatarFrames] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723064747_FixPointsStoreMappings'
)
BEGIN
    EXEC(N'ALTER TABLE [AvatarFrames] ADD CONSTRAINT [CK_AvatarFrames_PointsPrice] CHECK ([PointsPrice] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723064747_FixPointsStoreMappings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723064747_FixPointsStoreMappings', N'8.0.22');
END;
GO

COMMIT;
GO
