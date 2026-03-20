using System.Diagnostics;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class VideoRenderService : IVideoRenderService
{
    private readonly FfmpegOptions _ffmpegOptions;
    private readonly ILogger<VideoRenderService> _logger;

    public VideoRenderService(
        IOptions<FfmpegOptions> ffmpegOptions,
        ILogger<VideoRenderService> logger)
    {
        _ffmpegOptions = ffmpegOptions.Value;
        _logger = logger;
    }

    public async Task<string> RenderFinalVideoAsync(
        VideoJob job,
        IReadOnlyCollection<VideoScene> scenes,
        string scenesFolderPath,
        string? audioFilePath,
        string? subtitleFilePath,
        string outputFolderPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputFolderPath);

        var finalPath = Path.Combine(outputFolderPath, "final.mp4");

        if (File.Exists(finalPath))
            return finalPath;

        await File.WriteAllBytesAsync(finalPath, Array.Empty<byte>(), cancellationToken);

        return finalPath;
    }
}