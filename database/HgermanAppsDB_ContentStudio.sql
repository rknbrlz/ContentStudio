/*
  Hgerman Content Studio - Azure SQL bootstrap script
  Target database: HgermanAppsDB
  Run this script while connected to the HgermanAppsDB database.
*/

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* =========================
   Drop objects safely
   ========================= */
IF OBJECT_ID('dbo.CS_ErrorLog', 'U') IS NOT NULL DROP TABLE dbo.CS_ErrorLog;
IF OBJECT_ID('dbo.CS_PublishTask', 'U') IS NOT NULL DROP TABLE dbo.CS_PublishTask;
IF OBJECT_ID('dbo.CS_Asset', 'U') IS NOT NULL DROP TABLE dbo.CS_Asset;
IF OBJECT_ID('dbo.CS_VideoScene', 'U') IS NOT NULL DROP TABLE dbo.CS_VideoScene;
IF OBJECT_ID('dbo.CS_VideoJob', 'U') IS NOT NULL DROP TABLE dbo.CS_VideoJob;
IF OBJECT_ID('dbo.CS_PromptTemplate', 'U') IS NOT NULL DROP TABLE dbo.CS_PromptTemplate;
IF OBJECT_ID('dbo.CS_AppSetting', 'U') IS NOT NULL DROP TABLE dbo.CS_AppSetting;
IF OBJECT_ID('dbo.CS_Project', 'U') IS NOT NULL DROP TABLE dbo.CS_Project;
GO

/* =========================
   Create tables
   ========================= */
CREATE TABLE dbo.CS_Project
(
    ProjectId            INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProjectName          NVARCHAR(150) NOT NULL,
    Description          NVARCHAR(500) NULL,
    IsActive             BIT NOT NULL CONSTRAINT DF_CS_Project_IsActive DEFAULT(1),
    CreatedDate          DATETIME2(0) NOT NULL CONSTRAINT DF_CS_Project_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate          DATETIME2(0) NULL
);
GO

CREATE TABLE dbo.CS_VideoJob
(
    VideoJobId           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ProjectId            INT NOT NULL,
    JobNo                NVARCHAR(40) NOT NULL,
    Title                NVARCHAR(200) NOT NULL,
    Topic                NVARCHAR(500) NULL,
    SourcePrompt         NVARCHAR(MAX) NULL,
    LanguageCode         NVARCHAR(10) NOT NULL,
    PlatformType         INT NOT NULL,
    ToneType             INT NOT NULL,
    DurationTargetSec    INT NOT NULL CONSTRAINT DF_CS_VideoJob_DurationTargetSec DEFAULT(45),
    AspectRatio          INT NOT NULL CONSTRAINT DF_CS_VideoJob_AspectRatio DEFAULT(1),
    VoiceProvider        NVARCHAR(50) NULL,
    VoiceName            NVARCHAR(100) NULL,
    SubtitleEnabled      BIT NOT NULL CONSTRAINT DF_CS_VideoJob_SubtitleEnabled DEFAULT(1),
    ThumbnailEnabled     BIT NOT NULL CONSTRAINT DF_CS_VideoJob_ThumbnailEnabled DEFAULT(1),
    Status               INT NOT NULL CONSTRAINT DF_CS_VideoJob_Status DEFAULT(1),
    CurrentStep          INT NOT NULL CONSTRAINT DF_CS_VideoJob_CurrentStep DEFAULT(1),
    ErrorMessage         NVARCHAR(2000) NULL,
    RetryCount           INT NOT NULL CONSTRAINT DF_CS_VideoJob_RetryCount DEFAULT(0),
    CreatedDate          DATETIME2(0) NOT NULL CONSTRAINT DF_CS_VideoJob_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate          DATETIME2(0) NULL,
    CompletedDate        DATETIME2(0) NULL,
    CONSTRAINT FK_CS_VideoJob_CS_Project FOREIGN KEY (ProjectId) REFERENCES dbo.CS_Project(ProjectId)
);
GO

CREATE UNIQUE INDEX UX_CS_VideoJob_JobNo ON dbo.CS_VideoJob(JobNo);
CREATE INDEX IX_CS_VideoJob_ProjectId_Status ON dbo.CS_VideoJob(ProjectId, Status, VideoJobId DESC);
GO

