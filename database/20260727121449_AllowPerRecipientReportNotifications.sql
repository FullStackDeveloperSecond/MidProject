BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727121449_AllowPerRecipientReportNotifications'
)
BEGIN
    DROP INDEX [IX_Notifications_SourceReportID_SourceReportOutcome] ON [Notifications];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727121449_AllowPerRecipientReportNotifications'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Notifications_SourceReportID_SourceReportOutcome_MemberID]
        ON [Notifications] ([SourceReportID], [SourceReportOutcome], [MemberID])
        WHERE [SourceReportID] IS NOT NULL
          AND [SourceReportOutcome] IS NOT NULL
          AND [MemberID] IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727121449_AllowPerRecipientReportNotifications'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727121449_AllowPerRecipientReportNotifications', N'8.0.22');
END;
GO

COMMIT;
GO
