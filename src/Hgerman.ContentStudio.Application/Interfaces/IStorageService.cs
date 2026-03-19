namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IStorageService
{
    Task<string?> UploadTextAsync(
        string blobPath,
        string content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<string?> UploadBytesAsync(
        string blobPath,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<byte[]> ReadBytesAsync(
        string blobPath,
        CancellationToken cancellationToken = default);

    Task<string> GetLocalPathAsync(
        string blobPath,
        CancellationToken cancellationToken = default);
}