CREATE TABLE dbo.CS_VideoScene
(
    VideoSceneId         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    VideoJobId           INT NOT NULL,
    SceneNo              INT NOT NULL,
    SceneText            NVARCHAR(2000) NOT NULL,
    ScenePrompt          NVARCHAR(2000) NULL,
    ImageAssetId         INT NULL,
    StartSecond          DECIMAL(10,2) NOT NULL CONSTRAINT DF_CS_VideoScene_StartSecond DEFAULT(0),
    EndSecond            DECIMAL(10,2) NOT NULL CONSTRAINT DF_CS_VideoScene_EndSecond DEFAULT(0),
    DurationSecond       DECIMAL(10,2) NOT NULL CONSTRAINT DF_CS_VideoScene_DurationSecond DEFAULT(0),
    TransitionType       NVARCHAR(50) NULL,
    Status               INT NOT NULL CONSTRAINT DF_CS_VideoScene_Status DEFAULT(1),
    CreatedDate          DATETIME2(0) NOT NULL CONSTRAINT DF_CS_VideoScene_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate          DATETIME2(0) NULL,
    CONSTRAINT FK_CS_VideoScene_CS_VideoJob FOREIGN KEY (VideoJobId) REFERENCES dbo.CS_VideoJob(VideoJobId)
);
GO

CREATE UNIQUE INDEX UX_CS_VideoScene_VideoJobId_SceneNo ON dbo.CS_VideoScene(VideoJobId, SceneNo);
GO

CREATE TABLE dbo.CS_Asset
(
    AssetId              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    VideoJobId           INT NOT NULL,
    VideoSceneId         INT NULL,
    AssetType            INT NOT NULL,
    ProviderName         NVARCHAR(100) NULL,
    FileName             NVARCHAR(260) NOT NULL,
    BlobPath             NVARCHAR(500) NOT NULL,
    PublicUrl            NVARCHAR(1000) NULL,
    MimeType             NVARCHAR(100) NULL,
    FileSize             BIGINT NULL,
    Width                INT NULL,
    Height               INT NULL,
    DurationMs           INT NULL,
    Status               INT NOT NULL CONSTRAINT DF_CS_Asset_Status DEFAULT(1),
    CreatedDate          DATETIME2(0) NOT NULL CONSTRAINT DF_CS_Asset_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate          DATETIME2(0) NULL,
    CONSTRAINT FK_CS_Asset_CS_VideoJob FOREIGN KEY (VideoJobId) REFERENCES dbo.CS_VideoJob(VideoJobId),
    CONSTRAINT FK_CS_Asset_CS_VideoScene FOREIGN KEY (VideoSceneId) REFERENCES dbo.CS_VideoScene(VideoSceneId)
);
GO

CREATE INDEX IX_CS_Asset_VideoJobId_AssetType ON dbo.CS_Asset(VideoJobId, AssetType, AssetId DESC);
GO

ALTER TABLE dbo.CS_VideoScene
ADD CONSTRAINT FK_CS_VideoScene_ImageAssetId FOREIGN KEY (ImageAssetId) REFERENCES dbo.CS_Asset(AssetId);
GO

CREATE TABLE dbo.CS_PromptTemplate
(
    PromptTemplateId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    TemplateName         NVARCHAR(150) NOT NULL,
    TemplateType         NVARCHAR(50) NOT NULL,
    LanguageCode         NVARCHAR(10) NOT NULL,
    SystemPrompt         NVARCHAR(MAX) NOT NULL,
    UserPromptFormat     NVARCHAR(MAX) NOT NULL,
    IsDefault            BIT NOT NULL CONSTRAINT DF_CS_PromptTemplate_IsDefault DEFAULT(0),
    IsActive             BIT NOT NULL CONSTRAINT DF_CS_PromptTemplate_IsActive DEFAULT(1),
    CreatedDate          DATETIME2(0) NOT NULL CONSTRAINT DF_CS_PromptTemplate_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate          DATETIME2(0) NULL
);
GO

