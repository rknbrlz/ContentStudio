using Hgerman.ContentStudio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Infrastructure.Data;

public class ContentStudioDbContext : DbContext
{
    public ContentStudioDbContext(DbContextOptions<ContentStudioDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<VideoJob> VideoJobs => Set<VideoJob>();
    public DbSet<VideoScene> VideoScenes => Set<VideoScene>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<PublishTask> PublishTasks => Set<PublishTask>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    public DbSet<AutomationProfile> AutomationProfiles => Set<AutomationProfile>();
    public DbSet<TrendSnapshot> TrendSnapshots => Set<TrendSnapshot>();
    public DbSet<TitlePerformance> TitlePerformances => Set<TitlePerformance>();
    public DbSet<AutomationFeedback> AutomationFeedbacks => Set<AutomationFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("CS_Project");
            entity.HasKey(x => x.ProjectId);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1000);
        });

        modelBuilder.Entity<VideoJob>(entity =>
        {
            entity.ToTable("CS_VideoJob");
            entity.HasKey(x => x.VideoJobId);

            entity.HasIndex(x => x.JobNo).IsUnique();

            entity.Property(x => x.JobNo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Topic).HasMaxLength(500);
            entity.Property(x => x.SourcePrompt).HasMaxLength(4000);
            entity.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.VoiceProvider).HasMaxLength(50);
            entity.Property(x => x.VoiceName).HasMaxLength(100);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.LockedBy).HasMaxLength(100);
            entity.Property(x => x.MotionMode).HasMaxLength(30);
            entity.Property(x => x.RenderProfile).HasMaxLength(30);

            entity.HasOne(x => x.Project)
                .WithMany(x => x.VideoJobs)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.PrimarySourceAsset)
                .WithMany()
                .HasForeignKey(x => x.PrimarySourceAssetId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<VideoScene>(entity =>
        {
            entity.ToTable("CS_VideoScene");
            entity.HasKey(x => x.VideoSceneId);

            entity.Property(x => x.SceneText).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.ScenePrompt).HasMaxLength(2000);
            entity.Property(x => x.TransitionType).HasMaxLength(50);
            entity.Property(x => x.SourceType).HasMaxLength(30);
            entity.Property(x => x.VisualType).HasMaxLength(30);
            entity.Property(x => x.CameraMotion).HasMaxLength(30);
            entity.Property(x => x.OverlayText).HasMaxLength(120);

            entity.Property(x => x.StartSecond).HasColumnType("decimal(10,2)");
            entity.Property(x => x.EndSecond).HasColumnType("decimal(10,2)");
            entity.Property(x => x.DurationSecond).HasColumnType("decimal(10,2)");
            entity.Property(x => x.CropFocusX).HasColumnType("decimal(10,4)");
            entity.Property(x => x.CropFocusY).HasColumnType("decimal(10,4)");
            entity.Property(x => x.MotionIntensity).HasColumnType("decimal(10,4)");
            entity.Property(x => x.OverlayStartSecond).HasColumnType("decimal(10,2)");
            entity.Property(x => x.OverlayEndSecond).HasColumnType("decimal(10,2)");

            entity.HasOne(x => x.VideoJob)
                .WithMany(x => x.Scenes)
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ImageAsset)
                .WithMany()
                .HasForeignKey(x => x.ImageAssetId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.ToTable("CS_Asset");
            entity.HasKey(x => x.AssetId);

            entity.Property(x => x.ProviderName).HasMaxLength(100);
            entity.Property(x => x.FileName).HasMaxLength(260).IsRequired();
            entity.Property(x => x.BlobPath).HasMaxLength(1000);
            entity.Property(x => x.PublicUrl).HasMaxLength(1000);
            entity.Property(x => x.MimeType).HasMaxLength(100);
            entity.Property(x => x.DurationSec).HasColumnType("decimal(10,2)");

            entity.HasOne(x => x.Project)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.VideoJob)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.VideoScene)
                .WithMany()
                .HasForeignKey(x => x.VideoSceneId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PublishTask>(entity =>
        {
            entity.ToTable("CS_PublishTask");
            entity.HasKey(x => x.PublishTaskId);

            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Tags).HasMaxLength(1000);

            entity.HasOne(x => x.VideoJob)
                .WithMany(x => x.PublishTasks)
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ErrorLog>(entity =>
        {
            entity.ToTable("CS_ErrorLog");
            entity.HasKey(x => x.ErrorLogId);

            entity.Property(x => x.StepName).HasMaxLength(100);
            entity.Property(x => x.ErrorType).HasMaxLength(100);
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000);

            entity.HasOne(x => x.VideoJob)
                .WithMany(x => x.ErrorLogs)
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AutomationProfile>(entity =>
        {
            entity.ToTable("CS_AutomationProfile");
            entity.HasKey(x => x.AutomationProfileId);

            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.PreferredHoursCsv).HasMaxLength(100).IsRequired();
            entity.Property(x => x.TopicPrompt).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.HookTemplate).HasMaxLength(500);
            entity.Property(x => x.ViralPatternTemplate).HasMaxLength(500);
            entity.Property(x => x.TrendKeywordsCsv).HasMaxLength(500);
            entity.Property(x => x.SeedTopicsCsv).HasMaxLength(1000);
            entity.Property(x => x.GrowthMode).HasMaxLength(30).IsRequired();
            entity.Property(x => x.MinSuccessScore).HasColumnType("decimal(10,2)");

            entity.HasOne(x => x.Project)
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrendSnapshot>(entity =>
        {
            entity.ToTable("CS_TrendSnapshot");
            entity.HasKey(x => x.TrendSnapshotId);

            entity.Property(x => x.Keyword).HasMaxLength(150).IsRequired();
            entity.Property(x => x.TrendTitle).HasMaxLength(300).IsRequired();
            entity.Property(x => x.TrendScore).HasColumnType("decimal(10,2)");
            entity.Property(x => x.SourceName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Notes).HasMaxLength(1000);

            entity.HasOne(x => x.AutomationProfile)
                .WithMany(x => x.TrendSnapshots)
                .HasForeignKey(x => x.AutomationProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TitlePerformance>(entity =>
        {
            entity.ToTable("CS_TitlePerformance");
            entity.HasKey(x => x.TitlePerformanceId);

            entity.Property(x => x.OriginalTitle).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CandidateTitle).HasMaxLength(200).IsRequired();
            entity.Property(x => x.HookType).HasMaxLength(100);
            entity.Property(x => x.PatternType).HasMaxLength(100);
            entity.Property(x => x.PredictedScore).HasColumnType("decimal(10,2)");
            entity.Property(x => x.ActualScore).HasColumnType("decimal(10,2)");
            entity.Property(x => x.ClickThroughRate).HasColumnType("decimal(10,4)");
            entity.Property(x => x.AvgWatchSeconds).HasColumnType("decimal(10,2)");
            entity.Property(x => x.RetentionRate).HasColumnType("decimal(10,4)");

            entity.HasOne(x => x.VideoJob)
                .WithMany()
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.AutomationProfile)
                .WithMany(x => x.TitlePerformances)
                .HasForeignKey(x => x.AutomationProfileId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AutomationFeedback>(entity =>
        {
            entity.ToTable("CS_AutomationFeedback");
            entity.HasKey(x => x.AutomationFeedbackId);

            entity.Property(x => x.FeedbackType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Signal).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ScoreValue).HasColumnType("decimal(10,2)");
            entity.Property(x => x.Summary).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.SuggestedAction).HasMaxLength(1000);

            entity.HasOne(x => x.AutomationProfile)
                .WithMany(x => x.FeedbackItems)
                .HasForeignKey(x => x.AutomationProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.VideoJob)
                .WithMany()
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}