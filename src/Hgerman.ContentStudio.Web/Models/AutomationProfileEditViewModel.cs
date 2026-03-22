using System.ComponentModel.DataAnnotations;

namespace Hgerman.ContentStudio.Web.Models;

public class AutomationProfileEditViewModel
{
    public int? AutomationProfileId { get; set; }

    [Display(Name = "Project Id")]
    public int ProjectId { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Active")]
    public bool IsActive { get; set; }

    [Display(Name = "Language Code")]
    [StringLength(10)]
    public string LanguageCode { get; set; } = "en";

    [Display(Name = "Platform Type")]
    public int PlatformType { get; set; }

    [Display(Name = "Tone Type")]
    public int ToneType { get; set; }

    [Display(Name = "Duration Target (sec)")]
    public int DurationTargetSec { get; set; }

    [Display(Name = "Aspect Ratio")]
    public int AspectRatio { get; set; }

    [Display(Name = "Subtitle Enabled")]
    public bool SubtitleEnabled { get; set; }

    [Display(Name = "Thumbnail Enabled")]
    public bool ThumbnailEnabled { get; set; }

    [Display(Name = "Daily Video Limit")]
    public int DailyVideoLimit { get; set; }

    [Display(Name = "Preferred Hours CSV")]
    public string? PreferredHoursCsv { get; set; }

    [Display(Name = "Topic Prompt")]
    public string? TopicPrompt { get; set; }

    [Display(Name = "Hook Template")]
    public string? HookTemplate { get; set; }

    [Display(Name = "Viral Pattern Template")]
    public string? ViralPatternTemplate { get; set; }

    [Display(Name = "Auto Publish YouTube")]
    public bool AutoPublishYouTube { get; set; }

    [Display(Name = "Trend Keywords CSV")]
    public string? TrendKeywordsCsv { get; set; }

    [Display(Name = "Seed Topics CSV")]
    public string? SeedTopicsCsv { get; set; }

    [Display(Name = "Growth Mode")]
    public string? GrowthMode { get; set; }

    [Display(Name = "Title Test Variants")]
    public int TitleTestVariants { get; set; }

    [Display(Name = "Min Success Score")]
    public decimal MinSuccessScore { get; set; }
}