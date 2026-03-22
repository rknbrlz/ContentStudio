using System.ComponentModel.DataAnnotations;
using Hgerman.ContentStudio.Shared.DTOs;

namespace Hgerman.ContentStudio.Web.Models;

public class AutomationProfileEditViewModel
{
    public int? AutomationProfileId { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    [Required]
    public int ProjectId { get; set; }

    [Required, StringLength(10)]
    public string LanguageCode { get; set; } = "en";

    [Required]
    public int PlatformType { get; set; }

    [Required]
    public int ToneType { get; set; }

    [Range(15, 120)]
    public int DurationTargetSec { get; set; } = 45;

    [Required]
    public int AspectRatio { get; set; }

    public bool SubtitleEnabled { get; set; } = true;
    public bool ThumbnailEnabled { get; set; } = true;

    [Range(1, 20)]
    public int DailyVideoLimit { get; set; } = 3;

    [Required, StringLength(100)]
    public string PreferredHoursCsv { get; set; } = "09,14,20";

    [Required, StringLength(1000)]
    public string TopicPrompt { get; set; } = string.Empty;

    [StringLength(500)]
    public string? HookTemplate { get; set; }

    [StringLength(500)]
    public string? ViralPatternTemplate { get; set; }

    public bool AutoPublishYouTube { get; set; }

    [StringLength(500)]
    public string? TrendKeywordsCsv { get; set; }

    [StringLength(1000)]
    public string? SeedTopicsCsv { get; set; }

    [Required, StringLength(30)]
    public string GrowthMode { get; set; } = "balanced";

    [Range(1, 10)]
    public int TitleTestVariants { get; set; } = 3;

    [Range(0, 100)]
    public decimal MinSuccessScore { get; set; } = 55;

    public UpsertAutomationProfileRequest ToRequest()
    {
        return new UpsertAutomationProfileRequest
        {
            Name = Name,
            IsActive = IsActive,
            ProjectId = ProjectId,
            LanguageCode = LanguageCode,
            PlatformType = PlatformType,
            ToneType = ToneType,
            DurationTargetSec = DurationTargetSec,
            AspectRatio = AspectRatio,
            SubtitleEnabled = SubtitleEnabled,
            ThumbnailEnabled = ThumbnailEnabled,
            DailyVideoLimit = DailyVideoLimit,
            PreferredHoursCsv = PreferredHoursCsv,
            TopicPrompt = TopicPrompt,
            HookTemplate = HookTemplate,
            ViralPatternTemplate = ViralPatternTemplate,
            AutoPublishYouTube = AutoPublishYouTube,
            TrendKeywordsCsv = TrendKeywordsCsv,
            SeedTopicsCsv = SeedTopicsCsv,
            GrowthMode = GrowthMode,
            TitleTestVariants = TitleTestVariants,
            MinSuccessScore = MinSuccessScore
        };
    }

    public static AutomationProfileEditViewModel FromEntity(Hgerman.ContentStudio.Domain.Entities.AutomationProfile entity)
    {
        return new AutomationProfileEditViewModel
        {
            AutomationProfileId = entity.AutomationProfileId,
            Name = entity.Name,
            IsActive = entity.IsActive,
            ProjectId = entity.ProjectId,
            LanguageCode = entity.LanguageCode,
            PlatformType = entity.PlatformType,
            ToneType = entity.ToneType,
            DurationTargetSec = entity.DurationTargetSec,
            AspectRatio = entity.AspectRatio,
            SubtitleEnabled = entity.SubtitleEnabled,
            ThumbnailEnabled = entity.ThumbnailEnabled,
            DailyVideoLimit = entity.DailyVideoLimit,
            PreferredHoursCsv = entity.PreferredHoursCsv,
            TopicPrompt = entity.TopicPrompt,
            HookTemplate = entity.HookTemplate,
            ViralPatternTemplate = entity.ViralPatternTemplate,
            AutoPublishYouTube = entity.AutoPublishYouTube,
            TrendKeywordsCsv = entity.TrendKeywordsCsv,
            SeedTopicsCsv = entity.SeedTopicsCsv,
            GrowthMode = entity.GrowthMode,
            TitleTestVariants = entity.TitleTestVariants,
            MinSuccessScore = entity.MinSuccessScore
        };
    }
}