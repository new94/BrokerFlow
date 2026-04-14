-- ============================================================================
-- BrokerFlow — Database Initialization Script for MS SQL Server
-- Run this BEFORE starting the application for the first time,
-- OR let EF Core auto-migrate on startup.
-- ============================================================================

-- 1. Create the database (run from master)
-- USE [master]
-- GO
-- CREATE DATABASE [BrokerFlow]
-- GO

-- 2. Create a dedicated login/user (optional, recommended for production)
-- USE [master]
-- GO
-- CREATE LOGIN [brokerflow_user] WITH PASSWORD = 'YourStrongPassword123!';
-- GO
-- USE [BrokerFlow]
-- GO
-- CREATE USER [brokerflow_user] FOR LOGIN [brokerflow_user];
-- ALTER ROLE [db_owner] ADD MEMBER [brokerflow_user];
-- GO

-- 3. Tables (EF Core will create these automatically via migrations,
--    but you can run this manually if preferred)

USE [BrokerFlow]
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sources')
CREATE TABLE [dbo].[Sources] (
    [Id]                NVARCHAR(450)   NOT NULL PRIMARY KEY,
    [Name]              NVARCHAR(200)   NOT NULL,
    [Path]              NVARCHAR(500)   NOT NULL,
    [FileMask]          NVARCHAR(200)   NOT NULL DEFAULT '*.*',
    [FileFormat]        NVARCHAR(20)    NOT NULL DEFAULT 'auto',
    [CsvSeparator]      NVARCHAR(10)    NULL,
    [CsvCustomSeparator] NVARCHAR(10)   NULL,
    [Enabled]           BIT             NOT NULL DEFAULT 1,
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]         DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'XmlTemplates')
CREATE TABLE [dbo].[XmlTemplates] (
    [Id]        NVARCHAR(450)   NOT NULL PRIMARY KEY,
    [Name]      NVARCHAR(200)   NOT NULL,
    [Content]   NVARCHAR(MAX)   NOT NULL,
    [FieldsJson] NVARCHAR(MAX)  NULL,
    [CreatedAt] DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MappingConfigs')
CREATE TABLE [dbo].[MappingConfigs] (
    [Id]                    NVARCHAR(450)   NOT NULL PRIMARY KEY,
    [Name]                  NVARCHAR(200)   NOT NULL,
    [SourceId]              NVARCHAR(MAX)   NULL,
    [TemplateId]            NVARCHAR(MAX)   NULL,
    [RulesJson]             NVARCHAR(MAX)   NOT NULL DEFAULT '[]',
    [XmlTemplate]           NVARCHAR(MAX)   NULL,
    [SplitOutput]           BIT             NOT NULL DEFAULT 0,
    [SplitConditionJson]    NVARCHAR(MAX)   NULL,
    [SplitFileNamePattern]  NVARCHAR(500)   NULL,
    [CreatedAt]             DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt]             DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Schedules')
CREATE TABLE [dbo].[Schedules] (
    [Id]                NVARCHAR(450)   NOT NULL PRIMARY KEY,
    [Name]              NVARCHAR(200)   NOT NULL,
    [SourceId]          NVARCHAR(MAX)   NULL,
    [MappingId]         NVARCHAR(MAX)   NULL,
    [CronExpression]    NVARCHAR(100)   NOT NULL DEFAULT '0 */5 * * *',
    [Enabled]           BIT             NOT NULL DEFAULT 1,
    [LastRunAt]         DATETIME2       NULL,
    [NextRunAt]         DATETIME2       NULL,
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProcessingJobs')
CREATE TABLE [dbo].[ProcessingJobs] (
    [Id]                NVARCHAR(450)   NOT NULL PRIMARY KEY,
    [SourceId]          NVARCHAR(MAX)   NULL,
    [MappingId]         NVARCHAR(MAX)   NULL,
    [FilePath]          NVARCHAR(500)   NULL,
    [OriginalFileName]  NVARCHAR(500)   NULL,
    [Status]            NVARCHAR(50)    NOT NULL DEFAULT 'pending',
    [ResultPath]        NVARCHAR(MAX)   NULL,
    [ErrorMessage]      NVARCHAR(MAX)   NULL,
    [RecordsProcessed]  INT             NOT NULL DEFAULT 0,
    [FilesGenerated]    INT             NOT NULL DEFAULT 0,
    [StartedAt]         DATETIME2       NULL,
    [FinishedAt]        DATETIME2       NULL,
    [CreatedAt]         DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO
CREATE NONCLUSTERED INDEX IX_ProcessingJobs_Status ON [dbo].[ProcessingJobs]([Status]);
CREATE NONCLUSTERED INDEX IX_ProcessingJobs_CreatedAt ON [dbo].[ProcessingJobs]([CreatedAt] DESC);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditEntries')
CREATE TABLE [dbo].[AuditEntries] (
    [Id]            BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Action]        NVARCHAR(50)    NOT NULL,
    [EntityType]    NVARCHAR(200)   NULL,
    [EntityId]      NVARCHAR(100)   NULL,
    [Details]       NVARCHAR(MAX)   NULL,
    [CreatedAt]     DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO
CREATE NONCLUSTERED INDEX IX_AuditEntries_CreatedAt ON [dbo].[AuditEntries]([CreatedAt] DESC);
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppConfigs')
CREATE TABLE [dbo].[AppConfigs] (
    [Key]       NVARCHAR(100)   NOT NULL PRIMARY KEY,
    [Value]     NVARCHAR(MAX)   NOT NULL,
    [UpdatedAt] DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO

-- EF Core migrations tracking table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '__EFMigrationsHistory')
CREATE TABLE [dbo].[__EFMigrationsHistory] (
    [MigrationId]       NVARCHAR(150) NOT NULL PRIMARY KEY,
    [ProductVersion]    NVARCHAR(32) NOT NULL
);
GO

-- Mark initial migration as applied (if you created tables manually)
IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE MigrationId = '20240101000000_InitialCreate')
INSERT INTO [dbo].[__EFMigrationsHistory] (MigrationId, ProductVersion)
VALUES ('20240101000000_InitialCreate', '8.0.11');
GO

PRINT 'BrokerFlow database initialized successfully.';
GO
