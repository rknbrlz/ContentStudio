using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Shared.DTOs;

public class CreateVideoJobRequest
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? SourcePrompt { get; set; }
    public string LanguageCode { get; set; } = "en";
    public PlatformType PlatformType { get; set; } = PlatformType.YouTubeShorts;
    public ToneType ToneType { get; set; } = ToneType.Inspirational;
    public int DurationTargetSec { get; set; } = 45;
    public AspectRatioType AspectRatio { get; set; } = AspectRatioType.Vertical916;
    public string? VoiceProvider { get; set; }
    public string? VoiceName { get; set; }
    public bool SubtitleEnabled { get; set; } = true;
    public bool ThumbnailEnabled { get; set; } = true;
    public InputModeType InputMode { get; set; } = InputModeType.AiOnly;
}