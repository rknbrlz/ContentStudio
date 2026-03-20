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
            throw new InvalidOperationException("No scenes found for render.");
        }

        if (!Directory.Exists(scenesFolderPath))
        {
            throw new DirectoryNotFoundException($"Scenes folder not found: {scenesFolderPath}");
        }

        Directory.CreateDirectory(outputFolderPath);

        var finalPath = Path.Combine(outputFolderPath, "final.mp4");
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
        var orderedScenes = scenes.OrderBy(x => x.SceneNo).ToList();

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

        return finalPath;
    }

    private async Task CreateConcatFileAsync(
        string concatFilePath,
        IReadOnlyList<VideoScene> scenes,
        IReadOnlyList<string> imageFiles,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < imageFiles.Count; i++)
        {
            var imagePath = imageFiles[i];
            var scene = i < scenes.Count ? scenes[i] : scenes.Last();

            var duration = Convert.ToDouble(
                scene.DurationSecond > 0 ? scene.DurationSecond : 3,
                CultureInfo.InvariantCulture);

            if (duration <= 0)
            {
                duration = 3;
            }

            sb.AppendLine($"file '{EscapeConcatPath(imagePath)}'");
            sb.AppendLine($"duration {duration.ToString(CultureInfo.InvariantCulture)}");
        }

        sb.AppendLine($"file '{EscapeConcatPath(imageFiles.Last())}'");

        await File.WriteAllTextAsync(
            concatFilePath,
            sb.ToString(),
            new UTF8Encoding(false),
            cancellationToken);
    }

    private string BuildVideoFilter(VideoJob job, string? subtitleFilePath)
    {
        var aspectRatioText = job.AspectRatio.ToString();
        var platformTypeText = job.PlatformType.ToString();

        var isVertical =
            aspectRatioText.Contains("9:16", StringComparison.OrdinalIgnoreCase) ||
            aspectRatioText.Contains("Vertical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(platformTypeText, "YouTubeShorts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(platformTypeText, "TikTok", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(platformTypeText, "InstagramReels", StringComparison.OrdinalIgnoreCase);

        var targetSize = isVertical ? "1080:1920" : "1920:1080";

        var baseFilter =
            $"scale={targetSize}:force_original_aspect_ratio=decrease," +
            $"pad={targetSize}:(ow-iw)/2:(oh-ih)/2," +
            $"format=yuv420p";

        if (!string.IsNullOrWhiteSpace(subtitleFilePath) && File.Exists(subtitleFilePath))
        {
            var escapedSubtitle = EscapeSubtitlePathForFilter(subtitleFilePath);

            return
                $"{baseFilter}," +
                $"subtitles='{escapedSubtitle}:force_style=FontName=Arial,FontSize=24,PrimaryColour=&HFFFFFF&,OutlineColour=&H000000&,BorderStyle=3,Outline=2,Shadow=1,Alignment=2,MarginV=110'";
        }

        return baseFilter;
    }

    private string ResolveFfmpegPath()
    {
        if (!string.IsNullOrWhiteSpace(_ffmpegOptions.BinaryPath))
        {
            if (File.Exists(_ffmpegOptions.BinaryPath))
            {
                return _ffmpegOptions.BinaryPath;
            }

            throw new FileNotFoundException("FFmpeg binary path not found.", _ffmpegOptions.BinaryPath);
        }

        return "ffmpeg";
    }

    private static string EscapeConcatPath(string path)
    {
        return path.Replace("\\", "/").Replace("'", "'\\''");
    }

    private static string EscapePathForFfmpeg(string path)
    {
        return path.Replace("\\", "/");
    }

    private static string EscapeSubtitlePathForFilter(string path)
    {
        return path
            .Replace("\\", "/")
            .Replace(":", "\\:")
            .Replace("'", "\\'");
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        string logFilePath,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.Start();

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(3), cancellationToken);
        var waitForExitTask = process.WaitForExitAsync(cancellationToken);

        var completedTask = await Task.WhenAny(waitForExitTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
            }

            var timedOutStdOut = await stdOutTask;
            var timedOutStdErr = await stdErrTask;

            var timedOutLog =
                $"TIMED OUT{Environment.NewLine}" +
                $"FILE: {fileName}{Environment.NewLine}" +
                $"ARGS: {arguments}{Environment.NewLine}{Environment.NewLine}" +
                $"STDOUT:{Environment.NewLine}{timedOutStdOut}{Environment.NewLine}{Environment.NewLine}" +
                $"STDERR:{Environment.NewLine}{timedOutStdErr}";

            await File.WriteAllTextAsync(logFilePath, timedOutLog, cancellationToken);

            return new ProcessResult(-999, timedOutStdOut, timedOutStdErr, true);
        }

        await waitForExitTask;

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        var fullLog =
            $"EXIT CODE: {process.ExitCode}{Environment.NewLine}" +
            $"FILE: {fileName}{Environment.NewLine}" +
            $"ARGS: {arguments}{Environment.NewLine}{Environment.NewLine}" +
            $"STDOUT:{Environment.NewLine}{stdOut}{Environment.NewLine}{Environment.NewLine}" +
            $"STDERR:{Environment.NewLine}{stdErr}";

        await File.WriteAllTextAsync(logFilePath, fullLog, cancellationToken);

        return new ProcessResult(process.ExitCode, stdOut, stdErr, false);
    }

    private sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut);
}