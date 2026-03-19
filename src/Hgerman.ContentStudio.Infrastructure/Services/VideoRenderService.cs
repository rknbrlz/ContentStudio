using System.Diagnostics;
using System.Globalization;
using System.Text;
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
        if (scenes == null || scenes.Count == 0)
        {
            throw new InvalidOperationException("No scenes found for rendering.");
        }

        Directory.CreateDirectory(outputFolderPath);
        Directory.CreateDirectory(_ffmpegOptions.WorkingDirectory);

        var orderedScenes = scenes
            .OrderBy(x => x.SceneNo)
            .ToList();

        var concatFilePath = Path.Combine(_ffmpegOptions.WorkingDirectory, $"concat_{job.VideoJobId}.txt");
        var tempVideoPath = Path.Combine(_ffmpegOptions.WorkingDirectory, $"temp_video_{job.VideoJobId}.mp4");
        var tempVideoWithAudioPath = Path.Combine(_ffmpegOptions.WorkingDirectory, $"temp_video_audio_{job.VideoJobId}.mp4");
        var finalVideoPath = Path.Combine(outputFolderPath, "final.mp4");

        try
        {
            await CreateConcatFileAsync(orderedScenes, scenesFolderPath, concatFilePath, cancellationToken);

            await RenderSceneSlideshowAsync(concatFilePath, tempVideoPath, cancellationToken);

            string inputForFinalStep = tempVideoPath;

            if (!string.IsNullOrWhiteSpace(audioFilePath) && File.Exists(audioFilePath))
            {
                await AddAudioAsync(tempVideoPath, audioFilePath, tempVideoWithAudioPath, cancellationToken);
                inputForFinalStep = tempVideoWithAudioPath;
            }

            if (!string.IsNullOrWhiteSpace(subtitleFilePath) && File.Exists(subtitleFilePath))
            {
                try
                {
                    await BurnSubtitlesAsync(inputForFinalStep, subtitleFilePath, finalVideoPath, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Subtitle burn-in failed for VideoJobId {VideoJobId}. Falling back to video without subtitles.",
                        job.VideoJobId);

                    File.Copy(inputForFinalStep, finalVideoPath, overwrite: true);
                }
            }
            else
            {
                File.Copy(inputForFinalStep, finalVideoPath, overwrite: true);
            }

            return finalVideoPath;
        }
        finally
        {
            SafeDelete(concatFilePath);
            SafeDelete(tempVideoPath);
            SafeDelete(tempVideoWithAudioPath);
        }
    }

    private async Task CreateConcatFileAsync(
        IReadOnlyCollection<VideoScene> scenes,
        string scenesFolderPath,
        string concatFilePath,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        foreach (var scene in scenes.OrderBy(x => x.SceneNo))
        {
            var imagePath = FindSceneImagePath(scenesFolderPath, scene.SceneNo);
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Scene image not found for SceneNo={scene.SceneNo} in folder: {scenesFolderPath}");
            }

            var escapedPath = imagePath.Replace("\\", "/").Replace("'", "'\\''");
            var duration = Math.Max(1.0m, scene.DurationSecond);

            sb.AppendLine($"file '{escapedPath}'");
            sb.AppendLine($"duration {duration.ToString(CultureInfo.InvariantCulture)}");
        }

        var lastScene = scenes.OrderBy(x => x.SceneNo).Last();
        var lastImagePath = FindSceneImagePath(scenesFolderPath, lastScene.SceneNo);

        if (string.IsNullOrWhiteSpace(lastImagePath) || !File.Exists(lastImagePath))
        {
            throw new FileNotFoundException($"Last scene image not found for SceneNo={lastScene.SceneNo} in folder: {scenesFolderPath}");
        }

        var lastEscapedPath = lastImagePath.Replace("\\", "/").Replace("'", "'\\''");
        sb.AppendLine($"file '{lastEscapedPath}'");

        await File.WriteAllTextAsync(concatFilePath, sb.ToString(), cancellationToken);
    }

    private static string? FindSceneImagePath(string scenesFolderPath, int sceneNo)
    {
        var candidates = new[]
        {
        Path.Combine(scenesFolderPath, $"scene-{sceneNo:00}.png"),
        Path.Combine(scenesFolderPath, $"scene-{sceneNo:00}.jpg"),
        Path.Combine(scenesFolderPath, $"scene-{sceneNo:00}.jpeg"),
        Path.Combine(scenesFolderPath, $"scene_{sceneNo}.png"),
        Path.Combine(scenesFolderPath, $"scene_{sceneNo}.jpg"),
        Path.Combine(scenesFolderPath, $"scene_{sceneNo}.jpeg"),
        Path.Combine(scenesFolderPath, $"scene_{sceneNo}.JPG"),
        Path.Combine(scenesFolderPath, $"scene_{sceneNo}.JPEG")
    };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private async Task RenderSceneSlideshowAsync(
        string concatFilePath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var args =
            $"-y -f concat -safe 0 -i \"{concatFilePath}\" " +
            $"-vf \"scale={_ffmpegOptions.Width}:{_ffmpegOptions.Height}:force_original_aspect_ratio=decrease," +
            $"pad={_ffmpegOptions.Width}:{_ffmpegOptions.Height}:(ow-iw)/2:(oh-ih)/2,format=yuv420p\" " +
            $"-r {_ffmpegOptions.Fps} " +
            $"-c:v libx264 -pix_fmt yuv420p " +
            $"\"{outputPath}\"";

        await RunFfmpegAsync(args, cancellationToken);
    }

    private async Task AddAudioAsync(
        string videoPath,
        string audioPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var args =
            $"-y -i \"{videoPath}\" -i \"{audioPath}\" " +
            "-map 0:v:0 -map 1:a:0 " +
            "-c:v copy -c:a aac -b:a 192k -shortest " +
            $"\"{outputPath}\"";

        await RunFfmpegAsync(args, cancellationToken);
    }

    private async Task BurnSubtitlesAsync(
        string inputVideoPath,
        string subtitleFilePath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var escapedSubtitlePath = subtitleFilePath
            .Replace("\\", "\\\\")
            .Replace(":", "\\:")
            .Replace(",", "\\,");

        var args =
            $"-y -i \"{inputVideoPath}\" " +
            $"-vf \"subtitles='{escapedSubtitlePath}'\" " +
            "-c:v libx264 -preset medium -crf 20 " +
            "-c:a copy " +
            $"\"{outputPath}\"";

        await RunFfmpegAsync(args, cancellationToken);
    }

    private async Task RunFfmpegAsync(string arguments, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_ffmpegOptions.ExecutablePath) || !File.Exists(_ffmpegOptions.ExecutablePath))
        {
            throw new FileNotFoundException($"FFmpeg executable not found: {_ffmpegOptions.ExecutablePath}");
        }

        Directory.CreateDirectory(_ffmpegOptions.WorkingDirectory);

        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegOptions.ExecutablePath,
            Arguments = arguments,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _ffmpegOptions.WorkingDirectory
        };

        using var process = new Process { StartInfo = psi };

        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                stdOut.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                stdErr.AppendLine(e.Data);
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("FFmpeg process could not be started.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            var message =
                $"FFmpeg failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                $"Arguments: {arguments}{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{stdOut}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{stdErr}";

            _logger.LogError(message);
            throw new InvalidOperationException(message);
        }
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}