IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [UserLevels] (
        [LevelID] int NOT NULL IDENTITY,
        [LevelName] nvarchar(50) NOT NULL,
        [MinExp] int NOT NULL,
        [Rewards] nvarchar(50) NULL,
        CONSTRAINT [PK_UserLevels] PRIMARY KEY ([LevelID]),
        CONSTRAINT [CK_UserLevels_MinExp] CHECK ([MinExp] >= 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [BusinessHours] (
        [BusinessHourID] int NOT NULL IDENTITY,
        [RestaurantID] int NOT NULL,
        [DayOfWeek] int NOT NULL,
        [OpenTime] time(0) NOT NULL,
        [CloseTime] time(0) NOT NULL,
        [IsClosed] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_BusinessHours] PRIMARY KEY ([BusinessHourID]),
        CONSTRAINT [CK_BusinessHours_DayOfWeek] CHECK ([DayOfWeek] >= 1 AND [DayOfWeek] <= 7)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [FavoriteFolders] (
        [FavoriteFolderID] int NOT NULL IDENTITY,
        [MemberID] int NOT NULL,
        [FolderName] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        CONSTRAINT [PK_FavoriteFolders] PRIMARY KEY ([FavoriteFolderID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [Favorites] (
        [FavoriteID] int NOT NULL IDENTITY,
        [MemberID] int NOT NULL,
        [RestaurantID] int NOT NULL,
        [FavoriteFolderID] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        CONSTRAINT [PK_Favorites] PRIMARY KEY ([FavoriteID]),
        CONSTRAINT [FK_Favorites_FavoriteFolders_FavoriteFolderID] FOREIGN KEY ([FavoriteFolderID]) REFERENCES [FavoriteFolders] ([FavoriteFolderID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [Images] (
        [ImageID] int NOT NULL IDENTITY,
        [UploadedByMemberID] int NOT NULL,
        [ImageURL] nvarchar(500) NOT NULL,
        [ImageType] nvarchar(30) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        [UploadedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        CONSTRAINT [PK_Images] PRIMARY KEY ([ImageID]),
        CONSTRAINT [CK_Images_ImageType] CHECK ([ImageType] IN ('RestaurantCover', 'RestaurantEnvironment', 'ReviewImage', 'MemberAvatar'))
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [Members] (
        [MemberID] int NOT NULL IDENTITY,
        [UserName] nvarchar(50) NOT NULL,
        [NickName] nvarchar(50) NULL,
        [Email] nvarchar(100) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Role] nvarchar(10) NOT NULL DEFAULT N'User',
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [IsLocked] bit NOT NULL DEFAULT CAST(0 AS bit),
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Status] nvarchar(20) NOT NULL DEFAULT N'Normal',
        [AdminNote] nvarchar(max) NULL,
        [PenaltyEndAt] datetime2 NULL,
        [Birthday] date NULL,
        [LevelID] int NOT NULL DEFAULT 1,
        [Experience] int NOT NULL DEFAULT 0,
        [Points] int NOT NULL DEFAULT 0,
        [AvatarImageID] int NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        CONSTRAINT [PK_Members] PRIMARY KEY ([MemberID]),
        CONSTRAINT [CK_Members_Experience] CHECK ([Experience] >= 0),
        CONSTRAINT [CK_Members_Points] CHECK ([Points] >= 0),
        CONSTRAINT [CK_Members_Role] CHECK ([Role] IN ('User', 'Admin')),
        CONSTRAINT [CK_Members_Status] CHECK ([Status] IN ('Normal', 'Warning', 'Muted', 'Suspended', 'Deleted')),
        CONSTRAINT [FK_Members_Images_AvatarImageID] FOREIGN KEY ([AvatarImageID]) REFERENCES [Images] ([ImageID]),
        CONSTRAINT [FK_Members_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Members_UserLevels_LevelID] FOREIGN KEY ([LevelID]) REFERENCES [UserLevels] ([LevelID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [NotificationID] int NOT NULL IDENTITY,
        [MemberID] int NULL,
        [NotificationType] nvarchar(20) NOT NULL DEFAULT N'Personal',
        [TargetRole] nvarchar(10) NULL,
        [TargetStatus] nvarchar(20) NULL,
        [TargetLevelID] int NULL,
        [Title] nvarchar(100) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ScheduledAt] datetime2 NOT NULL,
        [SentAt] datetime2 NULL,
        [IsSent] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [CreatedBy] int NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationID]),
        CONSTRAINT [CK_Notifications_NotificationType] CHECK ([NotificationType] IN ('Personal', 'Condition')),
        CONSTRAINT [CK_Notifications_TargetRole] CHECK ([TargetRole] IS NULL OR [TargetRole] IN ('User', 'Admin')),
        CONSTRAINT [CK_Notifications_TargetStatus] CHECK ([TargetStatus] IS NULL OR [TargetStatus] IN ('Normal', 'Warning', 'Muted', 'Suspended', 'Deleted')),
        CONSTRAINT [FK_Notifications_Members_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Notifications_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Notifications_Members_MemberID] FOREIGN KEY ([MemberID]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Notifications_UserLevels_TargetLevelID] FOREIGN KEY ([TargetLevelID]) REFERENCES [UserLevels] ([LevelID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [Restaurants] (
        [RestaurantID] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [City] nvarchar(20) NOT NULL,
        [District] nvarchar(20) NOT NULL,
        [DetailedAddress] nvarchar(200) NOT NULL,
        [Phone] nvarchar(20) NULL,
        [Note] nvarchar(1000) NULL,
        [Latitude] decimal(9,6) NULL,
        [Longitude] decimal(9,6) NULL,
        [MemberID] int NOT NULL,
        [AverageRating] decimal(3,2) NOT NULL DEFAULT 0.0,
        [ReviewCount] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        [DeleteReason] nvarchar(200) NULL,
        CONSTRAINT [PK_Restaurants] PRIMARY KEY ([RestaurantID]),
        CONSTRAINT [CK_Restaurants_AverageRating] CHECK ([AverageRating] >= 0 AND [AverageRating] <= 5),
        CONSTRAINT [CK_Restaurants_Latitude] CHECK ([Latitude] IS NULL OR ([Latitude] >= -90 AND [Latitude] <= 90)),
        CONSTRAINT [CK_Restaurants_Longitude] CHECK ([Longitude] IS NULL OR ([Longitude] >= -180 AND [Longitude] <= 180)),
        CONSTRAINT [CK_Restaurants_ReviewCount] CHECK ([ReviewCount] >= 0),
        CONSTRAINT [FK_Restaurants_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Restaurants_Members_MemberID] FOREIGN KEY ([MemberID]) REFERENCES [Members] ([MemberID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [Tags] (
        [TagID] int NOT NULL IDENTITY,
        [TagName] nvarchar(50) NOT NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        CONSTRAINT [PK_Tags] PRIMARY KEY ([TagID]),
        CONSTRAINT [FK_Tags_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [RestaurantImages] (
        [RestaurantID] int NOT NULL,
        [ImageID] int NOT NULL,
        CONSTRAINT [PK_RestaurantImages] PRIMARY KEY ([RestaurantID], [ImageID]),
        CONSTRAINT [FK_RestaurantImages_Images_ImageID] FOREIGN KEY ([ImageID]) REFERENCES [Images] ([ImageID]),
        CONSTRAINT [FK_RestaurantImages_Restaurants_RestaurantID] FOREIGN KEY ([RestaurantID]) REFERENCES [Restaurants] ([RestaurantID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [ReviewID] int NOT NULL IDENTITY,
        [MemberID] int NOT NULL,
        [RestaurantID] int NOT NULL,
        [Rating] int NOT NULL,
        [Content] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Active',
        [ReportCount] int NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([ReviewID]),
        CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] >= 1 AND [Rating] <= 5),
        CONSTRAINT [CK_Reviews_ReportCount] CHECK ([ReportCount] >= 0),
        CONSTRAINT [CK_Reviews_Status] CHECK ([Status] IN ('Active', 'PendingReview')),
        CONSTRAINT [FK_Reviews_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Reviews_Members_MemberID] FOREIGN KEY ([MemberID]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Reviews_Restaurants_RestaurantID] FOREIGN KEY ([RestaurantID]) REFERENCES [Restaurants] ([RestaurantID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [RestaurantTags] (
        [RestaurantID] int NOT NULL,
        [TagID] int NOT NULL,
        CONSTRAINT [PK_RestaurantTags] PRIMARY KEY ([RestaurantID], [TagID]),
        CONSTRAINT [FK_RestaurantTags_Restaurants_RestaurantID] FOREIGN KEY ([RestaurantID]) REFERENCES [Restaurants] ([RestaurantID]),
        CONSTRAINT [FK_RestaurantTags_Tags_TagID] FOREIGN KEY ([TagID]) REFERENCES [Tags] ([TagID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [Reports] (
        [ReportID] int NOT NULL IDENTITY,
        [ReporterMemberID] int NOT NULL,
        [ReportedMemberID] int NULL,
        [RestaurantID] int NULL,
        [ReviewID] int NULL,
        [ImageID] int NULL,
        [Reason] nvarchar(500) NOT NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT N'Pending',
        [CreatedAt] datetime2 NOT NULL DEFAULT (GETDATE()),
        [HandledAt] datetime2 NULL,
        [HandledByMemberID] int NULL,
        [AdminNote] nvarchar(max) NULL,
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        [DeletedAt] datetime2 NULL,
        [DeletedBy] int NULL,
        CONSTRAINT [PK_Reports] PRIMARY KEY ([ReportID]),
        CONSTRAINT [CK_Reports_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected')),
        CONSTRAINT [FK_Reports_Images_ImageID] FOREIGN KEY ([ImageID]) REFERENCES [Images] ([ImageID]),
        CONSTRAINT [FK_Reports_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Reports_Members_HandledByMemberID] FOREIGN KEY ([HandledByMemberID]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Reports_Members_ReportedMemberID] FOREIGN KEY ([ReportedMemberID]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Reports_Members_ReporterMemberID] FOREIGN KEY ([ReporterMemberID]) REFERENCES [Members] ([MemberID]),
        CONSTRAINT [FK_Reports_Restaurants_RestaurantID] FOREIGN KEY ([RestaurantID]) REFERENCES [Restaurants] ([RestaurantID]),
        CONSTRAINT [FK_Reports_Reviews_ReviewID] FOREIGN KEY ([ReviewID]) REFERENCES [Reviews] ([ReviewID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE TABLE [ReviewImages] (
        [ReviewID] int NOT NULL,
        [ImageID] int NOT NULL,
        CONSTRAINT [PK_ReviewImages] PRIMARY KEY ([ReviewID], [ImageID]),
        CONSTRAINT [FK_ReviewImages_Images_ImageID] FOREIGN KEY ([ImageID]) REFERENCES [Images] ([ImageID]),
        CONSTRAINT [FK_ReviewImages_Reviews_ReviewID] FOREIGN KEY ([ReviewID]) REFERENCES [Reviews] ([ReviewID])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BusinessHours_RestaurantID] ON [BusinessHours] ([RestaurantID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FavoriteFolders_DeletedBy] ON [FavoriteFolders] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FavoriteFolders_IsDeleted] ON [FavoriteFolders] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FavoriteFolders_MemberID] ON [FavoriteFolders] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Favorites_DeletedBy] ON [Favorites] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Favorites_FavoriteFolderID] ON [Favorites] ([FavoriteFolderID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Favorites_IsDeleted] ON [Favorites] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Favorites_MemberID] ON [Favorites] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Favorites_RestaurantID] ON [Favorites] ([RestaurantID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Images_DeletedBy] ON [Images] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Images_ImageType] ON [Images] ([ImageType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Images_IsDeleted] ON [Images] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Images_UploadedByMemberID] ON [Images] ([UploadedByMemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Members_AvatarImageID] ON [Members] ([AvatarImageID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Members_DeletedBy] ON [Members] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Members_Email] ON [Members] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Members_IsDeleted] ON [Members] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Members_LevelID] ON [Members] ([LevelID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Members_UserName] ON [Members] ([UserName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_CreatedBy] ON [Notifications] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_DeletedBy] ON [Notifications] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_IsDeleted] ON [Notifications] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_IsSent] ON [Notifications] ([IsSent]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_MemberID] ON [Notifications] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_NotificationType] ON [Notifications] ([NotificationType]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_TargetLevelID] ON [Notifications] ([TargetLevelID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_DeletedBy] ON [Reports] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_HandledByMemberID] ON [Reports] ([HandledByMemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_ImageID] ON [Reports] ([ImageID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_IsDeleted] ON [Reports] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_ReportedMemberID] ON [Reports] ([ReportedMemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_ReporterMemberID] ON [Reports] ([ReporterMemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_RestaurantID] ON [Reports] ([RestaurantID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_ReviewID] ON [Reports] ([ReviewID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reports_Status] ON [Reports] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RestaurantImages_ImageID] ON [RestaurantImages] ([ImageID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Restaurants_City] ON [Restaurants] ([City]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Restaurants_DeletedBy] ON [Restaurants] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Restaurants_District] ON [Restaurants] ([District]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Restaurants_IsDeleted] ON [Restaurants] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Restaurants_MemberID] ON [Restaurants] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RestaurantTags_TagID] ON [RestaurantTags] ([TagID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ReviewImages_ImageID] ON [ReviewImages] ([ImageID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_DeletedBy] ON [Reviews] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_IsDeleted] ON [Reviews] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_MemberID] ON [Reviews] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_RestaurantID] ON [Reviews] ([RestaurantID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_Status] ON [Reviews] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tags_DeletedBy] ON [Tags] ([DeletedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Tags_IsDeleted] ON [Tags] ([IsDeleted]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tags_TagName] ON [Tags] ([TagName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserLevels_LevelName] ON [UserLevels] ([LevelName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_UserLevels_MinExp] ON [UserLevels] ([MinExp]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    ALTER TABLE [BusinessHours] ADD CONSTRAINT [FK_BusinessHours_Restaurants_RestaurantID] FOREIGN KEY ([RestaurantID]) REFERENCES [Restaurants] ([RestaurantID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    ALTER TABLE [FavoriteFolders] ADD CONSTRAINT [FK_FavoriteFolders_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    ALTER TABLE [FavoriteFolders] ADD CONSTRAINT [FK_FavoriteFolders_Members_MemberID] FOREIGN KEY ([MemberID]) REFERENCES [Members] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    ALTER TABLE [Favorites] ADD CONSTRAINT [FK_Favorites_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    ALTER TABLE [Favorites] ADD CONSTRAINT [FK_Favorites_Members_MemberID] FOREIGN KEY ([MemberID]) REFERENCES [Members] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    ALTER TABLE [Favorites] ADD CONSTRAINT [FK_Favorites_Restaurants_RestaurantID] FOREIGN KEY ([RestaurantID]) REFERENCES [Restaurants] ([RestaurantID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    ALTER TABLE [Images] ADD CONSTRAINT [FK_Images_Members_DeletedBy] FOREIGN KEY ([DeletedBy]) REFERENCES [Members] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    ALTER TABLE [Images] ADD CONSTRAINT [FK_Images_Members_UploadedByMemberID] FOREIGN KEY ([UploadedByMemberID]) REFERENCES [Members] ([MemberID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260707020329_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260707020329_InitialCreate', N'8.0.22');
END;
GO

COMMIT;
GO
BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715153559_AddCategoryToReports'
)
BEGIN
    ALTER TABLE [Reports] ADD [Category] nvarchar(10) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715153559_AddCategoryToReports'
)
BEGIN
    UPDATE [Reports] SET [Category] = N'未分類' WHERE [Category] IS NULL
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715153559_AddCategoryToReports'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reports]') AND [c].[name] = N'Category');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Reports] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Reports] ALTER COLUMN [Category] nvarchar(10) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715153559_AddCategoryToReports'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260715153559_AddCategoryToReports', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    IF EXISTS (
        SELECT 1
        FROM [Notifications]
        WHERE [CreatedBy] IS NULL
           OR LEN([Content]) > 1000
           OR ([NotificationType] = 'Personal' AND ([MemberID] IS NULL OR [TargetRole] IS NOT NULL OR [TargetStatus] IS NOT NULL OR [TargetLevelID] IS NOT NULL))
           OR ([NotificationType] = 'Condition' AND [MemberID] IS NOT NULL)
           OR ([IsSent] = 0 AND [SentAt] IS NOT NULL)
           OR ([IsSent] = 1 AND [SentAt] IS NULL)
           OR ([IsDeleted] = 0 AND ([DeletedAt] IS NOT NULL OR [DeletedBy] IS NOT NULL))
           OR ([IsDeleted] = 1 AND ([IsSent] = 1 OR [DeletedAt] IS NULL OR [DeletedBy] IS NULL))
    )
        THROW 51000, 'NotificationModuleV2 preflight failed: remediate invalid notification data before applying this migration.', 1;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    DROP INDEX [IX_Notifications_IsDeleted] ON [Notifications];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    DROP INDEX [IX_Notifications_IsSent] ON [Notifications];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    ALTER TABLE [UserLevels] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    DROP INDEX [IX_Notifications_CreatedBy] ON [Notifications];
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Notifications]') AND [c].[name] = N'CreatedBy');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Notifications] DROP CONSTRAINT [' + @var1 + '];');
    EXEC(N'UPDATE [Notifications] SET [CreatedBy] = 0 WHERE [CreatedBy] IS NULL');
    ALTER TABLE [Notifications] ALTER COLUMN [CreatedBy] int NOT NULL;
    ALTER TABLE [Notifications] ADD DEFAULT 0 FOR [CreatedBy];
    CREATE INDEX [IX_Notifications_CreatedBy] ON [Notifications] ([CreatedBy]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Notifications]') AND [c].[name] = N'CreatedAt');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Notifications] DROP CONSTRAINT [' + @var2 + '];');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Notifications]') AND [c].[name] = N'Content');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Notifications] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Notifications] ALTER COLUMN [Content] nvarchar(1000) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    ALTER TABLE [Notifications] ADD [RowVersion] rowversion NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    ALTER TABLE [Notifications] ADD [SourceReportID] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    ALTER TABLE [Notifications] ADD [SourceReportOutcome] nvarchar(10) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    CREATE INDEX [IX_Notifications_IsDeleted_ScheduledAt_NotificationID] ON [Notifications] ([IsDeleted], [ScheduledAt], [NotificationID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    CREATE INDEX [IX_Notifications_IsSent_IsDeleted_ScheduledAt_NotificationID] ON [Notifications] ([IsSent], [IsDeleted], [ScheduledAt], [NotificationID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Notifications_SourceReportID_SourceReportOutcome] ON [Notifications] ([SourceReportID], [SourceReportOutcome]) WHERE [SourceReportID] IS NOT NULL AND [SourceReportOutcome] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    EXEC(N'ALTER TABLE [Notifications] ADD CONSTRAINT [CK_Notifications_Audience] CHECK (([NotificationType] = ''Personal'' AND [MemberID] IS NOT NULL AND [TargetRole] IS NULL AND [TargetStatus] IS NULL AND [TargetLevelID] IS NULL) OR ([NotificationType] = ''Condition'' AND [MemberID] IS NULL))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    EXEC(N'ALTER TABLE [Notifications] ADD CONSTRAINT [CK_Notifications_DeletedState] CHECK (([IsDeleted] = 0 AND [DeletedAt] IS NULL AND [DeletedBy] IS NULL) OR ([IsDeleted] = 1 AND [IsSent] = 0 AND [DeletedAt] IS NOT NULL AND [DeletedBy] IS NOT NULL))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    EXEC(N'ALTER TABLE [Notifications] ADD CONSTRAINT [CK_Notifications_ReportSource] CHECK (([SourceReportID] IS NULL AND [SourceReportOutcome] IS NULL) OR ([SourceReportID] IS NOT NULL AND [SourceReportOutcome] IN (''Approved'', ''Rejected'')))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    EXEC(N'ALTER TABLE [Notifications] ADD CONSTRAINT [CK_Notifications_SentState] CHECK (([IsSent] = 0 AND [SentAt] IS NULL) OR ([IsSent] = 1 AND [SentAt] IS NOT NULL))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717033806_NotificationModuleV2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717033806_NotificationModuleV2', N'8.0.22');
END;
GO

COMMIT;
GO
