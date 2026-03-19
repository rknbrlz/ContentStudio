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
    public static IServiceCollection AddContentStudioInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiProviderOptions>(configuration.GetSection("AiProviders"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<FfmpegOptions>(configuration.GetSection("Ffmpeg"));

        services.AddDbContext<ContentStudioDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("HgermanAppsDb")));

        services.AddHttpClient();
        services.AddTransient<OpenAiApiClient>();

        services.AddScoped<IVideoJobService, VideoJobService>();
        services.AddScoped<IScriptGenerationService, ScriptGenerationService>();
        services.AddScoped<IScenePlannerService, ScenePlannerService>();
        services.AddScoped<IImagePromptService, ImagePromptService>();
        services.AddScoped<IImageGenerationService, ImageGenerationService>();
        services.AddScoped<IVoiceGenerationService, VoiceGenerationService>();
        services.AddScoped<ISubtitleService, SubtitleService>();
        services.AddScoped<IVideoRenderService, VideoRenderService>();
        services.AddScoped<IPublishService, PublishService>();
        services.AddScoped<IStorageService, BlobStorageService>();
        services.AddScoped<IJobProcessor, JobProcessor>();

        return services;
    }
}
