namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IYouTubeUploadService
{
    Task<string> UploadVideoAsync(
        string filePath,
        string title,
        string description,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken = default);
}