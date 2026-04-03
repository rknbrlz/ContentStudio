using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IUploadMetadataService
{
    Task<UploadMetadataResult> GenerateYouTubeMetadataAsync(
        VideoJob job,
        CancellationToken cancellationToken = default);
}

public sealed class UploadMetadataResult
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}