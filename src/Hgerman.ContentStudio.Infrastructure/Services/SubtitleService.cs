using System.Globalization;
using System.Text;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class SubtitleService : ISubtitleService
{
    private readonly IStorageService _storageService;

    public SubtitleService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<Asset?> GenerateSubtitleAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        if (!job.SubtitleEnabled || job.Scenes.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        var orderedScenes = job.Scenes.OrderBy(x => x.SceneNo).ToList();

        for (var i = 0; i < orderedScenes.Count; i++)
        {
            var scene = orderedScenes[i];
            builder.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture));
            builder.AppendLine($"{ToSrtTime(scene.StartSecond)} --> {ToSrtTime(scene.EndSecond)}");
            builder.AppendLine(scene.SceneText.Trim());
            builder.AppendLine();
        }

        var content = builder.ToString();
        var blobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/subtitles/subtitles.srt";
        var publicUrl = await _storageService.UploadTextAsync(blobPath, content, "application/x-subrip", cancellationToken);

        return new Asset
        {
            VideoJobId = job.VideoJobId,
            AssetType = AssetType.SubtitleSrt,
            ProviderName = "InternalSubtitleBuilder",
            FileName = "subtitles.srt",
            BlobPath = blobPath,
            PublicUrl = publicUrl,
            MimeType = "application/x-subrip",
            FileSize = Encoding.UTF8.GetByteCount(content),
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow
        };
    }

    private static string ToSrtTime(decimal seconds)
    {
        var wholeMilliseconds = (int)Math.Round(seconds * 1000m, MidpointRounding.AwayFromZero);
        var ts = TimeSpan.FromMilliseconds(wholeMilliseconds);
        return $"{ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00},{ts.Milliseconds:000}";
    }
}