CREATE INDEX IX_CS_PromptTemplate_TemplateType_LanguageCode ON dbo.CS_PromptTemplate(TemplateType, LanguageCode, IsActive);
GO

CREATE TABLE dbo.CS_PublishTask
(
    PublishTaskId        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    VideoJobId           INT NOT NULL,
    PlatformType         INT NOT NULL,
    Title                NVARCHAR(200) NOT NULL,
    Description          NVARCHAR(MAX) NULL,
    Tags                 NVARCHAR(1000) NULL,
    ThumbnailAssetId     INT NULL,
    PublishStatus        INT NOT NULL CONSTRAINT DF_CS_PublishTask_PublishStatus DEFAULT(1),
    PlatformVideoId      NVARCHAR(200) NULL,
    PublishUrl           NVARCHAR(1000) NULL,
    ScheduledDate        DATETIME2(0) NULL,
    PublishedDate        DATETIME2(0) NULL,
    ErrorMessage         NVARCHAR(2000) NULL,
    CreatedDate          DATETIME2(0) NOT NULL CONSTRAINT DF_CS_PublishTask_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate          DATETIME2(0) NULL,
    CONSTRAINT FK_CS_PublishTask_CS_VideoJob FOREIGN KEY (VideoJobId) REFERENCES dbo.CS_VideoJob(VideoJobId),
    CONSTRAINT FK_CS_PublishTask_ThumbnailAsset FOREIGN KEY (ThumbnailAssetId) REFERENCES dbo.CS_Asset(AssetId)
);
GO

CREATE INDEX IX_CS_PublishTask_VideoJobId_PublishStatus ON dbo.CS_PublishTask(VideoJobId, PublishStatus);
GO

CREATE TABLE dbo.CS_AppSetting
(
    AppSettingId         INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SettingKey           NVARCHAR(150) NOT NULL,
    SettingValue         NVARCHAR(MAX) NULL,
    SettingGroup         NVARCHAR(100) NOT NULL,
    IsEncrypted          BIT NOT NULL CONSTRAINT DF_CS_AppSetting_IsEncrypted DEFAULT(0),
    CreatedDate          DATETIME2(0) NOT NULL CONSTRAINT DF_CS_AppSetting_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate          DATETIME2(0) NULL
);
GO

CREATE UNIQUE INDEX UX_CS_AppSetting_SettingKey ON dbo.CS_AppSetting(SettingKey);
GO

CREATE TABLE dbo.CS_ErrorLog
(
    ErrorLogId           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    VideoJobId           INT NULL,
    StepName             NVARCHAR(100) NOT NULL,
    ErrorType            NVARCHAR(100) NULL,
    ErrorMessage         NVARCHAR(4000) NOT NULL,
    StackTrace           NVARCHAR(MAX) NULL,
    RawResponse          NVARCHAR(MAX) NULL,
    CreatedDate          DATETIME2(0) NOT NULL CONSTRAINT DF_CS_ErrorLog_CreatedDate DEFAULT (SYSUTCDATETIME()),
    UpdatedDate          DATETIME2(0) NULL,
    CONSTRAINT FK_CS_ErrorLog_CS_VideoJob FOREIGN KEY (VideoJobId) REFERENCES dbo.CS_VideoJob(VideoJobId)
);
GO

CREATE INDEX IX_CS_ErrorLog_VideoJobId_CreatedDate ON dbo.CS_ErrorLog(VideoJobId, CreatedDate DESC);
GO

/* =========================
   Seed data
   ========================= */
INSERT INTO dbo.CS_Project (ProjectName, Description, IsActive)
VALUES
('Hgerman Content Studio', 'Default project for YouTube Shorts, Reels and TikTok video generation.', 1);
GO

