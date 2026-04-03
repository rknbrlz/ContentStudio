using System.Text;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class BlobStorageService : IStorageService
{
    private readonly StorageOptions _options;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(
        IOptions<StorageOptions> options,
        ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> UploadTextAsync(
        string blobPath,
        string content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return await UploadBytesAsync(blobPath, bytes, contentType, cancellationToken);
    }

    public async Task<string?> UploadBytesAsync(
        string blobPath,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new ArgumentException("blobPath cannot be null or empty.", nameof(blobPath));
        }

        if (content == null || content.Length == 0)
        {
            throw new InvalidOperationException($"Storage upload refused empty content for {blobPath}");
        }

        var localPath = MapToLocalPath(blobPath);
        var directory = Path.GetDirectoryName(localPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(localPath, content, cancellationToken);

        _logger.LogInformation(
            "STORAGE_WRITE Path={Path}, Bytes={Bytes}, ContentType={ContentType}",
            localPath,
            content.Length,
            contentType);

        return localPath;
    }

    public async Task<byte[]> ReadBytesAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new ArgumentException("blobPath cannot be null or empty.", nameof(blobPath));
        }

        var localPath = MapToLocalPath(blobPath);

        if (!File.Exists(localPath))
        {
            throw new FileNotFoundException($"Storage file not found: {localPath}", localPath);
        }

        return await File.ReadAllBytesAsync(localPath, cancellationToken);
    }

    public Task<string> GetLocalPathAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            throw new ArgumentException("blobPath cannot be null or empty.", nameof(blobPath));
        }

        var localPath = MapToLocalPath(blobPath);
        return Task.FromResult(localPath);
    }

    private string MapToLocalPath(string blobPath)
    {
        var normalized = blobPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        return Path.Combine(_options.LocalRootPath, normalized);
    }
}