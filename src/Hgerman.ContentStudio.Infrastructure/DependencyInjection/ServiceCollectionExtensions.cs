using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Infrastructure.Services;
using Hgerman.ContentStudio.Shared.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Hgerman.ContentStudio.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentStudioInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HgermanAppsDb");

        services.AddDbContext<ContentStudioDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.Configure<AiProviderOptions>(configuration.GetSection("AiProviders"));
        services.Configure<FfmpegOptions>(configuration.GetSection("Ffmpeg"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<YouTubeOptions>(configuration.GetSection("YouTube"));

        services.AddHttpClient();

        services.AddScoped<OpenAiApiClient>();

        services.AddScoped<IVideoJobService, VideoJobService>();
        services.AddScoped<IJobProcessor, JobProcessor>();

        services.AddScoped<IHookGenerationService, HookGenerationService>();
        services.AddScoped<IScriptGenerationService, ScriptGenerationService>();
        services.AddScoped<IScenePlannerService, ScenePlannerService>();
        services.AddScoped<IImagePromptService, ImagePromptService>();
        services.AddScoped<IImageGenerationService, ImageGenerationService>();
        services.AddScoped<IVoiceGenerationService, VoiceGenerationService>();
        services.AddScoped<ISubtitleService, SubtitleService>();
        services.AddScoped<IVideoRenderService, VideoRenderService>();
        services.AddScoped<IStorageService, LocalStorageService>();

        services.AddScoped<IUploadMetadataService, UploadMetadataService>();
        services.AddScoped<IYouTubeUploadService, YouTubeUploadService>();

        services.AddScoped<IPublishService, PublishService>();

        return services;
    }
}