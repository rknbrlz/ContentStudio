using Hgerman.ContentStudio.Domain.Common;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Domain.Entities;

public class VideoJob : BaseEntity
{
    public int VideoJobId { get; set; }
    public int ProjectId { get; set; }
    public string JobNo { get; set; } = string.Empty;
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
    public int? PrimarySourceAssetId { get; set; }

    public VideoJobStatus Status { get; set; } = VideoJobStatus.Draft;
    public VideoPipelineStep CurrentStep { get; set; } = VideoPipelineStep.Draft;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? CompletedDate { get; set; }

    public Project? Project { get; set; }
    public Asset? PrimarySourceAsset { get; set; }

    public ICollection<VideoScene> Scenes { get; set; } = new List<VideoScene>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<PublishTask> PublishTasks { get; set; } = new List<PublishTask>();
    public ICollection<ErrorLog> ErrorLogs { get; set; } = new List<ErrorLog>();
}