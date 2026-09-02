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
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [AIPromptTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [TemplateContent] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_AIPromptTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Organizations] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NULL,
        [Website] nvarchar(max) NULL,
        [CompanySize] nvarchar(max) NULL,
        [Domain] nvarchar(max) NULL,
        [IsDomainVerified] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Organizations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Permissions] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [Module] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(255) NOT NULL,
        [TemplateCode] nvarchar(50) NOT NULL,
        [Description] nvarchar(max) NULL,
        [DefaultNavigationConfig] nvarchar(max) NULL,
        CONSTRAINT [PK_ProjectTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Roles] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [SystemSettings] (
        [Id] uniqueidentifier NOT NULL,
        [Key] nvarchar(max) NOT NULL,
        [Value] nvarchar(max) NOT NULL,
        [SettingGroup] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [LastModifiedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TenantConfigs] (
        [Id] uniqueidentifier NOT NULL,
        [OrganizationName] nvarchar(255) NOT NULL,
        [Domain] nvarchar(255) NULL,
        [LogoUrl] nvarchar(1000) NULL,
        [Require2FA] bit NOT NULL,
        [AllowContact] bit NOT NULL,
        [PublicProfile] bit NOT NULL,
        [AllowedContactTopics] nvarchar(max) NULL,
        [IpWhitelist] nvarchar(max) NULL,
        CONSTRAINT [PK_TenantConfigs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [Email] nvarchar(450) NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [FullName] nvarchar(max) NOT NULL,
        [AvatarUrl] nvarchar(max) NULL,
        [CoverUrl] nvarchar(max) NULL,
        [Bio] nvarchar(max) NULL,
        [JobTitle] nvarchar(max) NULL,
        [Location] nvarchar(max) NULL,
        [Timezone] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [RefreshToken] nvarchar(max) NULL,
        [RefreshTokenExpiryTime] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [Is2FAEnabled] bit NOT NULL,
        [OrganizationId] nvarchar(450) NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Organizations_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [Organizations] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [RolePermissions] (
        [RoleId] uniqueidentifier NOT NULL,
        [PermissionId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
        CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [AIFeedbacks] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PromptContent] nvarchar(max) NOT NULL,
        [AIResponse] nvarchar(max) NOT NULL,
        [CorrectedResponse] nvarchar(max) NULL,
        [Rating] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AIFeedbacks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AIFeedbacks_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [AITokenUsages] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [FeatureCode] nvarchar(max) NOT NULL,
        [TokensUsed] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AITokenUsages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AITokenUsages_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [AITrainingDatasets] (
        [Id] uniqueidentifier NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [InputData] nvarchar(max) NOT NULL,
        [OutputData] nvarchar(max) NOT NULL,
        [IsApproved] bit NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AITrainingDatasets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AITrainingDatasets_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Comments] (
        [Id] uniqueidentifier NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(450) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [ParentCommentId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Comments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Comments_Comments_ParentCommentId] FOREIGN KEY ([ParentCommentId]) REFERENCES [Comments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Comments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Departments] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [ManagerId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [Require2FA] bit NOT NULL,
        [ParentId] uniqueidentifier NULL,
        [Description] nvarchar(max) NULL,
        [CoverImage] nvarchar(max) NULL,
        CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Departments_Departments_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Departments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Departments_Users_ManagerId] FOREIGN KEY ([ManagerId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [EntityFollowers] (
        [Id] uniqueidentifier NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(max) NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_EntityFollowers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EntityFollowers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [IntegrationAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(450) NOT NULL,
        [AccountEmail] nvarchar(max) NOT NULL,
        [ExternalAccountId] nvarchar(max) NULL,
        [AccessToken] nvarchar(max) NOT NULL,
        [RefreshToken] nvarchar(max) NULL,
        [AccessTokenExpiresAt] datetime2 NULL,
        [Scopes] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [LastSyncedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_IntegrationAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IntegrationAccounts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [LinkUrl] nvarchar(max) NULL,
        [IsRead] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [NotificationType] nvarchar(max) NOT NULL,
        [RelatedTaskId] uniqueidentifier NULL,
        [RelatedProjectId] uniqueidentifier NULL,
        [TriggeredByUserId] uniqueidentifier NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_Users_TriggeredByUserId] FOREIGN KEY ([TriggeredByUserId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Notifications_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [PerformanceReviews] (
        [Id] uniqueidentifier NOT NULL,
        [ReviewerId] uniqueidentifier NOT NULL,
        [RevieweeId] uniqueidentifier NOT NULL,
        [Score] float NOT NULL,
        [Feedback] nvarchar(max) NULL,
        [ReviewPeriod] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PerformanceReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PerformanceReviews_Users_RevieweeId] FOREIGN KEY ([RevieweeId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PerformanceReviews_Users_ReviewerId] FOREIGN KEY ([ReviewerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [RecentViews] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(64) NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [Title] nvarchar(512) NOT NULL,
        [Subtitle] nvarchar(512) NULL,
        [Url] nvarchar(1024) NULL,
        [Icon] nvarchar(128) NULL,
        [ViewedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_RecentViews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RecentViews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Token] nvarchar(500) NOT NULL,
        [DeviceId] nvarchar(255) NULL,
        [UserAgent] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ExpiryTime] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [SiteAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [EntityId] uniqueidentifier NOT NULL,
        [EntityType] nvarchar(450) NOT NULL,
        [Action] nvarchar(max) NOT NULL,
        [OldValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SiteAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SiteAuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [StickyNotes] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Color] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StickyNotes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StickyNotes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [SystemAuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NULL,
        [Action] nvarchar(max) NOT NULL,
        [Resource] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [IPAddress] nvarchar(max) NULL,
        [Details] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SystemAuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SystemAuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TaskDrafts] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [Title] nvarchar(255) NULL,
        [Description] nvarchar(max) NULL,
        [PayloadJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TaskDrafts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskDrafts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] uniqueidentifier NOT NULL,
        [RoleId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [UserWallets] (
        [UserId] uniqueidentifier NOT NULL,
        [TotalPoints] int NOT NULL,
        [Level] int NOT NULL,
        CONSTRAINT [PK_UserWallets] PRIMARY KEY ([UserId]),
        CONSTRAINT [FK_UserWallets_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Workspaces] (
        [Id] uniqueidentifier NOT NULL,
        [Slug] nvarchar(450) NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Logo] nvarchar(max) NULL,
        [Timezone] nvarchar(max) NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_Workspaces] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Workspaces_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [CommentAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [CommentId] uniqueidentifier NOT NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [FileUrl] nvarchar(max) NOT NULL,
        [ContentType] nvarchar(max) NOT NULL,
        [FileSize] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CommentAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommentAttachments_Comments_CommentId] FOREIGN KEY ([CommentId]) REFERENCES [Comments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CommentAttachments_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [CommentMentions] (
        [Id] uniqueidentifier NOT NULL,
        [CommentId] uniqueidentifier NOT NULL,
        [MentionedUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CommentMentions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CommentMentions_Comments_CommentId] FOREIGN KEY ([CommentId]) REFERENCES [Comments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CommentMentions_Users_MentionedUserId] FOREIGN KEY ([MentionedUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [DepartmentMembers] (
        [DepartmentId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        [LeftAt] datetime2 NULL,
        CONSTRAINT [PK_DepartmentMembers] PRIMARY KEY ([DepartmentId], [UserId]),
        CONSTRAINT [FK_DepartmentMembers_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DepartmentMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Kudos] (
        [Id] uniqueidentifier NOT NULL,
        [SenderId] uniqueidentifier NOT NULL,
        [ReceiverId] uniqueidentifier NULL,
        [DepartmentId] uniqueidentifier NULL,
        [Message] nvarchar(max) NOT NULL,
        [Icon] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Kudos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Kudos_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]),
        CONSTRAINT [FK_Kudos_Users_ReceiverId] FOREIGN KEY ([ReceiverId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Kudos_Users_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [SyncHistories] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [IntegrationAccountId] uniqueidentifier NULL,
        [Provider] nvarchar(450) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [ItemsImported] int NOT NULL,
        [Message] nvarchar(max) NULL,
        [StartedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_SyncHistories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SyncHistories_IntegrationAccounts_IntegrationAccountId] FOREIGN KEY ([IntegrationAccountId]) REFERENCES [IntegrationAccounts] ([Id]),
        CONSTRAINT [FK_SyncHistories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Goals] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Status] nvarchar(max) NOT NULL,
        [DueDate] datetime2 NULL,
        [Progress] int NOT NULL,
        [IsArchived] bit NOT NULL,
        [OwnerId] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [ParentGoalId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Goals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Goals_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Goals_Goals_ParentGoalId] FOREIGN KEY ([ParentGoalId]) REFERENCES [Goals] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Goals_Users_OwnerId] FOREIGN KEY ([OwnerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Goals_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Projects] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Identifier] nvarchar(450) NOT NULL,
        [IssueSequence] int NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [Why] nvarchar(max) NULL,
        [SuccessCriteria] nvarchar(max) NULL,
        [CloseDate] datetime2 NULL,
        [TrackedLinkUrl] nvarchar(max) NULL,
        [Status] bit NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        [IsArchived] bit NOT NULL,
        [ProjectTemplateId] uniqueidentifier NULL,
        [TemplateType] nvarchar(max) NULL,
        [NavigationConfig] nvarchar(max) NULL,
        [NetworkType] nvarchar(max) NOT NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Projects_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Projects_ProjectTemplates_ProjectTemplateId] FOREIGN KEY ([ProjectTemplateId]) REFERENCES [ProjectTemplates] ([Id]),
        CONSTRAINT [FK_Projects_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Projects_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [StarredItems] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [ItemType] nvarchar(max) NOT NULL,
        [ItemId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StarredItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StarredItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_StarredItems_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [WorkspaceMembers] (
        [WorkspaceId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [WorkspaceRole] nvarchar(max) NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_WorkspaceMembers] PRIMARY KEY ([WorkspaceId], [UserId]),
        CONSTRAINT [FK_WorkspaceMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_WorkspaceMembers_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [KudoReactions] (
        [Id] uniqueidentifier NOT NULL,
        [KudoId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ReactionType] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_KudoReactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KudoReactions_Kudos_KudoId] FOREIGN KEY ([KudoId]) REFERENCES [Kudos] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_KudoReactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [GoalDecisions] (
        [Id] uniqueidentifier NOT NULL,
        [GoalId] uniqueidentifier NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [DecisionDate] datetime2 NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GoalDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoalDecisions_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GoalDecisions_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [GoalLessons] (
        [Id] uniqueidentifier NOT NULL,
        [GoalId] uniqueidentifier NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GoalLessons] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoalLessons_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GoalLessons_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [GoalRisks] (
        [Id] uniqueidentifier NOT NULL,
        [GoalId] uniqueidentifier NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [Severity] nvarchar(max) NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GoalRisks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoalRisks_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GoalRisks_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [GoalUpdates] (
        [Id] uniqueidentifier NOT NULL,
        [GoalId] uniqueidentifier NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [OldStatus] nvarchar(max) NULL,
        [NewStatus] nvarchar(max) NULL,
        [OldProgress] int NULL,
        [NewProgress] int NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_GoalUpdates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GoalUpdates_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_GoalUpdates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TeamGoals] (
        [Id] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NOT NULL,
        [GoalId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TeamGoals] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TeamGoals_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TeamGoals_Goals_GoalId] FOREIGN KEY ([GoalId]) REFERENCES [Goals] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TeamGoals_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Labels] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [ColorCode] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ProjectId] uniqueidentifier NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Labels] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Labels_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Labels_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Modules] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [StartDate] datetime2 NULL,
        [TargetDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [LeadId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Modules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Modules_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Modules_Users_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Pages] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [UpdatedById] uniqueidentifier NULL,
        [SortOrder] int NOT NULL,
        [IsLocked] bit NOT NULL,
        [IsArchived] bit NOT NULL,
        [IsPrivate] bit NOT NULL,
        [IsStarred] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Pages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Pages_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Pages_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Pages_Users_UpdatedById] FOREIGN KEY ([UpdatedById]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectDecisions] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProjectDecisions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectDecisions_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectDecisions_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectDepartmentRoles] (
        [ProjectId] uniqueidentifier NOT NULL,
        [DepartmentId] uniqueidentifier NOT NULL,
        [RoleName] nvarchar(100) NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProjectDepartmentRoles] PRIMARY KEY ([ProjectId], [DepartmentId], [RoleName]),
        CONSTRAINT [FK_ProjectDepartmentRoles_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectDepartmentRoles_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectLessons] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProjectLessons] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectLessons_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectLessons_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectLinks] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [LinkedType] nvarchar(max) NOT NULL,
        [LinkedId] uniqueidentifier NULL,
        [TrackedUrl] nvarchar(max) NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProjectLinks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectLinks_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectLinks_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectMembers] (
        [ProjectId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ProjectRole] nvarchar(max) NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        [LeftAt] datetime2 NULL,
        [Status] bit NOT NULL,
        CONSTRAINT [PK_ProjectMembers] PRIMARY KEY ([ProjectId], [UserId]),
        CONSTRAINT [FK_ProjectMembers_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectRisks] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [Severity] nvarchar(max) NOT NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProjectRisks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectRisks_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectRisks_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectUpdates] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [OldStatus] nvarchar(max) NULL,
        [NewStatus] nvarchar(max) NULL,
        [CreatorId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProjectUpdates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectUpdates_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectUpdates_Users_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [ProjectViews] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [QueryMetadata] nvarchar(max) NOT NULL,
        [IsFavorite] bit NOT NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProjectViews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectViews_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectViews_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Sprints] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [Status] bit NOT NULL,
        [IsFavorite] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Sprints] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Sprints_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TaskStatuses] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [Name] nvarchar(max) NOT NULL,
        [ColorCode] nvarchar(max) NULL,
        [Position] int NOT NULL,
        CONSTRAINT [PK_TaskStatuses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskStatuses_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TaskTypes] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [Name] nvarchar(max) NOT NULL,
        [ColorCode] nvarchar(max) NULL,
        [Icon] nvarchar(max) NULL,
        CONSTRAINT [PK_TaskTypes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskTypes_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [WorkTasks] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SprintId] uniqueidentifier NULL,
        [ParentTaskId] uniqueidentifier NULL,
        [TaskTypeId] uniqueidentifier NOT NULL,
        [TaskStatusId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Priority] int NOT NULL,
        [StoryPoints] float NOT NULL,
        [PlannedStartDate] datetime2 NULL,
        [PlannedEndDate] datetime2 NULL,
        [ReporterId] uniqueidentifier NOT NULL,
        [AssignedUserId] uniqueidentifier NULL,
        [DueDate] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [IsDeleted] bit NOT NULL,
        [IsArchived] bit NOT NULL,
        [TotalEstimatedHours] float NOT NULL,
        [TotalActualHours] float NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [SortOrder] float NOT NULL,
        [SequenceId] nvarchar(max) NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_WorkTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_WorkTasks_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]),
        CONSTRAINT [FK_WorkTasks_Sprints_SprintId] FOREIGN KEY ([SprintId]) REFERENCES [Sprints] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_WorkTasks_TaskStatuses_TaskStatusId] FOREIGN KEY ([TaskStatusId]) REFERENCES [TaskStatuses] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_TaskTypes_TaskTypeId] FOREIGN KEY ([TaskTypeId]) REFERENCES [TaskTypes] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_Users_AssignedUserId] FOREIGN KEY ([AssignedUserId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_WorkTasks_Users_ReporterId] FOREIGN KEY ([ReporterId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_WorkTasks_WorkTasks_ParentTaskId] FOREIGN KEY ([ParentTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Attachments] (
        [Id] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [FileUrl] nvarchar(max) NOT NULL,
        [FileSize] bigint NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Attachments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Attachments_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [FieldChanged] nvarchar(max) NOT NULL,
        [OldValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AuditLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AuditLogs_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [InboxItems] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [IntegrationAccountId] uniqueidentifier NULL,
        [Source] nvarchar(450) NOT NULL,
        [Provider] nvarchar(450) NOT NULL,
        [ExternalId] nvarchar(450) NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Content] nvarchar(max) NULL,
        [Location] nvarchar(max) NULL,
        [StartsAt] datetime2 NULL,
        [EndsAt] datetime2 NULL,
        [IsRead] bit NOT NULL,
        [CreatedTaskId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_InboxItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InboxItems_IntegrationAccounts_IntegrationAccountId] FOREIGN KEY ([IntegrationAccountId]) REFERENCES [IntegrationAccounts] ([Id]),
        CONSTRAINT [FK_InboxItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_InboxItems_WorkTasks_CreatedTaskId] FOREIGN KEY ([CreatedTaskId]) REFERENCES [WorkTasks] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [Intakes] (
        [Id] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [Source] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SubmittedById] uniqueidentifier NULL,
        [ReviewedById] uniqueidentifier NULL,
        [CreatedIssueId] uniqueidentifier NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ReviewedAt] datetime2 NULL,
        CONSTRAINT [PK_Intakes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Intakes_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Intakes_Users_ReviewedById] FOREIGN KEY ([ReviewedById]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Intakes_Users_SubmittedById] FOREIGN KEY ([SubmittedById]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_Intakes_WorkTasks_CreatedIssueId] FOREIGN KEY ([CreatedIssueId]) REFERENCES [WorkTasks] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [IssueLabels] (
        [WorkTaskId] uniqueidentifier NOT NULL,
        [LabelId] uniqueidentifier NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_IssueLabels] PRIMARY KEY ([WorkTaskId], [LabelId]),
        CONSTRAINT [FK_IssueLabels_Labels_LabelId] FOREIGN KEY ([LabelId]) REFERENCES [Labels] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_IssueLabels_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [IssueModules] (
        [WorkTaskId] uniqueidentifier NOT NULL,
        [ModuleId] uniqueidentifier NOT NULL,
        [AssignedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_IssueModules] PRIMARY KEY ([WorkTaskId], [ModuleId]),
        CONSTRAINT [FK_IssueModules_Modules_ModuleId] FOREIGN KEY ([ModuleId]) REFERENCES [Modules] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_IssueModules_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [PointTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [UserWalletUserId] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NULL,
        [Amount] int NOT NULL,
        [Reason] nvarchar(max) NOT NULL,
        [TransactionType] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PointTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PointTransactions_UserWallets_UserWalletUserId] FOREIGN KEY ([UserWalletUserId]) REFERENCES [UserWallets] ([UserId]) ON DELETE CASCADE,
        CONSTRAINT [FK_PointTransactions_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TaskAssignments] (
        [WorkTaskId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Status] bit NOT NULL,
        [ProgressPercent] float NOT NULL,
        [ContributionWeight] float NOT NULL DEFAULT 1.0E0,
        [BlockedByUserId] uniqueidentifier NULL,
        [BlockReason] nvarchar(max) NULL,
        [ProgressUpdatedAt] datetime2 NULL,
        [Priority] int NOT NULL,
        [ActualStartDate] datetime2 NOT NULL,
        [ActualEndDate] datetime2 NOT NULL,
        [Description] nvarchar(max) NULL,
        [EstimatedHours] float NOT NULL,
        [TotalActualHours] float NOT NULL,
        CONSTRAINT [PK_TaskAssignments] PRIMARY KEY ([WorkTaskId], [UserId]),
        CONSTRAINT [FK_TaskAssignments_Users_BlockedByUserId] FOREIGN KEY ([BlockedByUserId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_TaskAssignments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskAssignments_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TaskDependencies] (
        [PredecessorTaskId] uniqueidentifier NOT NULL,
        [SuccessorTaskId] uniqueidentifier NOT NULL,
        [DependencyType] int NOT NULL,
        CONSTRAINT [PK_TaskDependencies] PRIMARY KEY ([PredecessorTaskId], [SuccessorTaskId]),
        CONSTRAINT [FK_TaskDependencies_WorkTasks_PredecessorTaskId] FOREIGN KEY ([PredecessorTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TaskDependencies_WorkTasks_SuccessorTaskId] FOREIGN KEY ([SuccessorTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TaskSubscribers] (
        [WorkTaskId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [SubscribedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TaskSubscribers] PRIMARY KEY ([WorkTaskId], [UserId]),
        CONSTRAINT [FK_TaskSubscribers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskSubscribers_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TaskVectorEmbeddings] (
        [WorkTaskId] uniqueidentifier NOT NULL,
        [VectorData] nvarchar(max) NOT NULL,
        [LastCalculatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TaskVectorEmbeddings] PRIMARY KEY ([WorkTaskId]),
        CONSTRAINT [FK_TaskVectorEmbeddings_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE TABLE [TimeLogs] (
        [Id] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Hours] float NOT NULL,
        [WorkType] nvarchar(max) NOT NULL,
        [Note] nvarchar(max) NULL,
        [LoggedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TimeLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TimeLogs_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_TimeLogs_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DefaultNavigationConfig', N'Description', N'Name', N'TemplateCode') AND [object_id] = OBJECT_ID(N'[ProjectTemplates]'))
        SET IDENTITY_INSERT [ProjectTemplates] ON;
    EXEC(N'INSERT INTO [ProjectTemplates] ([Id], [DefaultNavigationConfig], [Description], [Name], [TemplateCode])
    VALUES (''11111111-1111-1111-1111-111111111111'', NULL, N''Dành cho Helpdesk, cung cấp sẵn các Issue Types về Service Request, Incident.'', N''Basic IT service management'', N''IT_SERVICE''),
    (''22222222-2222-2222-2222-222222222222'', NULL, N''Dành cho Dev Team, cung cấp Scrum Board mặc định.'', N''Software Development'', N''SOFTWARE_DEV'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'DefaultNavigationConfig', N'Description', N'Name', N'TemplateCode') AND [object_id] = OBJECT_ID(N'[ProjectTemplates]'))
        SET IDENTITY_INSERT [ProjectTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AllowContact', N'AllowedContactTopics', N'Domain', N'IpWhitelist', N'LogoUrl', N'OrganizationName', N'PublicProfile', N'Require2FA') AND [object_id] = OBJECT_ID(N'[TenantConfigs]'))
        SET IDENTITY_INSERT [TenantConfigs] ON;
    EXEC(N'INSERT INTO [TenantConfigs] ([Id], [AllowContact], [AllowedContactTopics], [Domain], [IpWhitelist], [LogoUrl], [OrganizationName], [PublicProfile], [Require2FA])
    VALUES (''10000000-0000-0000-0000-000000000001'', CAST(1 AS bit), NULL, NULL, NULL, NULL, N''Global Organization'', CAST(0 AS bit), CAST(0 AS bit))');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'AllowContact', N'AllowedContactTopics', N'Domain', N'IpWhitelist', N'LogoUrl', N'OrganizationName', N'PublicProfile', N'Require2FA') AND [object_id] = OBJECT_ID(N'[TenantConfigs]'))
        SET IDENTITY_INSERT [TenantConfigs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_AIFeedbacks_UserId] ON [AIFeedbacks] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_AITokenUsages_UserId] ON [AITokenUsages] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_AITrainingDatasets_CreatorId] ON [AITrainingDatasets] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Attachments_UserId] ON [Attachments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Attachments_WorkTaskId] ON [Attachments] ([WorkTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_WorkTaskId] ON [AuditLogs] ([WorkTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_CommentAttachments_CommentId] ON [CommentAttachments] ([CommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_CommentAttachments_UploadedByUserId] ON [CommentAttachments] ([UploadedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_CommentMentions_CommentId] ON [CommentMentions] ([CommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_CommentMentions_MentionedUserId] ON [CommentMentions] ([MentionedUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Comments_EntityType_EntityId] ON [Comments] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Comments_ParentCommentId] ON [Comments] ([ParentCommentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Comments_UserId] ON [Comments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_DepartmentMembers_UserId] ON [DepartmentMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Departments_ManagerId] ON [Departments] ([ManagerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Departments_ParentId] ON [Departments] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_EntityFollowers_UserId] ON [EntityFollowers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_GoalDecisions_CreatorId] ON [GoalDecisions] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_GoalDecisions_GoalId] ON [GoalDecisions] ([GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_GoalLessons_CreatorId] ON [GoalLessons] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_GoalLessons_GoalId] ON [GoalLessons] ([GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_GoalRisks_CreatorId] ON [GoalRisks] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_GoalRisks_GoalId] ON [GoalRisks] ([GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Goals_DepartmentId] ON [Goals] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Goals_OwnerId] ON [Goals] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Goals_ParentGoalId] ON [Goals] ([ParentGoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Goals_WorkspaceId] ON [Goals] ([WorkspaceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_GoalUpdates_GoalId] ON [GoalUpdates] ([GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_GoalUpdates_UserId] ON [GoalUpdates] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_InboxItems_CreatedTaskId] ON [InboxItems] ([CreatedTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_InboxItems_IntegrationAccountId] ON [InboxItems] ([IntegrationAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_InboxItems_UserId_IsRead] ON [InboxItems] ([UserId], [IsRead]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_InboxItems_UserId_Provider_ExternalId] ON [InboxItems] ([UserId], [Provider], [ExternalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_InboxItems_UserId_Source_CreatedAt] ON [InboxItems] ([UserId], [Source], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Intakes_CreatedIssueId] ON [Intakes] ([CreatedIssueId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Intakes_ProjectId] ON [Intakes] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Intakes_ReviewedById] ON [Intakes] ([ReviewedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Intakes_SubmittedById] ON [Intakes] ([SubmittedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_IntegrationAccounts_UserId_Provider] ON [IntegrationAccounts] ([UserId], [Provider]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_IssueLabels_LabelId] ON [IssueLabels] ([LabelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_IssueModules_ModuleId] ON [IssueModules] ([ModuleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_KudoReactions_KudoId] ON [KudoReactions] ([KudoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_KudoReactions_UserId] ON [KudoReactions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Kudos_DepartmentId] ON [Kudos] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Kudos_ReceiverId] ON [Kudos] ([ReceiverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Kudos_SenderId] ON [Kudos] ([SenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Labels_ProjectId] ON [Labels] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Labels_WorkspaceId] ON [Labels] ([WorkspaceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Modules_LeadId] ON [Modules] ([LeadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Modules_ProjectId] ON [Modules] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Notifications_TriggeredByUserId] ON [Notifications] ([TriggeredByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId] ON [Notifications] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Pages_CreatedById] ON [Pages] ([CreatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Pages_ProjectId] ON [Pages] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Pages_UpdatedById] ON [Pages] ([UpdatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_PerformanceReviews_RevieweeId] ON [PerformanceReviews] ([RevieweeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_PerformanceReviews_ReviewerId] ON [PerformanceReviews] ([ReviewerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Permissions_Code] ON [Permissions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_PointTransactions_UserWalletUserId_WorkTaskId_TransactionType] ON [PointTransactions] ([UserWalletUserId], [WorkTaskId], [TransactionType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_PointTransactions_WorkTaskId] ON [PointTransactions] ([WorkTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectDecisions_CreatorId] ON [ProjectDecisions] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectDecisions_ProjectId] ON [ProjectDecisions] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectDepartmentRoles_DepartmentId] ON [ProjectDepartmentRoles] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectLessons_CreatorId] ON [ProjectLessons] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectLessons_ProjectId] ON [ProjectLessons] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectLinks_CreatorId] ON [ProjectLinks] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectLinks_ProjectId] ON [ProjectLinks] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectMembers_UserId] ON [ProjectMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectRisks_CreatorId] ON [ProjectRisks] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectRisks_ProjectId] ON [ProjectRisks] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Projects_CreatorId] ON [Projects] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Projects_DepartmentId] ON [Projects] ([DepartmentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Projects_ProjectTemplateId] ON [Projects] ([ProjectTemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Projects_WorkspaceId_Identifier] ON [Projects] ([WorkspaceId], [Identifier]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectUpdates_CreatorId] ON [ProjectUpdates] ([CreatorId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectUpdates_ProjectId] ON [ProjectUpdates] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectViews_CreatedById] ON [ProjectViews] ([CreatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_ProjectViews_ProjectId] ON [ProjectViews] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RecentViews_UserId_EntityType_EntityId] ON [RecentViews] ([UserId], [EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_SiteAuditLogs_EntityType_EntityId] ON [SiteAuditLogs] ([EntityType], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_SiteAuditLogs_UserId] ON [SiteAuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Sprints_ProjectId] ON [Sprints] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_StarredItems_UserId] ON [StarredItems] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_StarredItems_WorkspaceId] ON [StarredItems] ([WorkspaceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_StickyNotes_UserId] ON [StickyNotes] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_SyncHistories_IntegrationAccountId] ON [SyncHistories] ([IntegrationAccountId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_SyncHistories_UserId_Provider_StartedAt] ON [SyncHistories] ([UserId], [Provider], [StartedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_SystemAuditLogs_UserId] ON [SystemAuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_BlockedByUserId] ON [TaskAssignments] ([BlockedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_UserId] ON [TaskAssignments] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TaskDependencies_SuccessorTaskId] ON [TaskDependencies] ([SuccessorTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TaskDrafts_UserId_ProjectId_UpdatedAt] ON [TaskDrafts] ([UserId], [ProjectId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TaskDrafts_UserId_UpdatedAt] ON [TaskDrafts] ([UserId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TaskStatuses_ProjectId] ON [TaskStatuses] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TaskSubscribers_UserId] ON [TaskSubscribers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TaskTypes_ProjectId] ON [TaskTypes] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TeamGoals_CreatedByUserId] ON [TeamGoals] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TeamGoals_DepartmentId_GoalId] ON [TeamGoals] ([DepartmentId], [GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TeamGoals_GoalId] ON [TeamGoals] ([GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TimeLogs_UserId] ON [TimeLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_TimeLogs_WorkTaskId] ON [TimeLogs] ([WorkTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Users_OrganizationId] ON [Users] ([OrganizationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkspaceMembers_UserId] ON [WorkspaceMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_Workspaces_OwnerId] ON [Workspaces] ([OwnerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Workspaces_Slug] ON [Workspaces] ([Slug]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_AssignedUserId] ON [WorkTasks] ([AssignedUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_ParentTaskId] ON [WorkTasks] ([ParentTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_ProjectId_IsDeleted] ON [WorkTasks] ([ProjectId], [IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_ReporterId] ON [WorkTasks] ([ReporterId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_SortOrder] ON [WorkTasks] ([SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_SprintId] ON [WorkTasks] ([SprintId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_TaskStatusId] ON [WorkTasks] ([TaskStatusId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_TaskTypeId] ON [WorkTasks] ([TaskTypeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    CREATE INDEX [IX_WorkTasks_WorkspaceId_ProjectId] ON [WorkTasks] ([WorkspaceId], [ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260702034844_PlaneRenovation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260702034844_PlaneRenovation', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260705122440_UpdateRolePermissionEntities'
)
BEGIN
    ALTER TABLE [Roles] ADD [IsProtected] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260705122440_UpdateRolePermissionEntities'
)
BEGIN
    ALTER TABLE [Permissions] ADD [Description] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260705122440_UpdateRolePermissionEntities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260705122440_UpdateRolePermissionEntities', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708132000_AddIntakeRequestFields'
)
BEGIN
    ALTER TABLE [Intakes] ADD [Priority] int NOT NULL DEFAULT 3;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708132000_AddIntakeRequestFields'
)
BEGIN
    ALTER TABLE [Intakes] ADD [DesiredDueDate] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260708132000_AddIntakeRequestFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260708132000_AddIntakeRequestFields', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709073448_AddChatTables'
)
BEGIN
    CREATE TABLE [ChannelMessages] (
        [Id] uniqueidentifier NOT NULL,
        [ChannelId] uniqueidentifier NOT NULL,
        [SenderId] uniqueidentifier NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [AttachmentUrl] nvarchar(max) NULL,
        CONSTRAINT [PK_ChannelMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChannelMessages_Departments_ChannelId] FOREIGN KEY ([ChannelId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChannelMessages_Users_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709073448_AddChatTables'
)
BEGIN
    CREATE TABLE [DirectMessages] (
        [Id] uniqueidentifier NOT NULL,
        [SenderId] uniqueidentifier NOT NULL,
        [ReceiverId] uniqueidentifier NOT NULL,
        [Content] nvarchar(max) NOT NULL,
        [SentAt] datetime2 NOT NULL,
        [IsRead] bit NOT NULL,
        [AttachmentUrl] nvarchar(max) NULL,
        CONSTRAINT [PK_DirectMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DirectMessages_Users_ReceiverId] FOREIGN KEY ([ReceiverId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DirectMessages_Users_SenderId] FOREIGN KEY ([SenderId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709073448_AddChatTables'
)
BEGIN
    CREATE INDEX [IX_ChannelMessages_ChannelId] ON [ChannelMessages] ([ChannelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709073448_AddChatTables'
)
BEGIN
    CREATE INDEX [IX_ChannelMessages_SenderId] ON [ChannelMessages] ([SenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709073448_AddChatTables'
)
BEGIN
    CREATE INDEX [IX_DirectMessages_ReceiverId] ON [DirectMessages] ([ReceiverId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709073448_AddChatTables'
)
BEGIN
    CREATE INDEX [IX_DirectMessages_SenderId] ON [DirectMessages] ([SenderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709073448_AddChatTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709073448_AddChatTables', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709143300_AddProjectCustomFields'
)
BEGIN
    CREATE TABLE [CustomFieldDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Key] nvarchar(450) NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [IsRequired] bit NOT NULL,
        [OptionsJson] nvarchar(max) NULL,
        [IsVisible] bit NOT NULL,
        [SortOrder] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomFieldDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomFieldDefinitions_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709143300_AddProjectCustomFields'
)
BEGIN
    CREATE TABLE [CustomFieldValues] (
        [Id] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NOT NULL,
        [FieldDefinitionId] uniqueidentifier NOT NULL,
        [Value] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CustomFieldValues] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomFieldValues_CustomFieldDefinitions_FieldDefinitionId] FOREIGN KEY ([FieldDefinitionId]) REFERENCES [CustomFieldDefinitions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CustomFieldValues_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709143300_AddProjectCustomFields'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CustomFieldDefinitions_ProjectId_Key] ON [CustomFieldDefinitions] ([ProjectId], [Key]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709143300_AddProjectCustomFields'
)
BEGIN
    CREATE INDEX [IX_CustomFieldValues_FieldDefinitionId] ON [CustomFieldValues] ([FieldDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709143300_AddProjectCustomFields'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomFieldValues_WorkTaskId_FieldDefinitionId] ON [CustomFieldValues] ([WorkTaskId], [FieldDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260709143300_AddProjectCustomFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260709143300_AddProjectCustomFields', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711064059_FixStarredItemTypeConstraint'
)
BEGIN
    DELETE FROM [StarredItems] WHERE [ItemType] NOT IN ('Goal', 'Project', 'Team', 'User');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711064059_FixStarredItemTypeConstraint'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StarredItems]') AND [c].[name] = N'ItemType');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [StarredItems] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [StarredItems] ALTER COLUMN [ItemType] nvarchar(64) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711064059_FixStarredItemTypeConstraint'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StarredItems_UserId_WorkspaceId_ItemType_ItemId] ON [StarredItems] ([UserId], [WorkspaceId], [ItemType], [ItemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711064059_FixStarredItemTypeConstraint'
)
BEGIN
    EXEC(N'ALTER TABLE [StarredItems] ADD CONSTRAINT [CK_StarredItems_ItemType] CHECK ([ItemType] IN (''Goal'', ''Project'', ''Team'', ''User''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711064059_FixStarredItemTypeConstraint'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260711064059_FixStarredItemTypeConstraint', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711074250_AddRoleAndPermissionEnhancements'
)
BEGIN
    ALTER TABLE [Roles] ADD [Badge] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711074250_AddRoleAndPermissionEnhancements'
)
BEGIN
    ALTER TABLE [Permissions] ADD [DependencyJson] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711074250_AddRoleAndPermissionEnhancements'
)
BEGIN
    ALTER TABLE [Permissions] ADD [IsDefault] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711074250_AddRoleAndPermissionEnhancements'
)
BEGIN
    ALTER TABLE [Permissions] ADD [RiskLevel] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260711074250_AddRoleAndPermissionEnhancements'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260711074250_AddRoleAndPermissionEnhancements', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    ALTER TABLE [Projects] ADD [CoverAltText] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    ALTER TABLE [Projects] ADD [CoverUrl] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE TABLE [AiCreditRules] (
        [Id] uniqueidentifier NOT NULL,
        [ActionType] nvarchar(128) NOT NULL,
        [EstimatedCredits] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Disclaimer] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiCreditRules] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE TABLE [AiPricingPlans] (
        [Id] uniqueidentifier NOT NULL,
        [Code] nvarchar(64) NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [MonthlyPriceVnd] decimal(18,2) NULL,
        [PerUser] bit NOT NULL,
        [IncludedUsers] int NULL,
        [IncludedAiCredits] int NOT NULL,
        [ExtraAiCreditsEnabled] bit NOT NULL,
        [PricingStatus] nvarchar(64) NOT NULL,
        [FeaturesJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiPricingPlans] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE TABLE [AiUsageLedgerEntries] (
        [Id] uniqueidentifier NOT NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [ActionType] nvarchar(max) NOT NULL,
        [CreditsConsumed] int NOT NULL,
        [ProviderTokens] bigint NULL,
        [IdempotencyKey] nvarchar(450) NULL,
        [OccurredAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiUsageLedgerEntries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiUsageLedgerEntries_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_AiUsageLedgerEntries_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AiUsageLedgerEntries_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE TABLE [NotificationPreferences] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Category] nvarchar(450) NOT NULL,
        [InAppEnabled] bit NOT NULL,
        [EmailEnabled] bit NOT NULL,
        [Priority] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_NotificationPreferences_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE TABLE [TaskContingencyPlans] (
        [Id] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NOT NULL,
        [Risk] nvarchar(max) NOT NULL,
        [Cause] nvarchar(max) NULL,
        [ResponsePlan] nvarchar(max) NOT NULL,
        [SupportPersonId] uniqueidentifier NULL,
        [ReplacementDeadline] datetime2 NULL,
        [ImpactLevel] nvarchar(max) NOT NULL,
        [TriggerCondition] nvarchar(max) NULL,
        [Status] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] uniqueidentifier NOT NULL,
        [UpdatedById] uniqueidentifier NOT NULL,
        CONSTRAINT [PK_TaskContingencyPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskContingencyPlans_Users_CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_TaskContingencyPlans_Users_SupportPersonId] FOREIGN KEY ([SupportPersonId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_TaskContingencyPlans_Users_UpdatedById] FOREIGN KEY ([UpdatedById]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_TaskContingencyPlans_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiCreditRules_ActionType] ON [AiCreditRules] ([ActionType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiPricingPlans_Code] ON [AiPricingPlans] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AiUsageLedgerEntries_IdempotencyKey] ON [AiUsageLedgerEntries] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE INDEX [IX_AiUsageLedgerEntries_ProjectId] ON [AiUsageLedgerEntries] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE INDEX [IX_AiUsageLedgerEntries_UserId_OccurredAt] ON [AiUsageLedgerEntries] ([UserId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE INDEX [IX_AiUsageLedgerEntries_WorkspaceId_OccurredAt] ON [AiUsageLedgerEntries] ([WorkspaceId], [OccurredAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationPreferences_UserId_Category] ON [NotificationPreferences] ([UserId], [Category]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE INDEX [IX_TaskContingencyPlans_CreatedById] ON [TaskContingencyPlans] ([CreatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE INDEX [IX_TaskContingencyPlans_SupportPersonId] ON [TaskContingencyPlans] ([SupportPersonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE INDEX [IX_TaskContingencyPlans_UpdatedById] ON [TaskContingencyPlans] ([UpdatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    CREATE INDEX [IX_TaskContingencyPlans_WorkTaskId_Status] ON [TaskContingencyPlans] ([WorkTaskId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713044508_AddTerraAiUsagePricingAndContingency'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260713044508_AddTerraAiUsagePricingAndContingency', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713100122_AddContingencyPlan'
)
BEGIN
    CREATE TABLE [ContingencyPlans] (
        [Id] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NOT NULL,
        [Name] nvarchar(200) NOT NULL DEFAULT N'',
        [RiskLevel] nvarchar(50) NOT NULL,
        [RiskStatus] nvarchar(50) NOT NULL,
        [ActivationCondition] nvarchar(500) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [ContingencyTaskId] uniqueidentifier NULL,
        [IsActivated] bit NOT NULL DEFAULT CAST(0 AS bit),
        [ActivatedById] uniqueidentifier NULL,
        [ActivatedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedBy] uniqueidentifier NULL,
        [UpdatedBy] uniqueidentifier NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ContingencyPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContingencyPlans_Users_ActivatedById] FOREIGN KEY ([ActivatedById]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ContingencyPlans_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ContingencyPlans_WorkTasks_ContingencyTaskId] FOREIGN KEY ([ContingencyTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713100122_AddContingencyPlan'
)
BEGIN
    CREATE INDEX [IX_ContingencyPlans_ActivatedById] ON [ContingencyPlans] ([ActivatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713100122_AddContingencyPlan'
)
BEGIN
    CREATE INDEX [IX_ContingencyPlans_ContingencyTaskId] ON [ContingencyPlans] ([ContingencyTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713100122_AddContingencyPlan'
)
BEGIN
    CREATE INDEX [IX_ContingencyPlans_WorkTaskId] ON [ContingencyPlans] ([WorkTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260713100122_AddContingencyPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260713100122_AddContingencyPlan', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714074108_UpdateContingencyPlanSchema'
)
BEGIN
    DECLARE @var1 nvarchar(max);
    SELECT @var1 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContingencyPlans]') AND [c].[name] = N'IsActivated');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ContingencyPlans] DROP CONSTRAINT ' + @var1 + ';');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714074108_UpdateContingencyPlanSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260714074108_UpdateContingencyPlanSchema', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    ALTER TABLE [ContingencyPlans] DROP CONSTRAINT [FK_ContingencyPlans_Users_ActivatedById];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    ALTER TABLE [ContingencyPlans] DROP CONSTRAINT [FK_ContingencyPlans_WorkTasks_ContingencyTaskId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    DROP INDEX [IX_ContingencyPlans_ActivatedById] ON [ContingencyPlans];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    DROP INDEX [IX_ContingencyPlans_ContingencyTaskId] ON [ContingencyPlans];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    DECLARE @var2 nvarchar(max);
    SELECT @var2 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContingencyPlans]') AND [c].[name] = N'ActivatedAt');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ContingencyPlans] DROP CONSTRAINT ' + @var2 + ';');
    ALTER TABLE [ContingencyPlans] DROP COLUMN [ActivatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    DECLARE @var3 nvarchar(max);
    SELECT @var3 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContingencyPlans]') AND [c].[name] = N'ActivatedById');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ContingencyPlans] DROP CONSTRAINT ' + @var3 + ';');
    ALTER TABLE [ContingencyPlans] DROP COLUMN [ActivatedById];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    DECLARE @var4 nvarchar(max);
    SELECT @var4 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContingencyPlans]') AND [c].[name] = N'ActivationCondition');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [ContingencyPlans] DROP CONSTRAINT ' + @var4 + ';');
    ALTER TABLE [ContingencyPlans] DROP COLUMN [ActivationCondition];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    DECLARE @var5 nvarchar(max);
    SELECT @var5 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContingencyPlans]') AND [c].[name] = N'ContingencyTaskId');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [ContingencyPlans] DROP CONSTRAINT ' + @var5 + ';');
    ALTER TABLE [ContingencyPlans] DROP COLUMN [ContingencyTaskId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    DECLARE @var6 nvarchar(max);
    SELECT @var6 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContingencyPlans]') AND [c].[name] = N'IsActivated');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [ContingencyPlans] DROP CONSTRAINT ' + @var6 + ';');
    ALTER TABLE [ContingencyPlans] DROP COLUMN [IsActivated];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    DECLARE @var7 nvarchar(max);
    SELECT @var7 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContingencyPlans]') AND [c].[name] = N'RiskStatus');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [ContingencyPlans] DROP CONSTRAINT ' + @var7 + ';');
    ALTER TABLE [ContingencyPlans] DROP COLUMN [RiskStatus];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    CREATE TABLE [ContingencyPlanTasks] (
        [Id] uniqueidentifier NOT NULL,
        [ContingencyPlanId] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NOT NULL,
        [IsActivated] bit NOT NULL,
        [ActivatedById] uniqueidentifier NULL,
        [ActivatedAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ContingencyPlanTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ContingencyPlanTasks_ContingencyPlans_ContingencyPlanId] FOREIGN KEY ([ContingencyPlanId]) REFERENCES [ContingencyPlans] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ContingencyPlanTasks_Users_ActivatedById] FOREIGN KEY ([ActivatedById]) REFERENCES [Users] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ContingencyPlanTasks_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    CREATE INDEX [IX_ContingencyPlanTasks_ActivatedById] ON [ContingencyPlanTasks] ([ActivatedById]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    CREATE INDEX [IX_ContingencyPlanTasks_ContingencyPlanId] ON [ContingencyPlanTasks] ([ContingencyPlanId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    CREATE INDEX [IX_ContingencyPlanTasks_WorkTaskId] ON [ContingencyPlanTasks] ([WorkTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260714091551_UpdateContingencyPlanSchemaV3'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260714091551_UpdateContingencyPlanSchemaV3', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715054827_AddRiskDescriptionToContingencyPlan'
)
BEGIN
    ALTER TABLE [ContingencyPlans] ADD [RiskDescription] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715054827_AddRiskDescriptionToContingencyPlan'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260715054827_AddRiskDescriptionToContingencyPlan', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    DECLARE @var8 nvarchar(max);
    SELECT @var8 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ContingencyPlanTasks]') AND [c].[name] = N'WorkTaskId');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [ContingencyPlanTasks] DROP CONSTRAINT ' + @var8 + ';');
    ALTER TABLE [ContingencyPlanTasks] ALTER COLUMN [WorkTaskId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    ALTER TABLE [ContingencyPlanTasks] ADD [AssigneeId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    ALTER TABLE [ContingencyPlanTasks] ADD [Description] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    ALTER TABLE [ContingencyPlanTasks] ADD [Priority] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    ALTER TABLE [ContingencyPlanTasks] ADD [StatusName] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    ALTER TABLE [ContingencyPlanTasks] ADD [Title] nvarchar(255) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    CREATE INDEX [IX_ContingencyPlanTasks_AssigneeId] ON [ContingencyPlanTasks] ([AssigneeId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    ALTER TABLE [ContingencyPlanTasks] ADD CONSTRAINT [FK_ContingencyPlanTasks_Users_AssigneeId] FOREIGN KEY ([AssigneeId]) REFERENCES [Users] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260715083243_UpdateContingencyTaskSchema'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260715083243_UpdateContingencyTaskSchema', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716035604_SyncDbContextMappings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716035604_SyncDbContextMappings', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716050006_AddAiConversationHistory'
)
BEGIN
    CREATE TABLE [AiConversations] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [Title] nvarchar(180) NOT NULL,
        [MessagesJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiConversations] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716050006_AddAiConversationHistory'
)
BEGIN
    CREATE INDEX [IX_AiConversations_UserId_WorkspaceId_UpdatedAt] ON [AiConversations] ([UserId], [WorkspaceId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716050006_AddAiConversationHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716050006_AddAiConversationHistory', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716054020_AddBadgeAndIsProtected'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716054020_AddBadgeAndIsProtected', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    DROP INDEX [IX_StickyNotes_UserId] ON [StickyNotes];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    DECLARE @var9 nvarchar(max);
    SELECT @var9 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StickyNotes]') AND [c].[name] = N'Color');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [StickyNotes] DROP CONSTRAINT ' + @var9 + ';');
    ALTER TABLE [StickyNotes] ALTER COLUMN [Color] nvarchar(20) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [GoalId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [IsPinned] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [ProjectId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [SourceRoute] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [Title] nvarchar(180) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [WorkTaskId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [WorkspaceId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    CREATE INDEX [IX_StickyNotes_GoalId] ON [StickyNotes] ([GoalId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    CREATE INDEX [IX_StickyNotes_ProjectId] ON [StickyNotes] ([ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    CREATE INDEX [IX_StickyNotes_UserId_IsDeleted_IsPinned_UpdatedAt] ON [StickyNotes] ([UserId], [IsDeleted], [IsPinned], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    CREATE INDEX [IX_StickyNotes_WorkspaceId] ON [StickyNotes] ([WorkspaceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    CREATE INDEX [IX_StickyNotes_WorkTaskId] ON [StickyNotes] ([WorkTaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716061325_AddGlobalStickiesMvp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716061325_AddGlobalStickiesMvp', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716124944_AddAiMultimodalAttachments'
)
BEGIN
    CREATE TABLE [AiAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [ConversationId] uniqueidentifier NOT NULL,
        [FileName] nvarchar(255) NOT NULL,
        [StoredFileName] nvarchar(100) NOT NULL,
        [MimeType] nvarchar(150) NOT NULL,
        [Extension] nvarchar(20) NOT NULL,
        [Kind] nvarchar(20) NOT NULL,
        [Sha256] nvarchar(64) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [ErrorMessage] nvarchar(500) NULL,
        [FileSize] bigint NOT NULL,
        [Width] int NULL,
        [Height] int NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiAttachments_AiConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [AiConversations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716124944_AddAiMultimodalAttachments'
)
BEGIN
    CREATE TABLE [AiAttachmentChunks] (
        [Id] uniqueidentifier NOT NULL,
        [AttachmentId] uniqueidentifier NOT NULL,
        [ChunkIndex] int NOT NULL,
        [Locator] nvarchar(180) NOT NULL,
        [Content] nvarchar(2000) NOT NULL,
        [TokenEstimate] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiAttachmentChunks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiAttachmentChunks_AiAttachments_AttachmentId] FOREIGN KEY ([AttachmentId]) REFERENCES [AiAttachments] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716124944_AddAiMultimodalAttachments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiAttachmentChunks_AttachmentId_ChunkIndex] ON [AiAttachmentChunks] ([AttachmentId], [ChunkIndex]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716124944_AddAiMultimodalAttachments'
)
BEGIN
    CREATE INDEX [IX_AiAttachments_ConversationId_CreatedAt] ON [AiAttachments] ([ConversationId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716124944_AddAiMultimodalAttachments'
)
BEGIN
    CREATE INDEX [IX_AiAttachments_UserId_WorkspaceId_ConversationId_Sha256] ON [AiAttachments] ([UserId], [WorkspaceId], [ConversationId], [Sha256]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716124944_AddAiMultimodalAttachments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716124944_AddAiMultimodalAttachments', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716233911_AddFloatingStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [IsFloating] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716233911_AddFloatingStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [PositionX] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716233911_AddFloatingStickiesMvp'
)
BEGIN
    ALTER TABLE [StickyNotes] ADD [PositionY] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716233911_AddFloatingStickiesMvp'
)
BEGIN
    CREATE INDEX [IX_StickyNotes_UserId_IsFloating] ON [StickyNotes] ([UserId], [IsFloating]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260716233911_AddFloatingStickiesMvp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260716233911_AddFloatingStickiesMvp', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260717061834_AutoMigrationAfterModelChanges'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260717061834_AutoMigrationAfterModelChanges', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718001618_PreserveTaskAssignmentHistory'
)
BEGIN
    ALTER TABLE [TaskAssignments] ADD [RemovalReason] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718001618_PreserveTaskAssignmentHistory'
)
BEGIN
    ALTER TABLE [TaskAssignments] ADD [RemovedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718001618_PreserveTaskAssignmentHistory'
)
BEGIN
    ALTER TABLE [TaskAssignments] ADD [RemovedBy] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718001618_PreserveTaskAssignmentHistory'
)
BEGIN
    CREATE INDEX [IX_TaskAssignments_RemovedBy] ON [TaskAssignments] ([RemovedBy]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718001618_PreserveTaskAssignmentHistory'
)
BEGIN
    ALTER TABLE [TaskAssignments] ADD CONSTRAINT [FK_TaskAssignments_Users_RemovedBy] FOREIGN KEY ([RemovedBy]) REFERENCES [Users] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718001618_PreserveTaskAssignmentHistory'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260718001618_PreserveTaskAssignmentHistory', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718003622_AddImmutableRewardLedger'
)
BEGIN
    ALTER TABLE [PointTransactions] ADD [IdempotencyKey] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718003622_AddImmutableRewardLedger'
)
BEGIN
    ALTER TABLE [PointTransactions] ADD [ReversalOfTransactionId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718003622_AddImmutableRewardLedger'
)
BEGIN
    ALTER TABLE [PointTransactions] ADD [RewardEventId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718003622_AddImmutableRewardLedger'
)
BEGIN
    ALTER TABLE [PointTransactions] ADD [RewardRuleVersion] nvarchar(40) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718003622_AddImmutableRewardLedger'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PointTransactions_IdempotencyKey] ON [PointTransactions] ([IdempotencyKey]) WHERE [IdempotencyKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718003622_AddImmutableRewardLedger'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PointTransactions_ReversalOfTransactionId] ON [PointTransactions] ([ReversalOfTransactionId]) WHERE [ReversalOfTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718003622_AddImmutableRewardLedger'
)
BEGIN
    ALTER TABLE [PointTransactions] ADD CONSTRAINT [FK_PointTransactions_PointTransactions_ReversalOfTransactionId] FOREIGN KEY ([ReversalOfTransactionId]) REFERENCES [PointTransactions] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718003622_AddImmutableRewardLedger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260718003622_AddImmutableRewardLedger', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718010229_ReplaceRuntimeSchemaGuards'
)
BEGIN

    IF OBJECT_ID('dbo.IntegrationAccounts', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.IntegrationAccounts (
            Id uniqueidentifier NOT NULL,
            UserId uniqueidentifier NOT NULL,
            Provider nvarchar(128) NOT NULL,
            AccountEmail nvarchar(512) NOT NULL,
            ExternalAccountId nvarchar(512) NULL,
            AccessToken nvarchar(max) NOT NULL,
            RefreshToken nvarchar(max) NULL,
            AccessTokenExpiresAt datetime2 NULL,
            Scopes nvarchar(max) NOT NULL,
            IsActive bit NOT NULL CONSTRAINT DF_IntegrationAccounts_IsActive DEFAULT CAST(1 AS bit),
            LastSyncedAt datetime2 NULL,
            CreatedAt datetime2 NOT NULL CONSTRAINT DF_IntegrationAccounts_CreatedAt DEFAULT SYSUTCDATETIME(),
            UpdatedAt datetime2 NOT NULL CONSTRAINT DF_IntegrationAccounts_UpdatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_IntegrationAccounts PRIMARY KEY (Id),
            CONSTRAINT FK_IntegrationAccounts_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_IntegrationAccounts_UserId_Provider' AND object_id = OBJECT_ID('dbo.IntegrationAccounts'))
        CREATE UNIQUE INDEX IX_IntegrationAccounts_UserId_Provider ON dbo.IntegrationAccounts(UserId, Provider);

    IF OBJECT_ID('dbo.InboxItems', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.InboxItems (
            Id uniqueidentifier NOT NULL,
            UserId uniqueidentifier NOT NULL,
            IntegrationAccountId uniqueidentifier NULL,
            Source nvarchar(64) NOT NULL,
            Provider nvarchar(128) NOT NULL,
            ExternalId nvarchar(512) NOT NULL,
            Title nvarchar(512) NOT NULL,
            Content nvarchar(max) NULL,
            Location nvarchar(512) NULL,
            StartsAt datetime2 NULL,
            EndsAt datetime2 NULL,
            IsRead bit NOT NULL CONSTRAINT DF_InboxItems_IsRead DEFAULT CAST(0 AS bit),
            CreatedTaskId uniqueidentifier NULL,
            CreatedAt datetime2 NOT NULL CONSTRAINT DF_InboxItems_CreatedAt DEFAULT SYSUTCDATETIME(),
            UpdatedAt datetime2 NOT NULL CONSTRAINT DF_InboxItems_UpdatedAt DEFAULT SYSUTCDATETIME(),
            CONSTRAINT PK_InboxItems PRIMARY KEY (Id),
            CONSTRAINT FK_InboxItems_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
            CONSTRAINT FK_InboxItems_IntegrationAccounts_IntegrationAccountId FOREIGN KEY (IntegrationAccountId) REFERENCES dbo.IntegrationAccounts(Id),
            CONSTRAINT FK_InboxItems_WorkTasks_CreatedTaskId FOREIGN KEY (CreatedTaskId) REFERENCES dbo.WorkTasks(Id)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InboxItems_UserId_Provider_ExternalId' AND object_id = OBJECT_ID('dbo.InboxItems'))
        CREATE UNIQUE INDEX IX_InboxItems_UserId_Provider_ExternalId ON dbo.InboxItems(UserId, Provider, ExternalId);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InboxItems_UserId_Source_CreatedAt' AND object_id = OBJECT_ID('dbo.InboxItems'))
        CREATE INDEX IX_InboxItems_UserId_Source_CreatedAt ON dbo.InboxItems(UserId, Source, CreatedAt);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InboxItems_UserId_IsRead' AND object_id = OBJECT_ID('dbo.InboxItems'))
        CREATE INDEX IX_InboxItems_UserId_IsRead ON dbo.InboxItems(UserId, IsRead);

    IF OBJECT_ID('dbo.SyncHistories', 'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SyncHistories (
            Id uniqueidentifier NOT NULL,
            UserId uniqueidentifier NOT NULL,
            IntegrationAccountId uniqueidentifier NULL,
            Provider nvarchar(128) NOT NULL,
            Status nvarchar(64) NOT NULL,
            ItemsImported int NOT NULL CONSTRAINT DF_SyncHistories_ItemsImported DEFAULT 0,
            Message nvarchar(max) NULL,
            StartedAt datetime2 NOT NULL CONSTRAINT DF_SyncHistories_StartedAt DEFAULT SYSUTCDATETIME(),
            CompletedAt datetime2 NULL,
            CONSTRAINT PK_SyncHistories PRIMARY KEY (Id),
            CONSTRAINT FK_SyncHistories_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE,
            CONSTRAINT FK_SyncHistories_IntegrationAccounts_IntegrationAccountId FOREIGN KEY (IntegrationAccountId) REFERENCES dbo.IntegrationAccounts(Id)
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SyncHistories_UserId_Provider_StartedAt' AND object_id = OBJECT_ID('dbo.SyncHistories'))
        CREATE INDEX IX_SyncHistories_UserId_Provider_StartedAt ON dbo.SyncHistories(UserId, Provider, StartedAt);

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718010229_ReplaceRuntimeSchemaGuards'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260718010229_ReplaceRuntimeSchemaGuards', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718020108_AddAiActionSafetyState'
)
BEGIN
    CREATE TABLE [AiActionExecutions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NULL,
        [ConversationId] uniqueidentifier NULL,
        [ActionType] nvarchar(128) NOT NULL,
        [IdempotencyKey] nvarchar(200) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [PayloadHash] nvarchar(64) NOT NULL,
        [PreviewJson] nvarchar(max) NOT NULL,
        [ResultJson] nvarchar(max) NULL,
        [State] nvarchar(32) NOT NULL,
        [ErrorCode] nvarchar(64) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [ConfirmedAt] datetime2 NULL,
        [ExecutedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_AiActionExecutions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718020108_AddAiActionSafetyState'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiActionExecutions_UserId_IdempotencyKey] ON [AiActionExecutions] ([UserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718020108_AddAiActionSafetyState'
)
BEGIN
    CREATE INDEX [IX_AiActionExecutions_UserId_State_UpdatedAt] ON [AiActionExecutions] ([UserId], [State], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260718020108_AddAiActionSafetyState'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260718020108_AddAiActionSafetyState', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    ALTER TABLE [StarredItems] DROP CONSTRAINT [CK_StarredItems_ItemType];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    ;WITH NormalizedStarred AS
    (
        SELECT [Id],
               ROW_NUMBER() OVER
               (
                   PARTITION BY [UserId], [WorkspaceId],
                       CASE
                           WHEN LOWER([ItemType]) IN ('task', 'work-task', 'work_task', 'worktask') THEN 'WorkTask'
                           WHEN LOWER([ItemType]) = 'project' THEN 'Project'
                           WHEN LOWER([ItemType]) = 'goal' THEN 'Goal'
                           WHEN LOWER([ItemType]) = 'team' THEN 'Team'
                           WHEN LOWER([ItemType]) = 'user' THEN 'User'
                           ELSE [ItemType]
                       END,
                       [ItemId]
                   ORDER BY [CreatedAt] DESC, [Id]
               ) AS [RowNumber]
        FROM [StarredItems]
    )
    DELETE FROM [StarredItems]
    WHERE [Id] IN
    (
        SELECT [Id] FROM NormalizedStarred WHERE [RowNumber] > 1
    );

    UPDATE [StarredItems]
    SET [ItemType] =
        CASE
            WHEN LOWER([ItemType]) IN ('task', 'work-task', 'work_task', 'worktask') THEN 'WorkTask'
            WHEN LOWER([ItemType]) = 'project' THEN 'Project'
            WHEN LOWER([ItemType]) = 'goal' THEN 'Goal'
            WHEN LOWER([ItemType]) = 'team' THEN 'Team'
            WHEN LOWER([ItemType]) = 'user' THEN 'User'
        END
    WHERE LOWER([ItemType]) IN
        ('task', 'work-task', 'work_task', 'worktask', 'project', 'goal', 'team', 'user');

    DELETE FROM [StarredItems]
    WHERE [ItemType] NOT IN ('Goal', 'Project', 'Team', 'User', 'WorkTask');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    ALTER TABLE [StarredItems] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    UPDATE [StarredItems] SET [UpdatedAt] = [CreatedAt];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    CREATE INDEX [IX_StarredItems_UserId_WorkspaceId_CreatedAt_Id] ON [StarredItems] ([UserId], [WorkspaceId], [CreatedAt] DESC, [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    EXEC(N'ALTER TABLE [StarredItems] ADD CONSTRAINT [CK_StarredItems_ItemType] CHECK ([ItemType] IN (''Goal'', ''Project'', ''Team'', ''User'', ''WorkTask''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    CREATE INDEX [IX_RecentViews_UserId_ViewedAt_Id] ON [RecentViews] ([UserId], [ViewedAt] DESC, [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    ;WITH NormalizedRecent AS
    (
        SELECT [Id],
               ROW_NUMBER() OVER
               (
                   PARTITION BY [UserId],
                       CASE
                           WHEN LOWER([EntityType]) IN ('task', 'work-task', 'work_task', 'worktask') THEN 'WorkTask'
                           WHEN LOWER([EntityType]) = 'project' THEN 'Project'
                           WHEN LOWER([EntityType]) = 'goal' THEN 'Goal'
                           WHEN LOWER([EntityType]) = 'team' THEN 'Team'
                           WHEN LOWER([EntityType]) = 'user' THEN 'User'
                           ELSE [EntityType]
                       END,
                       [EntityId]
                   ORDER BY [ViewedAt] DESC, [Id]
               ) AS [RowNumber]
        FROM [RecentViews]
    )
    DELETE FROM [RecentViews]
    WHERE [Id] IN
    (
        SELECT [Id] FROM NormalizedRecent WHERE [RowNumber] > 1
    );

    UPDATE [RecentViews]
    SET [EntityType] =
        CASE
            WHEN LOWER([EntityType]) IN ('task', 'work-task', 'work_task', 'worktask') THEN 'WorkTask'
            WHEN LOWER([EntityType]) = 'project' THEN 'Project'
            WHEN LOWER([EntityType]) = 'goal' THEN 'Goal'
            WHEN LOWER([EntityType]) = 'team' THEN 'Team'
            WHEN LOWER([EntityType]) = 'user' THEN 'User'
        END
    WHERE LOWER([EntityType]) IN
        ('task', 'work-task', 'work_task', 'worktask', 'project', 'goal', 'team', 'user');

    DELETE FROM [RecentViews]
    WHERE [EntityType] NOT IN ('Goal', 'Project', 'Team', 'User', 'WorkTask');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    EXEC(N'ALTER TABLE [RecentViews] ADD CONSTRAINT [CK_RecentViews_EntityType] CHECK ([EntityType] IN (''Goal'', ''Project'', ''Team'', ''User'', ''WorkTask''))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726152821_PersistStarredAndRecentlyViewedItems'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726152821_PersistStarredAndRecentlyViewedItems', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    DROP INDEX [IX_Sprints_ProjectId] ON [Sprints];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    ALTER TABLE [Sprints] ADD [CompletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    ALTER TABLE [Sprints] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    ALTER TABLE [Sprints] ADD [StartedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    ALTER TABLE [Sprints] ADD [State] nvarchar(20) NOT NULL DEFAULT N'Planned';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    UPDATE [Sprints]
    SET [State] = CASE
            WHEN [Status] = 1 THEN N'Active'
            WHEN CONVERT(date, [EndDate]) < CONVERT(date, SYSUTCDATETIME()) THEN N'Completed'
            ELSE N'Planned'
        END,
        [StartedAt] = CASE
            WHEN [Status] = 1 OR CONVERT(date, [EndDate]) < CONVERT(date, SYSUTCDATETIME())
                THEN [StartDate]
            ELSE NULL
        END,
        [CompletedAt] = CASE
            WHEN [Status] = 0 AND CONVERT(date, [EndDate]) < CONVERT(date, SYSUTCDATETIME())
                THEN [EndDate]
            ELSE NULL
        END;

    WITH [RankedActive] AS
    (
        SELECT [Id],
               ROW_NUMBER() OVER (
                   PARTITION BY [ProjectId]
                   ORDER BY [StartDate] DESC, [CreatedAt] DESC, [Id] ASC
               ) AS [ActiveRank]
        FROM [Sprints]
        WHERE [State] = N'Active' AND [IsDeleted] = 0
    )
    UPDATE [s]
    SET [State] = CASE
            WHEN CONVERT(date, [s].[EndDate]) < CONVERT(date, SYSUTCDATETIME())
                THEN N'Completed'
            ELSE N'Planned'
        END,
        [Status] = 0,
        [CompletedAt] = CASE
            WHEN CONVERT(date, [s].[EndDate]) < CONVERT(date, SYSUTCDATETIME())
                THEN [s].[EndDate]
            ELSE NULL
        END,
        [StartedAt] = CASE
            WHEN CONVERT(date, [s].[EndDate]) < CONVERT(date, SYSUTCDATETIME())
                THEN [s].[StartDate]
            ELSE NULL
        END
    FROM [Sprints] AS [s]
    INNER JOIN [RankedActive] AS [ranked] ON [ranked].[Id] = [s].[Id]
    WHERE [ranked].[ActiveRank] > 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    CREATE INDEX [IX_Sprints_Project_State_Order] ON [Sprints] ([ProjectId], [State], [StartDate], [CreatedAt], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Sprints_Project_Active] ON [Sprints] ([ProjectId]) WHERE [State] = N''Active'' AND [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260727141026_EnforceCycleTransitions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260727141026_EnforceCycleTransitions', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728053502_AddGoogleExternalLogins'
)
BEGIN
    CREATE TABLE [ExternalLogins] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(32) NOT NULL,
        [ProviderSubject] nvarchar(255) NOT NULL,
        [ProviderEmail] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastLoginAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ExternalLogins] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExternalLogins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728053502_AddGoogleExternalLogins'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExternalLogins_Provider_ProviderSubject] ON [ExternalLogins] ([Provider], [ProviderSubject]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728053502_AddGoogleExternalLogins'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExternalLogins_UserId_Provider] ON [ExternalLogins] ([UserId], [Provider]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728053502_AddGoogleExternalLogins'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728053502_AddGoogleExternalLogins', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    DECLARE @var10 nvarchar(max);
    SELECT @var10 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChannelMessages]') AND [c].[name] = N'ChannelId');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [ChannelMessages] DROP CONSTRAINT ' + @var10 + ';');
    ALTER TABLE [ChannelMessages] ALTER COLUMN [ChannelId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    ALTER TABLE [ChannelMessages] ADD [CollaborationChannelId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    CREATE TABLE [CollaborationChannels] (
        [Id] uniqueidentifier NOT NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [IsArchived] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_CollaborationChannels] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CollaborationChannels_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollaborationChannels_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollaborationChannels_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    CREATE TABLE [CollaborationChannelMembers] (
        [ChannelId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        [LeftAt] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [CanSendMessages] bit NOT NULL,
        CONSTRAINT [PK_CollaborationChannelMembers] PRIMARY KEY ([ChannelId], [UserId]),
        CONSTRAINT [FK_CollaborationChannelMembers_CollaborationChannels_ChannelId] FOREIGN KEY ([ChannelId]) REFERENCES [CollaborationChannels] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CollaborationChannelMembers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    CREATE INDEX [IX_ChannelMessages_CollaborationChannelId_SentAt_Id] ON [ChannelMessages] ([CollaborationChannelId], [SentAt], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    CREATE INDEX [IX_CollaborationChannelMembers_UserId_IsActive] ON [CollaborationChannelMembers] ([UserId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    CREATE INDEX [IX_CollaborationChannels_CreatedByUserId] ON [CollaborationChannels] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    CREATE INDEX [IX_CollaborationChannels_ProjectId_IsDeleted_IsArchived] ON [CollaborationChannels] ([ProjectId], [IsDeleted], [IsArchived]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    CREATE INDEX [IX_CollaborationChannels_WorkspaceId_ProjectId] ON [CollaborationChannels] ([WorkspaceId], [ProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    ALTER TABLE [ChannelMessages] ADD CONSTRAINT [FK_ChannelMessages_CollaborationChannels_CollaborationChannelId] FOREIGN KEY ([CollaborationChannelId]) REFERENCES [CollaborationChannels] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728091647_AddCollaborationChannelText'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728091647_AddCollaborationChannelText', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728095916_AddCollaborationChannelDiscovery'
)
BEGIN
    ALTER TABLE [CollaborationChannels] ADD [Description] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728095916_AddCollaborationChannelDiscovery'
)
BEGIN
    ALTER TABLE [CollaborationChannels] ADD [ProvisioningKey] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728095916_AddCollaborationChannelDiscovery'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CollaborationChannels_ProjectId_CreatedByUserId_ProvisioningKey] ON [CollaborationChannels] ([ProjectId], [CreatedByUserId], [ProvisioningKey]) WHERE [ProvisioningKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728095916_AddCollaborationChannelDiscovery'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728095916_AddCollaborationChannelDiscovery', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    DECLARE @var11 nvarchar(max);
    SELECT @var11 = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[DirectMessages]') AND [c].[name] = N'Content');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [DirectMessages] DROP CONSTRAINT ' + @var11 + ';');
    ALTER TABLE [DirectMessages] ALTER COLUMN [Content] nvarchar(4000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    ALTER TABLE [DirectMessages] ADD [ConversationId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    CREATE TABLE [DirectConversations] (
        [Id] uniqueidentifier NOT NULL,
        [WorkspaceId] uniqueidentifier NOT NULL,
        [UserLowId] uniqueidentifier NOT NULL,
        [UserHighId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [LastMessageAt] datetime2 NULL,
        CONSTRAINT [PK_DirectConversations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_DirectConversations_DistinctUsers] CHECK ([UserLowId] <> [UserHighId]),
        CONSTRAINT [FK_DirectConversations_Users_UserHighId] FOREIGN KEY ([UserHighId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DirectConversations_Users_UserLowId] FOREIGN KEY ([UserLowId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DirectConversations_Workspaces_WorkspaceId] FOREIGN KEY ([WorkspaceId]) REFERENCES [Workspaces] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    CREATE TABLE [DirectConversationParticipants] (
        [ConversationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [JoinedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DirectConversationParticipants] PRIMARY KEY ([ConversationId], [UserId]),
        CONSTRAINT [FK_DirectConversationParticipants_DirectConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [DirectConversations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DirectConversationParticipants_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    CREATE INDEX [IX_DirectMessages_ConversationId_SentAt_Id] ON [DirectMessages] ([ConversationId], [SentAt], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    CREATE INDEX [IX_DirectConversationParticipants_UserId_ConversationId] ON [DirectConversationParticipants] ([UserId], [ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    CREATE INDEX [IX_DirectConversations_UserHighId] ON [DirectConversations] ([UserHighId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DirectConversations_UserLowId_UserHighId] ON [DirectConversations] ([UserLowId], [UserHighId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    CREATE INDEX [IX_DirectConversations_WorkspaceId_LastMessageAt_CreatedAt_Id] ON [DirectConversations] ([WorkspaceId], [LastMessageAt], [CreatedAt], [Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    ALTER TABLE [DirectMessages] ADD CONSTRAINT [FK_DirectMessages_DirectConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [DirectConversations] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260728144925_AddDirectConversationPersistence'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260728144925_AddDirectConversationPersistence', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804062616_AddCollaborationReadState'
)
BEGIN
    CREATE TABLE [CollaborationChannelReadStates] (
        [ChannelId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LastReadMessageId] uniqueidentifier NULL,
        [LastReadAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CollaborationChannelReadStates] PRIMARY KEY ([ChannelId], [UserId]),
        CONSTRAINT [FK_CollaborationChannelReadStates_ChannelMessages_LastReadMessageId] FOREIGN KEY ([LastReadMessageId]) REFERENCES [ChannelMessages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollaborationChannelReadStates_CollaborationChannels_ChannelId] FOREIGN KEY ([ChannelId]) REFERENCES [CollaborationChannels] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CollaborationChannelReadStates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804062616_AddCollaborationReadState'
)
BEGIN
    CREATE TABLE [DirectConversationReadStates] (
        [ConversationId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [LastReadMessageId] uniqueidentifier NULL,
        [LastReadAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DirectConversationReadStates] PRIMARY KEY ([ConversationId], [UserId]),
        CONSTRAINT [FK_DirectConversationReadStates_DirectConversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [DirectConversations] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DirectConversationReadStates_DirectMessages_LastReadMessageId] FOREIGN KEY ([LastReadMessageId]) REFERENCES [DirectMessages] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_DirectConversationReadStates_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804062616_AddCollaborationReadState'
)
BEGIN
    CREATE INDEX [IX_CollaborationChannelReadStates_LastReadMessageId] ON [CollaborationChannelReadStates] ([LastReadMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804062616_AddCollaborationReadState'
)
BEGIN
    CREATE INDEX [IX_CollaborationChannelReadStates_UserId_ChannelId] ON [CollaborationChannelReadStates] ([UserId], [ChannelId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804062616_AddCollaborationReadState'
)
BEGIN
    CREATE INDEX [IX_DirectConversationReadStates_LastReadMessageId] ON [DirectConversationReadStates] ([LastReadMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804062616_AddCollaborationReadState'
)
BEGIN
    CREATE INDEX [IX_DirectConversationReadStates_UserId_ConversationId] ON [DirectConversationReadStates] ([UserId], [ConversationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804062616_AddCollaborationReadState'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804062616_AddCollaborationReadState', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804072953_AddCollaborationMessageAttachments'
)
BEGIN
    CREATE TABLE [CollaborationMessageAttachments] (
        [Id] uniqueidentifier NOT NULL,
        [ChannelMessageId] uniqueidentifier NULL,
        [DirectMessageId] uniqueidentifier NULL,
        [StorageKey] nvarchar(80) NOT NULL,
        [OriginalFileName] nvarchar(255) NOT NULL,
        [ContentType] nvarchar(120) NOT NULL,
        [SizeBytes] bigint NOT NULL,
        [UploadedByUserId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CollaborationMessageAttachments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CollaborationMessageAttachments_ExactlyOneMessage] CHECK (([ChannelMessageId] IS NOT NULL AND [DirectMessageId] IS NULL) OR ([ChannelMessageId] IS NULL AND [DirectMessageId] IS NOT NULL)),
        CONSTRAINT [FK_CollaborationMessageAttachments_ChannelMessages_ChannelMessageId] FOREIGN KEY ([ChannelMessageId]) REFERENCES [ChannelMessages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CollaborationMessageAttachments_DirectMessages_DirectMessageId] FOREIGN KEY ([DirectMessageId]) REFERENCES [DirectMessages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CollaborationMessageAttachments_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804072953_AddCollaborationMessageAttachments'
)
BEGIN
    CREATE INDEX [IX_CollaborationMessageAttachments_ChannelMessageId] ON [CollaborationMessageAttachments] ([ChannelMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804072953_AddCollaborationMessageAttachments'
)
BEGIN
    CREATE INDEX [IX_CollaborationMessageAttachments_DirectMessageId] ON [CollaborationMessageAttachments] ([DirectMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804072953_AddCollaborationMessageAttachments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CollaborationMessageAttachments_StorageKey] ON [CollaborationMessageAttachments] ([StorageKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804072953_AddCollaborationMessageAttachments'
)
BEGIN
    CREATE INDEX [IX_CollaborationMessageAttachments_UploadedByUserId_CreatedAt] ON [CollaborationMessageAttachments] ([UploadedByUserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804072953_AddCollaborationMessageAttachments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804072953_AddCollaborationMessageAttachments', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    DROP INDEX [IX_Notifications_UserId] ON [Notifications];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    ALTER TABLE [Notifications] ADD [ChannelMessageId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    ALTER TABLE [Notifications] ADD [CollaborationChannelId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    CREATE TABLE [ChannelMessageMentions] (
        [Id] uniqueidentifier NOT NULL,
        [ChannelMessageId] uniqueidentifier NOT NULL,
        [MentionedUserId] uniqueidentifier NOT NULL,
        [StartIndex] int NOT NULL,
        [Length] int NOT NULL,
        [DisplayText] nvarchar(200) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChannelMessageMentions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChannelMessageMentions_ChannelMessages_ChannelMessageId] FOREIGN KEY ([ChannelMessageId]) REFERENCES [ChannelMessages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChannelMessageMentions_Users_MentionedUserId] FOREIGN KEY ([MentionedUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    CREATE INDEX [IX_Notifications_ChannelMessageId] ON [Notifications] ([ChannelMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    CREATE INDEX [IX_Notifications_CollaborationChannelId_CreatedAt] ON [Notifications] ([CollaborationChannelId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Notifications_UserId_ChannelMessageId] ON [Notifications] ([UserId], [ChannelMessageId]) WHERE [ChannelMessageId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ChannelMessageMentions_ChannelMessageId_MentionedUserId] ON [ChannelMessageMentions] ([ChannelMessageId], [MentionedUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    CREATE INDEX [IX_ChannelMessageMentions_MentionedUserId_CreatedAt] ON [ChannelMessageMentions] ([MentionedUserId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_ChannelMessages_ChannelMessageId] FOREIGN KEY ([ChannelMessageId]) REFERENCES [ChannelMessages] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_CollaborationChannels_CollaborationChannelId] FOREIGN KEY ([CollaborationChannelId]) REFERENCES [CollaborationChannels] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804080930_AddChannelMessageMentions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804080930_AddChannelMessageMentions', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804140000_SeedAiCreditSourceOfTruth'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'free')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000001', N'free', N'Free', NULL, 0, 3, 100, 0, N'PendingConfirmation', NULL, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'team')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000002', N'team', N'Team', NULL, 0, NULL, 0, 0, N'PendingConfirmation', NULL, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'business')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000003', N'business', N'Business', NULL, 0, NULL, 0, 0, N'PendingConfirmation', NULL, SYSUTCDATETIME(), SYSUTCDATETIME());

    DECLARE @Disclaimer nvarchar(max) = N'Mức sử dụng là ước tính và có thể thay đổi theo độ dài nội dung.';

    IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'summarize_project')
    INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
    VALUES ('c1000000-0000-0000-0000-000000000001', N'summarize_project', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'create_project')
    INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
    VALUES ('c1000000-0000-0000-0000-000000000002', N'create_project', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'create_task')
    INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
    VALUES ('c1000000-0000-0000-0000-000000000003', N'create_task', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'create_cycle')
    INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
    VALUES ('c1000000-0000-0000-0000-000000000004', N'create_cycle', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'create_goal')
    INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
    VALUES ('c1000000-0000-0000-0000-000000000005', N'create_goal', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiCreditRules] WHERE [ActionType] = N'list_overdue_tasks')
    INSERT INTO [AiCreditRules] ([Id], [ActionType], [EstimatedCredits], [IsActive], [Disclaimer], [CreatedAt], [UpdatedAt])
    VALUES ('c1000000-0000-0000-0000-000000000006', N'list_overdue_tasks', 1, 1, @Disclaimer, SYSUTCDATETIME(), SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804140000_SeedAiCreditSourceOfTruth'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804140000_SeedAiCreditSourceOfTruth', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804160000_ApplyApprovedMvpAiPricing'
)
BEGIN
    ALTER TABLE [AiPricingPlans] ADD [Audience] nvarchar(32) NOT NULL DEFAULT N'Personal';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804160000_ApplyApprovedMvpAiPricing'
)
BEGIN
    ALTER TABLE [AiPricingPlans] ADD [IsRecommended] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804160000_ApplyApprovedMvpAiPricing'
)
BEGIN
    ALTER TABLE [AiPricingPlans] ADD [IsPublished] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804160000_ApplyApprovedMvpAiPricing'
)
BEGIN
    UPDATE [AiPricingPlans]
    SET [Name] = N'Free', [MonthlyPriceVnd] = 0, [IncludedAiCredits] = 100,
        [Audience] = N'Personal', [IsRecommended] = 0, [IsPublished] = 1,
        [PricingStatus] = N'Published'
    WHERE [Code] = N'free';

    UPDATE [AiPricingPlans]
    SET [Name] = N'Team', [MonthlyPriceVnd] = 499000, [IncludedAiCredits] = 9000,
        [Audience] = N'Team', [IsRecommended] = 1, [IsPublished] = 1,
        [PricingStatus] = N'Published'
    WHERE [Code] = N'team';

    UPDATE [AiPricingPlans]
    SET [Audience] = N'Legacy', [IsRecommended] = 0, [IsPublished] = 0
    WHERE [Code] = N'business';

    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'starter')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000004', N'starter', N'Starter', 49000, 0, NULL, 500, 0, N'Personal', 0, 1, N'Published', N'["500 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'plus')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000005', N'plus', N'Plus', 99000, 0, NULL, 1200, 0, N'Personal', 1, 1, N'Published', N'["1,200 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'pro')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000006', N'pro', N'Pro', 199000, 0, NULL, 3000, 0, N'Personal', 0, 1, N'Published', N'["3,000 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'enterprise')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000007', N'enterprise', N'Enterprise', NULL, 0, NULL, 0, 0, N'Team', 0, 1, N'Contact', N'["Credit by agreement"]', SYSUTCDATETIME(), SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260804160000_ApplyApprovedMvpAiPricing'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260804160000_ApplyApprovedMvpAiPricing', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260808120000_EnsureApprovedMvpAiPricingPlans'
)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'free')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000001', N'free', N'Free', 0, 0, NULL, 100, 0, N'Personal', 0, 1, N'Published', N'["100 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());

    IF NOT EXISTS (SELECT 1 FROM [AiPricingPlans] WHERE [Code] = N'team')
    INSERT INTO [AiPricingPlans]
        ([Id], [Code], [Name], [MonthlyPriceVnd], [PerUser], [IncludedUsers], [IncludedAiCredits], [ExtraAiCreditsEnabled], [Audience], [IsRecommended], [IsPublished], [PricingStatus], [FeaturesJson], [CreatedAt], [UpdatedAt])
    VALUES
        ('f1000000-0000-0000-0000-000000000003', N'team', N'Team', 499000, 0, NULL, 9000, 0, N'Team', 0, 1, N'Published', N'["9,000 AI credits"]', SYSUTCDATETIME(), SYSUTCDATETIME());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260808120000_EnsureApprovedMvpAiPricingPlans'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260808120000_EnsureApprovedMvpAiPricingPlans', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE TABLE [AiCreditAdjustments] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Amount] int NOT NULL,
        [AdjustmentType] nvarchar(32) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [CreatedByUserId] uniqueidentifier NOT NULL,
        [EffectivePeriodStart] datetime2 NOT NULL,
        [EffectivePeriodEnd] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiCreditAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiCreditAdjustments_Users_CreatedByUserId] FOREIGN KEY ([CreatedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AiCreditAdjustments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE TABLE [AiSubscriptions] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PlanCode] nvarchar(64) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CurrentPeriodStart] datetime2 NOT NULL,
        [CurrentPeriodEnd] datetime2 NOT NULL,
        [ActivatedAt] datetime2 NULL,
        [CancelledAt] datetime2 NULL,
        [AutoRenew] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiSubscriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiSubscriptions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE TABLE [PaymentOrders] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PlanCode] nvarchar(64) NOT NULL,
        [AmountVnd] decimal(18,2) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [TransferCode] nvarchar(64) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [PaidAt] datetime2 NULL,
        [ApprovedByUserId] uniqueidentifier NULL,
        [AdminNote] nvarchar(1000) NULL,
        CONSTRAINT [PK_PaymentOrders] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentOrders_Users_ApprovedByUserId] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentOrders_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE INDEX [IX_AiCreditAdjustments_CreatedByUserId] ON [AiCreditAdjustments] ([CreatedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE INDEX [IX_AiCreditAdjustments_UserId_EffectivePeriodStart_EffectivePeriodEnd] ON [AiCreditAdjustments] ([UserId], [EffectivePeriodStart], [EffectivePeriodEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE INDEX [IX_AiSubscriptions_Status_CurrentPeriodEnd] ON [AiSubscriptions] ([Status], [CurrentPeriodEnd]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiSubscriptions_UserId] ON [AiSubscriptions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentOrders_ApprovedByUserId] ON [PaymentOrders] ([ApprovedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentOrders_Status_CreatedAt] ON [PaymentOrders] ([Status], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentOrders_TransferCode] ON [PaymentOrders] ([TransferCode]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    CREATE INDEX [IX_PaymentOrders_UserId] ON [PaymentOrders] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811073650_AddBillingSubscriptionsAndPayments'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811073650_AddBillingSubscriptionsAndPayments', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812053825_AddDailyCheckins'
)
BEGIN
    CREATE TABLE [DailyCheckins] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [CheckinDate] date NOT NULL,
        [Yesterday] nvarchar(4000) NOT NULL,
        [Today] nvarchar(4000) NOT NULL,
        [Blocker] nvarchar(1000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_DailyCheckins] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DailyCheckins_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_DailyCheckins_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812053825_AddDailyCheckins'
)
BEGIN
    CREATE INDEX [IX_DailyCheckins_ProjectId_CheckinDate] ON [DailyCheckins] ([ProjectId], [CheckinDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812053825_AddDailyCheckins'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DailyCheckins_ProjectId_UserId_CheckinDate] ON [DailyCheckins] ([ProjectId], [UserId], [CheckinDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812053825_AddDailyCheckins'
)
BEGIN
    CREATE INDEX [IX_DailyCheckins_UserId_CheckinDate] ON [DailyCheckins] ([UserId], [CheckinDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812053825_AddDailyCheckins'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812053825_AddDailyCheckins', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819002112_AddCollaborationChannelScope'
)
BEGIN
    ALTER TABLE [CollaborationChannels] ADD [ChannelScope] nvarchar(32) NOT NULL DEFAULT N'Private';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819002112_AddCollaborationChannelScope'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_CollaborationChannels_ProjectId_ChannelScope] ON [CollaborationChannels] ([ProjectId], [ChannelScope]) WHERE [ChannelScope] = ''ProjectDiscussion'' AND [IsDeleted] = 0 AND [IsArchived] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819002112_AddCollaborationChannelScope'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260819002112_AddCollaborationChannelScope', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    ALTER TABLE [RefreshTokens] ADD [ProjectInvitationId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    ALTER TABLE [Notifications] ADD [ActionState] nvarchar(32) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    ALTER TABLE [Notifications] ADD [RelatedInvitationId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    CREATE TABLE [ProjectInvitations] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [InvitedByUserId] uniqueidentifier NULL,
        [InvitedEmail] nvarchar(320) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [AcceptedAt] datetime2 NULL,
        [DeclinedAt] datetime2 NULL,
        CONSTRAINT [PK_ProjectInvitations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProjectInvitations_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProjectInvitations_Users_InvitedByUserId] FOREIGN KEY ([InvitedByUserId]) REFERENCES [Users] ([Id]),
        CONSTRAINT [FK_ProjectInvitations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_ProjectInvitationId] ON [RefreshTokens] ([ProjectInvitationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    CREATE INDEX [IX_Notifications_RelatedInvitationId] ON [Notifications] ([RelatedInvitationId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    CREATE INDEX [IX_ProjectInvitations_InvitedByUserId] ON [ProjectInvitations] ([InvitedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    CREATE INDEX [IX_ProjectInvitations_ProjectId_UserId_Status] ON [ProjectInvitations] ([ProjectId], [UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    CREATE INDEX [IX_ProjectInvitations_UserId] ON [ProjectInvitations] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    ALTER TABLE [Notifications] ADD CONSTRAINT [FK_Notifications_ProjectInvitations_RelatedInvitationId] FOREIGN KEY ([RelatedInvitationId]) REFERENCES [ProjectInvitations] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    ALTER TABLE [RefreshTokens] ADD CONSTRAINT [FK_RefreshTokens_ProjectInvitations_ProjectInvitationId] FOREIGN KEY ([ProjectInvitationId]) REFERENCES [ProjectInvitations] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819051727_AddProjectInvitationsAndNotificationActions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260819051727_AddProjectInvitationsAndNotificationActions', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    DROP INDEX [IX_PaymentOrders_UserId] ON [PaymentOrders];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    ALTER TABLE [PaymentOrders] ADD [Currency] nvarchar(8) NOT NULL DEFAULT N'VND';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    ALTER TABLE [PaymentOrders] ADD [ExpiresAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    ALTER TABLE [PaymentOrders] ADD [IncludedAiCreditsSnapshot] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    ALTER TABLE [PaymentOrders] ADD [PlanNameSnapshot] nvarchar(128) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    ALTER TABLE [PaymentOrders] ADD [Provider] nvarchar(64) NOT NULL DEFAULT N'manual_bank_transfer';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN

    UPDATE po
    SET po.Currency = CASE WHEN NULLIF(po.Currency, N'') IS NULL THEN N'VND' ELSE po.Currency END,
        po.Provider = CASE WHEN NULLIF(po.Provider, N'') IS NULL THEN N'manual_bank_transfer' ELSE po.Provider END,
        po.PlanNameSnapshot = CASE WHEN NULLIF(po.PlanNameSnapshot, N'') IS NULL THEN COALESCE(pricing.Name, po.PlanCode) ELSE po.PlanNameSnapshot END,
        po.IncludedAiCreditsSnapshot = CASE WHEN po.IncludedAiCreditsSnapshot = 0 THEN COALESCE(pricing.IncludedAiCredits, 0) ELSE po.IncludedAiCreditsSnapshot END,
        po.ExpiresAt = CASE WHEN po.ExpiresAt IS NULL AND po.Status = N'Pending' THEN DATEADD(minute, 30, po.CreatedAt) ELSE po.ExpiresAt END
    FROM PaymentOrders po
    LEFT JOIN AiPricingPlans pricing ON pricing.Code = po.PlanCode;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE TABLE [AiCreditReservations] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Credits] int NOT NULL,
        [IdempotencyKey] nvarchar(200) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        CONSTRAINT [PK_AiCreditReservations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiCreditReservations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE TABLE [PaymentTransactions] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentOrderId] uniqueidentifier NOT NULL,
        [Provider] nvarchar(64) NOT NULL,
        [ProviderTransactionId] nvarchar(128) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(8) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [PaidAt] datetime2 NOT NULL,
        [ProviderReference] nvarchar(256) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PaymentTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentTransactions_PaymentOrders_PaymentOrderId] FOREIGN KEY ([PaymentOrderId]) REFERENCES [PaymentOrders] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE TABLE [PaymentWebhookEvents] (
        [Id] uniqueidentifier NOT NULL,
        [Provider] nvarchar(64) NOT NULL,
        [ProviderEventId] nvarchar(128) NOT NULL,
        [EventType] nvarchar(64) NOT NULL,
        [RawPayload] nvarchar(max) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ReceivedAt] datetime2 NOT NULL,
        [ProcessedAt] datetime2 NULL,
        CONSTRAINT [PK_PaymentWebhookEvents] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE INDEX [IX_PaymentOrders_UserId_PlanCode_Status] ON [PaymentOrders] ([UserId], [PlanCode], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiCreditReservations_IdempotencyKey] ON [AiCreditReservations] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE INDEX [IX_AiCreditReservations_UserId_Status_ExpiresAt] ON [AiCreditReservations] ([UserId], [Status], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE INDEX [IX_PaymentTransactions_PaymentOrderId] ON [PaymentTransactions] ([PaymentOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentTransactions_Provider_ProviderTransactionId] ON [PaymentTransactions] ([Provider], [ProviderTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentWebhookEvents_Provider_ProviderEventId] ON [PaymentWebhookEvents] ([Provider], [ProviderEventId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821152328_PaymentP0Foundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821152328_PaymentP0Foundation', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    ALTER TABLE [PaymentWebhookEvents] ADD [FailureReason] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    ALTER TABLE [PaymentWebhookEvents] ADD [PaymentOrderId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    ALTER TABLE [PaymentTransactions] ADD [IncludedAiCredits] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    ALTER TABLE [PaymentTransactions] ADD [SubscriptionPeriodEnd] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    ALTER TABLE [PaymentTransactions] ADD [SubscriptionPeriodStart] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    ALTER TABLE [Notifications] ADD [DedupeKey] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    CREATE TABLE [PaymentEmailDeliveries] (
        [Id] uniqueidentifier NOT NULL,
        [PaymentOrderId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [RecipientEmail] nvarchar(320) NOT NULL,
        [Kind] nvarchar(64) NOT NULL,
        [IsAutomatic] bit NOT NULL,
        [Attempt] int NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ProviderMessageId] nvarchar(128) NULL,
        [FailureReason] nvarchar(500) NULL,
        [RequestedAt] datetime2 NOT NULL,
        [SentAt] datetime2 NULL,
        [FailedAt] datetime2 NULL,
        CONSTRAINT [PK_PaymentEmailDeliveries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentEmailDeliveries_PaymentOrders_PaymentOrderId] FOREIGN KEY ([PaymentOrderId]) REFERENCES [PaymentOrders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PaymentEmailDeliveries_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    CREATE INDEX [IX_PaymentWebhookEvents_PaymentOrderId] ON [PaymentWebhookEvents] ([PaymentOrderId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Notifications_DedupeKey] ON [Notifications] ([DedupeKey]) WHERE [DedupeKey] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PaymentEmailDeliveries_PaymentOrderId_Kind_Attempt] ON [PaymentEmailDeliveries] ([PaymentOrderId], [Kind], [Attempt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PaymentEmailDeliveries_PaymentOrderId_Kind_IsAutomatic] ON [PaymentEmailDeliveries] ([PaymentOrderId], [Kind], [IsAutomatic]) WHERE [IsAutomatic] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    CREATE INDEX [IX_PaymentEmailDeliveries_UserId] ON [PaymentEmailDeliveries] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822043920_BillingSupport'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822043920_BillingSupport', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    ALTER TABLE [ChannelMessages] ADD [ReplyToMessageId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE TABLE [CollaborationMessagePins] (
        [Id] uniqueidentifier NOT NULL,
        [ChannelMessageId] uniqueidentifier NOT NULL,
        [PinnedByUserId] uniqueidentifier NOT NULL,
        [PinnedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CollaborationMessagePins] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CollaborationMessagePins_ChannelMessages_ChannelMessageId] FOREIGN KEY ([ChannelMessageId]) REFERENCES [ChannelMessages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CollaborationMessagePins_Users_PinnedByUserId] FOREIGN KEY ([PinnedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE TABLE [CollaborationMessageReactions] (
        [Id] uniqueidentifier NOT NULL,
        [ChannelMessageId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Emoji] nvarchar(32) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CollaborationMessageReactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CollaborationMessageReactions_ChannelMessages_ChannelMessageId] FOREIGN KEY ([ChannelMessageId]) REFERENCES [ChannelMessages] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CollaborationMessageReactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE INDEX [IX_ChannelMessages_ReplyToMessageId] ON [ChannelMessages] ([ReplyToMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CollaborationMessagePins_ChannelMessageId] ON [CollaborationMessagePins] ([ChannelMessageId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE INDEX [IX_CollaborationMessagePins_ChannelMessageId_PinnedAt] ON [CollaborationMessagePins] ([ChannelMessageId], [PinnedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE INDEX [IX_CollaborationMessagePins_PinnedByUserId] ON [CollaborationMessagePins] ([PinnedByUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE INDEX [IX_CollaborationMessageReactions_ChannelMessageId_Emoji] ON [CollaborationMessageReactions] ([ChannelMessageId], [Emoji]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CollaborationMessageReactions_ChannelMessageId_UserId_Emoji] ON [CollaborationMessageReactions] ([ChannelMessageId], [UserId], [Emoji]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    CREATE INDEX [IX_CollaborationMessageReactions_UserId] ON [CollaborationMessageReactions] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    ALTER TABLE [ChannelMessages] ADD CONSTRAINT [FK_ChannelMessages_ChannelMessages_ReplyToMessageId] FOREIGN KEY ([ReplyToMessageId]) REFERENCES [ChannelMessages] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822065924_ChatComms1MessageInteractions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822065924_ChatComms1MessageInteractions', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822131316_ChatAi2CallTranscriptFoundation'
)
BEGIN
    CREATE TABLE [CallTranscriptChunks] (
        [Id] uniqueidentifier NOT NULL,
        [CallSessionId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [VoiceChannelId] nvarchar(200) NOT NULL,
        [SpeakerUserId] uniqueidentifier NOT NULL,
        [SpeakerDisplayName] nvarchar(256) NOT NULL,
        [StartedAt] datetimeoffset NOT NULL,
        [EndedAt] datetimeoffset NOT NULL,
        [Text] nvarchar(max) NOT NULL,
        [Confidence] float NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_CallTranscriptChunks] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822131316_ChatAi2CallTranscriptFoundation'
)
BEGIN
    CREATE INDEX [IX_CallTranscriptChunks_CallSessionId_CreatedAt] ON [CallTranscriptChunks] ([CallSessionId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822131316_ChatAi2CallTranscriptFoundation'
)
BEGIN
    CREATE INDEX [IX_CallTranscriptChunks_ProjectId_VoiceChannelId_CallSessionId_StartedAt] ON [CallTranscriptChunks] ([ProjectId], [VoiceChannelId], [CallSessionId], [StartedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260822131316_ChatAi2CallTranscriptFoundation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260822131316_ChatAi2CallTranscriptFoundation', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823031856_AddAiCreditBuckets'
)
BEGIN
    CREATE TABLE [AiCreditBuckets] (
        [Id] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [PlanCode] nvarchar(64) NOT NULL,
        [GrantedCredits] int NOT NULL,
        [RemainingCredits] int NOT NULL,
        [ValidFrom] datetime2 NOT NULL,
        [ExpiresAt] datetime2 NOT NULL,
        [SourceType] nvarchar(32) NOT NULL,
        [SourcePaymentOrderId] uniqueidentifier NULL,
        [SourceReference] nvarchar(200) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiCreditBuckets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiCreditBuckets_PaymentOrders_SourcePaymentOrderId] FOREIGN KEY ([SourcePaymentOrderId]) REFERENCES [PaymentOrders] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AiCreditBuckets_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823031856_AddAiCreditBuckets'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AiCreditBuckets_SourcePaymentOrderId] ON [AiCreditBuckets] ([SourcePaymentOrderId]) WHERE [SourcePaymentOrderId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823031856_AddAiCreditBuckets'
)
BEGIN
    CREATE INDEX [IX_AiCreditBuckets_UserId_ExpiresAt_CreatedAt] ON [AiCreditBuckets] ([UserId], [ExpiresAt], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823031856_AddAiCreditBuckets'
)
BEGIN
    CREATE INDEX [IX_AiCreditBuckets_UserId_ValidFrom_ExpiresAt] ON [AiCreditBuckets] ([UserId], [ValidFrom], [ExpiresAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823031856_AddAiCreditBuckets'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823031856_AddAiCreditBuckets', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    ALTER TABLE [AiCreditBuckets] ADD [RowVersion] rowversion NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    ALTER TABLE [AiCreditReservations] ADD [RequestedCredits] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    ALTER TABLE [AiCreditReservations] ADD [ReservedCredits] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    ALTER TABLE [AiCreditReservations] ADD [FinalizedCredits] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    ALTER TABLE [AiCreditReservations] ADD [FinalizedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    ALTER TABLE [AiCreditReservations] ADD [ReleasedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    UPDATE AiCreditReservations SET RequestedCredits = Credits, ReservedCredits = Credits WHERE RequestedCredits = 0
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    CREATE TABLE [AiCreditReservationAllocations] (
        [Id] uniqueidentifier NOT NULL,
        [ReservationId] uniqueidentifier NOT NULL,
        [CreditBucketId] uniqueidentifier NOT NULL,
        [AllocatedCredits] int NOT NULL,
        [ConsumedCredits] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_AiCreditReservationAllocations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AiCreditReservationAllocations_AiCreditBuckets_CreditBucketId] FOREIGN KEY ([CreditBucketId]) REFERENCES [AiCreditBuckets] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_AiCreditReservationAllocations_AiCreditReservations_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [AiCreditReservations] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    CREATE INDEX [IX_AiCreditReservationAllocations_CreditBucketId] ON [AiCreditReservationAllocations] ([CreditBucketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiCreditReservationAllocations_ReservationId_CreditBucketId] ON [AiCreditReservationAllocations] ([ReservationId], [CreditBucketId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AiCreditReservations_UserId_IdempotencyKey] ON [AiCreditReservations] ([UserId], [IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823033910_AddAiCreditReservations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823033910_AddAiCreditReservations', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823042404_AddAiCreditCutoverUniqueness'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_AiCreditBuckets_SourceType_SourceReference] ON [AiCreditBuckets] ([SourceType], [SourceReference]) WHERE [SourceReference] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823042404_AddAiCreditCutoverUniqueness'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823042404_AddAiCreditCutoverUniqueness', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823130847_AddCallSessionChat'
)
BEGIN
    CREATE TABLE [CallChatMessages] (
        [Id] uniqueidentifier NOT NULL,
        [CallSessionId] uniqueidentifier NOT NULL,
        [RoomId] nvarchar(300) NOT NULL,
        [SenderUserId] uniqueidentifier NOT NULL,
        [Content] nvarchar(4000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CallChatMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CallChatMessages_Users_SenderUserId] FOREIGN KEY ([SenderUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823130847_AddCallSessionChat'
)
BEGIN
    CREATE INDEX [IX_CallChatMessages_CallSessionId_CreatedAt] ON [CallChatMessages] ([CallSessionId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823130847_AddCallSessionChat'
)
BEGIN
    CREATE INDEX [IX_CallChatMessages_RoomId_CreatedAt] ON [CallChatMessages] ([RoomId], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823130847_AddCallSessionChat'
)
BEGIN
    CREATE INDEX [IX_CallChatMessages_SenderUserId] ON [CallChatMessages] ([SenderUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823130847_AddCallSessionChat'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823130847_AddCallSessionChat', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825122904_AddMeetingAiReports'
)
BEGIN
    CREATE TABLE [MeetingAiReports] (
        [Id] uniqueidentifier NOT NULL,
        [CallSessionId] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [VoiceChannelId] nvarchar(200) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [ProcessedTranscriptChunkCount] int NOT NULL,
        [StateJson] nvarchar(max) NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        [CompletedAt] datetimeoffset NULL,
        CONSTRAINT [PK_MeetingAiReports] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825122904_AddMeetingAiReports'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MeetingAiReports_CallSessionId] ON [MeetingAiReports] ([CallSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825122904_AddMeetingAiReports'
)
BEGIN
    CREATE INDEX [IX_MeetingAiReports_ProjectId_VoiceChannelId_UpdatedAt] ON [MeetingAiReports] ([ProjectId], [VoiceChannelId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825122904_AddMeetingAiReports'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825122904_AddMeetingAiReports', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE TABLE [RewardSeasons] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Type] nvarchar(32) NOT NULL,
        [SprintId] uniqueidentifier NULL,
        [StartAt] datetimeoffset NOT NULL,
        [EndAt] datetimeoffset NULL,
        [TimeZone] nvarchar(80) NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [AllowSelfApproval] bit NOT NULL,
        [CreatedBy] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [ClosedAt] datetimeoffset NULL,
        CONSTRAINT [PK_RewardSeasons] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RewardSeasons_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RewardSeasons_Sprints_SprintId] FOREIGN KEY ([SprintId]) REFERENCES [Sprints] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE TABLE [RewardDefinitions] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SeasonId] uniqueidentifier NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [RewardType] nvarchar(32) NOT NULL,
        [DisplayValue] decimal(18,2) NULL,
        [Currency] nvarchar(8) NULL,
        [ConditionType] nvarchar(32) NOT NULL,
        [ConditionMetric] nvarchar(40) NOT NULL,
        [Threshold] decimal(18,2) NOT NULL,
        [RankFrom] int NULL,
        [RankTo] int NULL,
        [RequireActiveMemberAtSettlement] bit NOT NULL,
        [IsEnabled] bit NOT NULL,
        [CreatedBy] uniqueidentifier NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RewardDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RewardDefinitions_RewardSeasons_SeasonId] FOREIGN KEY ([SeasonId]) REFERENCES [RewardSeasons] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE TABLE [RewardPointEvents] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SeasonId] uniqueidentifier NOT NULL,
        [WorkTaskId] uniqueidentifier NOT NULL,
        [UserId] uniqueidentifier NOT NULL,
        [Points] int NOT NULL,
        [Xp] int NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [EventType] nvarchar(40) NOT NULL,
        [ScoreSource] nvarchar(40) NOT NULL,
        [DifficultySnapshot] nvarchar(8) NOT NULL,
        [CompletedAt] datetimeoffset NOT NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [FinalizedAt] datetimeoffset NULL,
        [FinalizedBy] uniqueidentifier NULL,
        [CancelledAt] datetimeoffset NULL,
        [CancelledBy] uniqueidentifier NULL,
        [CancellationReason] nvarchar(500) NULL,
        [IdempotencyKey] nvarchar(180) NOT NULL,
        CONSTRAINT [PK_RewardPointEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RewardPointEvents_RewardSeasons_SeasonId] FOREIGN KEY ([SeasonId]) REFERENCES [RewardSeasons] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RewardPointEvents_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RewardPointEvents_WorkTasks_WorkTaskId] FOREIGN KEY ([WorkTaskId]) REFERENCES [WorkTasks] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE TABLE [RewardGrants] (
        [Id] uniqueidentifier NOT NULL,
        [ProjectId] uniqueidentifier NOT NULL,
        [SeasonId] uniqueidentifier NOT NULL,
        [RewardDefinitionId] uniqueidentifier NOT NULL,
        [RecipientUserId] uniqueidentifier NOT NULL,
        [Status] nvarchar(32) NOT NULL,
        [RequiresManagerResolution] bit NOT NULL,
        [ManagerNote] nvarchar(1000) NULL,
        [EarnedAt] datetimeoffset NOT NULL,
        [FulfilledAt] datetimeoffset NULL,
        [FulfilledBy] uniqueidentifier NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        CONSTRAINT [PK_RewardGrants] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RewardGrants_RewardDefinitions_RewardDefinitionId] FOREIGN KEY ([RewardDefinitionId]) REFERENCES [RewardDefinitions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_RewardGrants_RewardSeasons_SeasonId] FOREIGN KEY ([SeasonId]) REFERENCES [RewardSeasons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RewardGrants_Users_RecipientUserId] FOREIGN KEY ([RecipientUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE INDEX [IX_RewardDefinitions_SeasonId_IsEnabled] ON [RewardDefinitions] ([SeasonId], [IsEnabled]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE INDEX [IX_RewardGrants_RecipientUserId_Status] ON [RewardGrants] ([RecipientUserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RewardGrants_RewardDefinitionId_SeasonId_RecipientUserId] ON [RewardGrants] ([RewardDefinitionId], [SeasonId], [RecipientUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE INDEX [IX_RewardGrants_SeasonId] ON [RewardGrants] ([SeasonId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RewardPointEvents_IdempotencyKey] ON [RewardPointEvents] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE INDEX [IX_RewardPointEvents_SeasonId_UserId_Status] ON [RewardPointEvents] ([SeasonId], [UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE INDEX [IX_RewardPointEvents_UserId] ON [RewardPointEvents] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RewardPointEvents_WorkTaskId_UserId] ON [RewardPointEvents] ([WorkTaskId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE INDEX [IX_RewardSeasons_ProjectId_StartAt] ON [RewardSeasons] ([ProjectId], [StartAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE INDEX [IX_RewardSeasons_ProjectId_Status] ON [RewardSeasons] ([ProjectId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    CREATE INDEX [IX_RewardSeasons_SprintId] ON [RewardSeasons] ([SprintId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829115626_SprintARewardSystemV1'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829115626_SprintARewardSystemV1', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829122038_RewardEventDueDateSnapshot'
)
BEGIN
    ALTER TABLE [RewardPointEvents] ADD [DueDateSnapshot] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829122038_RewardEventDueDateSnapshot'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829122038_RewardEventDueDateSnapshot', N'10.0.9');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830121918_AddRewardShopFields'
)
BEGIN
    ALTER TABLE [RewardDefinitions] ADD [ClaimLimit] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830121918_AddRewardShopFields'
)
BEGIN
    ALTER TABLE [RewardDefinitions] ADD [EndAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830121918_AddRewardShopFields'
)
BEGIN
    ALTER TABLE [RewardDefinitions] ADD [Method] nvarchar(max) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830121918_AddRewardShopFields'
)
BEGIN
    ALTER TABLE [RewardDefinitions] ADD [PointCost] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830121918_AddRewardShopFields'
)
BEGIN
    ALTER TABLE [RewardDefinitions] ADD [Quantity] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830121918_AddRewardShopFields'
)
BEGIN
    ALTER TABLE [RewardDefinitions] ADD [StartAt] datetimeoffset NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260830121918_AddRewardShopFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260830121918_AddRewardShopFields', N'10.0.9');
END;

COMMIT;
GO