INSERT INTO dbo.CS_PromptTemplate
(
    TemplateName,
    TemplateType,
    LanguageCode,
    SystemPrompt,
    UserPromptFormat,
    IsDefault,
    IsActive
)
VALUES
(
    'Motivational Shorts EN',
    'Motivational',
    'en',
    'You are an expert short-form video scriptwriter. Return concise scene-ready narration.',
    'Topic: {{topic}}; Tone: {{tone}}; DurationSeconds: {{duration}}; Platform: {{platform}}',
    1,
    1
),
(
    'Motivational Shorts PL',
    'Motivational',
    'pl',
    'Jestes ekspertem od krotkich skryptow wideo. Zwroc zwięzly tekst gotowy do podzialu na sceny.',
    'Temat: {{topic}}; Ton: {{tone}}; Czas trwania: {{duration}}; Platforma: {{platform}}',
    0,
    1
),
(
    'Jewelry Showcase EN',
    'ProductShowcase',
    'en',
    'You write premium short scripts for luxury jewelry product videos.',
    'Product: {{topic}}; Tone: luxury; DurationSeconds: {{duration}}; Platform: {{platform}}',
    0,
    1
);
GO

INSERT INTO dbo.CS_AppSetting (SettingKey, SettingValue, SettingGroup, IsEncrypted)
VALUES
('OpenAI:Model', 'gpt-5', 'AI', 0),
('Storage:ContainerName', 'content-studio', 'Storage', 0),
('Video:DefaultAspectRatio', 'Vertical916', 'Video', 0),
('Video:DefaultDurationSec', '45', 'Video', 0);
GO

/* =========================
   Helpful view
   ========================= */
CREATE OR ALTER VIEW dbo.V_CS_VideoJobDashboard
AS
SELECT
    j.VideoJobId,
    j.JobNo,
    j.Title,
    j.LanguageCode,
    j.PlatformType,
    j.ToneType,
    j.DurationTargetSec,
    j.Status,
    j.CurrentStep,
    j.CreatedDate,
    j.UpdatedDate,
    j.CompletedDate,
    p.ProjectName,
    SceneCount = (SELECT COUNT(1) FROM dbo.CS_VideoScene s WHERE s.VideoJobId = j.VideoJobId),
    AssetCount = (SELECT COUNT(1) FROM dbo.CS_Asset a WHERE a.VideoJobId = j.VideoJobId),
    ErrorCount = (SELECT COUNT(1) FROM dbo.CS_ErrorLog e WHERE e.VideoJobId = j.VideoJobId)
FROM dbo.CS_VideoJob j
INNER JOIN dbo.CS_Project p ON p.ProjectId = j.ProjectId;
GO

/* =========================
   Worker helper proc
   ========================= */
CREATE OR ALTER PROCEDURE dbo.CS_GetNextQueuedVideoJob
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        VideoJobId,
        ProjectId,
        JobNo,
        Title,
        Topic,
        LanguageCode,
        PlatformType,
        ToneType,
        DurationTargetSec,
        AspectRatio,
        Status,
        CurrentStep,
        RetryCount,
        CreatedDate,
        UpdatedDate
    FROM dbo.CS_VideoJob
    WHERE Status = 2
    ORDER BY VideoJobId ASC;
END;
GO

/* =========================
   Enum reference comments
   =========================
   PlatformType:
     1 = YouTubeShorts
     2 = InstagramReels
     3 = TikTok

   ToneType:
     1 = Emotional
     2 = Inspirational
     3 = Educational
     4 = Luxury
     5 = Soft
     6 = Dramatic

   AspectRatio:
     1 = Vertical916
     2 = Square11
     3 = Landscape169

   VideoJobStatus:
     1 = Draft
     2 = Queued
     3 = Processing
     4 = Completed
     5 = Failed
     6 = Cancelled

   VideoPipelineStep:
     1  = Draft
     2  = ScriptGenerating
     3  = ScriptReady
     4  = ScenePlanning
     5  = SceneReady
     6  = ImagePromptGenerating
     7  = ImageGenerating
     8  = ImagesReady
     9  = VoiceGenerating
     10 = VoiceReady
     11 = SubtitleGenerating
     12 = SubtitleReady
     13 = Rendering
     14 = Completed
     15 = Failed

   PublishStatus:
     1 = Draft
     2 = Ready
     3 = Scheduled
     4 = Published
     5 = Failed

   AssetType:
     1 = ScriptText
     2 = SceneImage
     3 = VoiceAudio
     4 = SubtitleSrt
     5 = Thumbnail
     6 = FinalVideo
     7 = TempClip
*/
