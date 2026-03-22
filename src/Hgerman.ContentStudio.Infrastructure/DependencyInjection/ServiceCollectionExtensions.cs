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

        services.Configure<StorageOptions>(
            configuration.GetSection("Storage"));

        services.Configure<AiProviderOptions>(
            configuration.GetSection("AiProviders"));

        services.AddHttpClient<OpenAiApiClient>();

        services.AddScoped<IJobProcessor, JobProcessor>();
        services.AddScoped<IVideoJobService, VideoJobService>();

        services.AddScoped<IAutomationService, AutomationService>();
        services.AddScoped<IAutomationProfileService, AutomationProfileService>();

        services.AddScoped<ITitleOptimizationService, TitleOptimizationService>();
        services.AddScoped<ITrendAnalysisService, TrendAnalysisService>();
        services.AddScoped<ITitleFeedbackService, TitleFeedbackService>();
        services.AddScoped<IAnalyticsFeedbackLoopService, AnalyticsFeedbackLoopService>();

        services.AddScoped<IScriptGenerationService, ScriptGenerationService>();
        services.AddScoped<IHookGenerationService, HookGenerationService>();
        services.AddScoped<IScenePlannerService, ScenePlannerService>();
        services.AddScoped<IImagePromptService, ImagePromptService>();
        services.AddScoped<IImageGenerationService, ImageGenerationService>();
        services.AddScoped<IVoiceGenerationService, VoiceGenerationService>();
        services.AddScoped<ISubtitleService, SubtitleService>();
        services.AddScoped<IVideoRenderService, VideoRenderService>();

        services.AddScoped<IPublishService, PublishService>();
        services.AddScoped<IUploadMetadataService, UploadMetadataService>();
        services.AddScoped<IYouTubeUploadService, YouTubeUploadService>();
        services.AddScoped<IStorageService, LocalStorageService>();

        return services;
    }
}