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
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();
    public DbSet<PublishTask> PublishTasks => Set<PublishTask>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("CS_Project");
            entity.HasKey(x => x.ProjectId);
            entity.Property(x => x.ProjectName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<VideoJob>(entity =>
        {
            entity.ToTable("CS_VideoJob");
            entity.HasKey(x => x.VideoJobId);
            entity.HasIndex(x => x.JobNo).IsUnique();

            entity.Property(x => x.JobNo).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Topic).HasMaxLength(500);
            entity.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
            entity.Property(x => x.VoiceProvider).HasMaxLength(50);
            entity.Property(x => x.VoiceName).HasMaxLength(100);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);

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
            entity.Property(x => x.StartSecond).HasColumnType("decimal(10,2)");
            entity.Property(x => x.EndSecond).HasColumnType("decimal(10,2)");
            entity.Property(x => x.DurationSecond).HasColumnType("decimal(10,2)");
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
            entity.Property(x => x.BlobPath).HasMaxLength(500).IsRequired();
            entity.Property(x => x.PublicUrl).HasMaxLength(1000);
            entity.Property(x => x.MimeType).HasMaxLength(100);
            entity.HasOne(x => x.VideoJob)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.VideoScene)
                .WithMany()
                .HasForeignKey(x => x.VideoSceneId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PromptTemplate>(entity =>
        {
            entity.ToTable("CS_PromptTemplate");
            entity.HasKey(x => x.PromptTemplateId);
            entity.Property(x => x.TemplateName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.TemplateType).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
        });

        modelBuilder.Entity<PublishTask>(entity =>
        {
            entity.ToTable("CS_PublishTask");
            entity.HasKey(x => x.PublishTaskId);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Tags).HasMaxLength(1000);
            entity.Property(x => x.PlatformVideoId).HasMaxLength(200);
            entity.Property(x => x.PublishUrl).HasMaxLength(1000);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.HasOne(x => x.VideoJob)
                .WithMany(x => x.PublishTasks)
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ThumbnailAsset)
                .WithMany()
                .HasForeignKey(x => x.ThumbnailAssetId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("CS_AppSetting");
            entity.HasKey(x => x.AppSettingId);
            entity.Property(x => x.SettingKey).HasMaxLength(150).IsRequired();
            entity.Property(x => x.SettingGroup).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.SettingKey).IsUnique();
        });

        modelBuilder.Entity<ErrorLog>(entity =>
        {
            entity.ToTable("CS_ErrorLog");
            entity.HasKey(x => x.ErrorLogId);
            entity.Property(x => x.StepName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.ErrorType).HasMaxLength(100);
            entity.Property(x => x.ErrorMessage).HasMaxLength(4000).IsRequired();
            entity.HasOne(x => x.VideoJob)
                .WithMany(x => x.ErrorLogs)
                .HasForeignKey(x => x.VideoJobId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
