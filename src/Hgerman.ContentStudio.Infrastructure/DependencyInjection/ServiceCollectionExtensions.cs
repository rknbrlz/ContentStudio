using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Infrastructure.Data;
using Hgerman.ContentStudio.Infrastructure.Services;
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

        services.AddScoped<IVideoJobService, VideoJobService>();
        services.AddScoped<IAutomationService, AutomationService>();
        services.AddScoped<IAutomationProfileService, AutomationProfileService>();
        services.AddScoped<ITitleOptimizationService, TitleOptimizationService>();
        services.AddScoped<ITrendAnalysisService, TrendAnalysisService>();
        services.AddScoped<ITitleFeedbackService, TitleFeedbackService>();
        services.AddScoped<IAnalyticsFeedbackLoopService, AnalyticsFeedbackLoopService>();

        services.AddScoped<IScriptGenerationService, ScriptGenerationService>();
        services.AddScoped<IPublishService, PublishService>();

        return services;
    }
}