using System.Text.Json;
using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Helpers;

public static class AutomationProfileSnapshotHelper
{
    public static string Build(AutomationProfile profile)
    {
        var snapshot = new
        {
            profile.AutomationProfileId,
            profile.Name,
            profile.IsActive,
            profile.ProjectId,
            profile.LanguageCode,
            profile.PlatformType,
            profile.ToneType,
            profile.DurationTargetSec,
            profile.AspectRatio,
            profile.SubtitleEnabled,
            profile.ThumbnailEnabled,
            profile.DailyVideoLimit,
            profile.PreferredHoursCsv,
            profile.TopicPrompt,
            profile.HookTemplate,
            profile.ViralPatternTemplate,
            profile.AutoPublishYouTube,
            profile.TrendKeywordsCsv,
            profile.SeedTopicsCsv,
            profile.GrowthMode,
            profile.TitleTestVariants,
            profile.MinSuccessScore,
            SnapshotUtc = DateTime.UtcNow
        };

        return JsonSerializer.Serialize(snapshot);
    }
}