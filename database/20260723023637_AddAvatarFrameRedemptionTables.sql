BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    ALTER TABLE [Images] DROP CONSTRAINT [CK_Images_ImageType];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    ALTER TABLE [Members] ADD [EquippedFrameID] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE TABLE [AvatarFrames] (
        [FrameID] int NOT NULL IDENTITY,
        [Name] nvarchar(50) NOT NULL,
        [Description] nvarchar(500) NULL,
        [Rarity] nvarchar(10) NOT NULL DEFAULT N'Common',
        [PointsPrice] int NOT NULL,
        [ImageID] int NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        CONSTRAINT [PK_AvatarFrames] PRIMARY KEY ([FrameID]),
        CONSTRAINT [CK_AvatarFrames_Rarity] CHECK ([Rarity] IN ('Common', 'Rare', 'Limited')),
        CONSTRAINT [FK_AvatarFrames_Images_ImageID] FOREIGN KEY ([ImageID]) REFERENCES [Images] ([ImageID]),
        CONSTRAINT [FK_AvatarFrames_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE TABLE [MemberAvatarFrames] (
        [MemberAvatarFrameID] int NOT NULL IDENTITY,
        [MemberID] int NOT NULL,
        [FrameID] int NOT NULL,
        [RedeemedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        CONSTRAINT [PK_MemberAvatarFrames] PRIMARY KEY ([MemberAvatarFrameID]),
        CONSTRAINT [FK_MemberAvatarFrames_AvatarFrames_FrameID] FOREIGN KEY ([FrameID]) REFERENCES [AvatarFrames] ([FrameID]),
        CONSTRAINT [FK_MemberAvatarFrames_Members_MemberID] FOREIGN KEY ([MemberID]) REFERENCES [Members] ([MemberID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE TABLE [PointsTransactions] (
        [TransactionID] int NOT NULL IDENTITY,
        [MemberID] int NOT NULL,
        [Amount] int NOT NULL,
        [BalanceAfter] int NOT NULL,
        [Type] nvarchar(20) NOT NULL,
        [RelatedFrameID] int NULL,
        [Note] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [CreatedBy] int NULL,
        CONSTRAINT [PK_PointsTransactions] PRIMARY KEY ([TransactionID]),
        CONSTRAINT [CK_PointsTransactions_BalanceAfter] CHECK ([BalanceAfter] >= 0),
        CONSTRAINT [CK_PointsTransactions_RelatedFrame] CHECK (([Type] = 'Redeem' AND [RelatedFrameID] IS NOT NULL) OR ([Type] <> 'Redeem' AND [RelatedFrameID] IS NULL)),
        CONSTRAINT [CK_PointsTransactions_Type] CHECK ([Type] IN ('Redeem', 'AdminAdjust', 'Earn')),
        CONSTRAINT [FK_PointsTransactions_AvatarFrames_RelatedFrameID] FOREIGN KEY ([RelatedFrameID]) REFERENCES [AvatarFrames] ([FrameID]),
        CONSTRAINT [FK_PointsTransactions_Members_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_PointsTransactions_Members_MemberID] FOREIGN KEY ([MemberID]) REFERENCES [Members] ([MemberID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE INDEX [IX_Members_EquippedFrameID] ON [Members] ([EquippedFrameID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    EXEC(N'ALTER TABLE [Images] ADD CONSTRAINT [CK_Images_ImageType] CHECK ([ImageType] IN (''RestaurantCover'', ''RestaurantEnvironment'', ''ReviewImage'', ''MemberAvatar'', ''AvatarFrame''))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE INDEX [IX_AvatarFrames_DeletedBy] ON [AvatarFrames] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE INDEX [IX_AvatarFrames_ImageID] ON [AvatarFrames] ([ImageID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE INDEX [IX_MemberAvatarFrames_FrameID] ON [MemberAvatarFrames] ([FrameID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MemberAvatarFrames_MemberID_FrameID] ON [MemberAvatarFrames] ([MemberID], [FrameID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE INDEX [IX_PointsTransactions_CreatedBy] ON [PointsTransactions] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE INDEX [IX_PointsTransactions_MemberID] ON [PointsTransactions] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    CREATE INDEX [IX_PointsTransactions_RelatedFrameID] ON [PointsTransactions] ([RelatedFrameID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    ALTER TABLE [Members] ADD CONSTRAINT [FK_Members_AvatarFrames_EquippedFrameID] FOREIGN KEY ([EquippedFrameID]) REFERENCES [AvatarFrames] ([FrameID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_Reports_SourceReportID] FOREIGN KEY ([SourceReportID]) REFERENCES [Reports] ([ReportID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723023637_AddAvatarFrameRedemptionTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723023637_AddAvatarFrameRedemptionTables', N'8.0.22');
END;
GO

COMMIT;
GO
