using Hgerman.ContentStudio.Domain.Common;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Domain.Entities;

public class Asset : BaseEntity
{
    public int AssetId { get; set; }
    public int? ProjectId { get; set; }
    public int? VideoJobId { get; set; }
    public int? VideoSceneId { get; set; }

    public AssetType AssetType { get; set; }
    public string? ProviderName { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? BlobPath { get; set; }
    public string? PublicUrl { get; set; }
    public string? MimeType { get; set; }
    public long? FileSize { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }

    public decimal? DurationSec { get; set; }
    public int? DurationMs { get; set; }

    public VideoJobStatus Status { get; set; } = VideoJobStatus.Draft;

    public Project? Project { get; set; }
    public VideoJob? VideoJob { get; set; }
    public VideoScene? VideoScene { get; set; }
}