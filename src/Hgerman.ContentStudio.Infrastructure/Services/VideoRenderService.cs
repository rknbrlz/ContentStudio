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
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        if (scenes == null || scenes.Count == 0)
        {
            throw new InvalidOperationException("No scenes found for render.");
        }

        if (!Directory.Exists(scenesFolderPath))
        {
            throw new DirectoryNotFoundException($"Scenes folder not found: {scenesFolderPath}");
        }

        Directory.CreateDirectory(outputFolderPath);

        var finalFileName = $"{job.VideoJobId}.mp4";
        var finalPath = Path.Combine(outputFolderPath, finalFileName);
        var concatFilePath = Path.Combine(outputFolderPath, "scenes.txt");
        var logFilePath = Path.Combine(outputFolderPath, "ffmpeg.log");

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        if (File.Exists(concatFilePath))
        {
            File.Delete(concatFilePath);
        }

        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        var ffmpegPath = ResolveFfmpegPath();

        var orderedScenes = scenes
            .OrderBy(x => x.SceneNo)
            .ToList();

        var imageFiles = orderedScenes
            .Select(scene => Path.Combine(scenesFolderPath, $"scene_{scene.SceneNo:00}.png"))
            .Where(File.Exists)
            .ToList();

        if (imageFiles.Count == 0)
        {
            imageFiles = Directory
                .GetFiles(scenesFolderPath, "*.png")
                .OrderBy(x => x)
                .ToList();
        }

        if (imageFiles.Count == 0)
        {
            throw new FileNotFoundException("No scene image files found in scenes folder.", scenesFolderPath);
        }

        await CreateConcatFileAsync(concatFilePath, orderedScenes, imageFiles, cancellationToken);

        var escapedConcat = EscapePathForFfmpeg(concatFilePath);
        var escapedFinal = EscapePathForFfmpeg(finalPath);

        var hasAudio = !string.IsNullOrWhiteSpace(audioFilePath) && File.Exists(audioFilePath);
        var hasSubtitle = !string.IsNullOrWhiteSpace(subtitleFilePath) && File.Exists(subtitleFilePath);

        var vf = BuildVideoFilter(job, hasSubtitle ? subtitleFilePath : null);

        string arguments;

        if (hasAudio)
        {
            var escapedAudio = EscapePathForFfmpeg(audioFilePath!);

            arguments =
                $"-y " +
                $"-loglevel info " +
                $"-f concat -safe 0 -i \"{escapedConcat}\" " +
                $"-i \"{escapedAudio}\" " +
                $"-vf \"{vf}\" " +
                $"-map 0:v:0 -map 1:a:0 " +
                $"-c:v libx264 -pix_fmt yuv420p -preset veryfast -crf 23 " +
                $"-c:a aac -b:a 192k " +
                $"-shortest " +
                $"\"{escapedFinal}\"";
        }
        else
        {
            arguments =
                $"-y " +
                $"-loglevel info " +
                $"-f concat -safe 0 -i \"{escapedConcat}\" " +
                $"-vf \"{vf}\" " +
                $"-c:v libx264 -pix_fmt yuv420p -preset veryfast -crf 23 " +
                $"-an " +
                $"\"{escapedFinal}\"";
        }

        var result = await RunProcessAsync(
            ffmpegPath,
            arguments,
            outputFolderPath,
            logFilePath,
            cancellationToken);

        if (result.TimedOut)
        {
            throw new TimeoutException($"FFmpeg timed out. See log file: {logFilePath}");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"FFmpeg failed with exit code {result.ExitCode}. See log file: {logFilePath}. Error: {result.StandardError}");
        }

        if (!File.Exists(finalPath))
        {
            throw new FileNotFoundException($"Final video was not created. See log file: {logFilePath}", finalPath);
        }

        var fileInfo = new FileInfo(finalPath);
        if (fileInfo.Length <= 0)
        {
            throw new InvalidOperationException($"Final video file was created but is 0 bytes. See log file: {logFilePath}");
        }

        _logger.LogInformation("Final video created for job {VideoJobId}: {FinalPath}", job.VideoJobId, finalPath);

        return finalPath;
    }

    private async Task CreateConcatFileAsync(
        string concatFilePath,
        IReadOnlyList<VideoScene> orderedScenes,
        IReadOnlyList<string> imageFiles,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < imageFiles.Count; i++)
        {
            var file = imageFiles[i];
            var scene = i < orderedScenes.Count ? orderedScenes[i] : null;

            var duration = scene?.DurationSecond ?? 2m;
            if (duration <= 0)
            {
                duration = 2m;
            }

            builder.AppendLine($"file '{EscapeConcatPath(file)}'");
            builder.AppendLine($"duration {duration.ToString(CultureInfo.InvariantCulture)}");
        }

        if (imageFiles.Count > 0)
        {
            builder.AppendLine($"file '{EscapeConcatPath(imageFiles[^1])}'");
        }

        await File.WriteAllTextAsync(concatFilePath, builder.ToString(), cancellationToken);
    }

    private string BuildVideoFilter(VideoJob job, string? subtitleFilePath)
    {
        var filters = new List<string>();

        if (!string.IsNullOrWhiteSpace(subtitleFilePath) && File.Exists(subtitleFilePath))
        {
            var escapedSubtitle = EscapeSubtitlePath(subtitleFilePath);
            filters.Add($"subtitles='{escapedSubtitle}'");
        }

        return filters.Count == 0
            ? "scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2"
            : $"scale=1080:1920:force_original_aspect_ratio=decrease,pad=1080:1920:(ow-iw)/2:(oh-ih)/2,{string.Join(",", filters)}";
    }

    private string ResolveFfmpegPath()
    {
        if (!string.IsNullOrWhiteSpace(_ffmpegOptions.BinaryPath))
        {
            return _ffmpegOptions.BinaryPath;
        }

        return "ffmpeg";
    }

    private static string EscapePathForFfmpeg(string path)
    {
        return path.Replace("\\", "/");
    }

    private static string EscapeSubtitlePath(string path)
    {
        return path
            .Replace("\\", "/")
            .Replace(":", "\\:")
            .Replace("'", "\\'");
    }

    private static string EscapeConcatPath(string path)
    {
        return path.Replace("\\", "/").Replace("'", "'\\''");
    }

    private async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        string logFilePath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        _logger.LogInformation("Starting FFmpeg: {FileName} {Arguments}", fileName, arguments);

        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var standardOutput = await stdOutTask;
        var standardError = await stdErrTask;

        await File.WriteAllTextAsync(
            logFilePath,
            $"STDOUT:{Environment.NewLine}{standardOutput}{Environment.NewLine}{Environment.NewLine}STDERR:{Environment.NewLine}{standardError}",
            cancellationToken);

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput,
            StandardError = standardError,
            TimedOut = false
        };
    }

    private sealed class ProcessResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; } = string.Empty;
        public string StandardError { get; set; } = string.Empty;
        public bool TimedOut { get; set; }
    }
}