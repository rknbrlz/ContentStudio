using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class LocalStorageService : IStorageService
{
    private readonly StorageOptions _options;
    private readonly ILogger<LocalStorageService> _logger;

    public LocalStorageService(
        IOptions<StorageOptions> options,
        ILogger<LocalStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> UploadTextAsync(
        string blobPath,
        string content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        return await UploadBytesAsync(blobPath, bytes, contentType, cancellationToken);
    }

    public async Task<string> UploadBytesAsync(
        string blobPath,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var fullPath = BuildFullPath(blobPath);
        var folder = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        await File.WriteAllBytesAsync(fullPath, content, cancellationToken);

        _logger.LogInformation("Saved asset to local storage: {Path}", fullPath);

        return fullPath;
    }

    public async Task<byte[]> ReadBytesAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = BuildFullPath(blobPath);
        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }

    public Task<string> GetLocalPathAsync(
        string blobPath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = BuildFullPath(blobPath);
        return Task.FromResult(fullPath);
    }

    private string BuildFullPath(string blobPath)
    {
        var root = string.IsNullOrWhiteSpace(_options.LocalRootPath)
            ? Path.Combine(AppContext.BaseDirectory, "storage")
            : _options.LocalRootPath;

        var normalized = blobPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        return Path.Combine(root, normalized);
    }
}