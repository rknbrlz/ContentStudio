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

        if (!Directory.Exists(scenesFolderPath))
        {
            throw new DirectoryNotFoundException($"Scenes folder not found: {scenesFolderPath}");
        }

        Directory.CreateDirectory(outputFolderPath);

        var orderedScenes = scenes
            .OrderBy(x => x.SceneNo)
            .ToList();

        var preparedClips = new List<string>();

        foreach (var scene in orderedScenes)
        {
            var clipPath = await EnsureSceneClipAsync(
                scene,
                scenesFolderPath,
                outputFolderPath,
                cancellationToken);

            preparedClips.Add(clipPath);
        }

        if (preparedClips.Count == 0)
        {
            throw new InvalidOperationException("No scene clips were prepared.");
        }

        var concatFilePath = Path.Combine(outputFolderPath, "scenes.txt");
        await File.WriteAllTextAsync(
            concatFilePath,
            BuildConcatFile(preparedClips),
            new UTF8Encoding(false),
            cancellationToken);

        var finalPath = Path.Combine(outputFolderPath, "final.mp4");
        var logFilePath = Path.Combine(outputFolderPath, "ffmpeg-final.log");

        if (File.Exists(finalPath))
        {
            File.Delete(finalPath);
        }

        if (File.Exists(logFilePath))
        {
            File.Delete(logFilePath);
        }

        var ffmpegPath = ResolveFfmpegPath();
        var hasAudio = !string.IsNullOrWhiteSpace(audioFilePath) && File.Exists(audioFilePath);
        var hasSubtitle = !string.IsNullOrWhiteSpace(subtitleFilePath) && File.Exists(subtitleFilePath);

        var args = new StringBuilder();
        args.Append("-y ");
        args.Append("-loglevel info ");
        args.Append("-f concat -safe 0 ");
        args.Append($"-i \"{concatFilePath}\" ");

        if (hasAudio)
        {
            args.Append($"-i \"{audioFilePath}\" ");
        }

        if (hasSubtitle)
        {
            var escapedSubtitle = EscapeSubtitlePathForFilter(subtitleFilePath!);
            args.Append($"-vf \"subtitles='{escapedSubtitle}'\" ");
        }

        args.Append("-r 30 ");
        args.Append("-c:v libx264 ");
        args.Append("-pix_fmt yuv420p ");
        args.Append("-preset medium ");
        args.Append("-crf 20 ");
        args.Append("-movflags +faststart ");

        if (hasAudio)
        {
            args.Append("-map 0:v:0 -map 1:a:0 ");
            args.Append("-c:a aac -b:a 192k ");
            args.Append("-shortest ");
        }
        else
        {
            args.Append("-an ");
        }

        args.Append($"\"{finalPath}\"");

        var result = await RunProcessAsync(
            ffmpegPath,
            args.ToString(),
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

    private async Task<string> EnsureSceneClipAsync(
        VideoScene scene,
        string scenesFolderPath,
        string outputFolderPath,
        CancellationToken cancellationToken)
    {
        var existingRenderedClip = FindExistingSceneVideo(scene.SceneNo, scenesFolderPath);
        if (!string.IsNullOrWhiteSpace(existingRenderedClip))
        {
            return existingRenderedClip;
        }

        var visualPath = FindSceneVisual(scene.SceneNo, scenesFolderPath);
        if (string.IsNullOrWhiteSpace(visualPath))
        {
            var files = Directory.GetFiles(scenesFolderPath)
                .Select(Path.GetFileName)
                .OrderBy(x => x)
                .ToList();

            var fileList = files.Count == 0
                ? "(empty folder)"
                : string.Join(", ", files);

            throw new FileNotFoundException(
                $"Visual not found for scene {scene.SceneNo}. Expected names like scene-001.*, scene_01.*, scene_1.*. Files in folder: {fileList}");
        }

        if (Path.GetExtension(visualPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return visualPath;
        }

        var outputClipPath = Path.Combine(outputFolderPath, $"scene-{scene.SceneNo:D3}.mp4");
        if (File.Exists(outputClipPath))
        {
            return outputClipPath;
        }

        var duration = Convert.ToDouble(
            scene.DurationSecond > 0 ? scene.DurationSecond : 3,
            CultureInfo.InvariantCulture);

        if (duration <= 0)
        {
            duration = 3;
        }

        var ffmpegPath = ResolveFfmpegPath();
        var logFilePath = Path.Combine(outputFolderPath, $"ffmpeg-scene-{scene.SceneNo:D3}.log");

        var vf =
            "scale=1080:1920:force_original_aspect_ratio=increase," +
            "crop=1080:1920," +
            "zoompan=z='min(zoom+0.0015,1.12)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d=1:s=1080x1920:fps=30," +
            "format=yuv420p";

        var args = new StringBuilder();
        args.Append("-y ");
        args.Append("-loop 1 ");
        args.Append($"-t {duration.ToString("0.00", CultureInfo.InvariantCulture)} ");
        args.Append($"-i \"{visualPath}\" ");
        args.Append($"-vf \"{vf}\" ");
        args.Append("-r 30 ");
        args.Append("-c:v libx264 ");
        args.Append("-preset medium ");
        args.Append("-crf 21 ");
        args.Append("-pix_fmt yuv420p ");
        args.Append("-an ");
        args.Append($"\"{outputClipPath}\"");

        var result = await RunProcessAsync(
            ffmpegPath,
            args.ToString(),
            outputFolderPath,
            logFilePath,
            cancellationToken);

        if (result.TimedOut)
        {
            throw new TimeoutException($"Scene clip render timed out for scene {scene.SceneNo}. See log file: {logFilePath}");
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Scene clip render failed for scene {scene.SceneNo}. Exit code: {result.ExitCode}. See log file: {logFilePath}. Error: {result.StandardError}");
        }

        if (!File.Exists(outputClipPath))
        {
            throw new FileNotFoundException($"Rendered scene clip was not created for scene {scene.SceneNo}.", outputClipPath);
        }

        return outputClipPath;
    }

    private static string? FindExistingSceneVideo(int sceneNo, string folderPath)
    {
        foreach (var candidate in BuildSceneCandidates(sceneNo, new[] { ".mp4" }))
        {
            var fullPath = Path.Combine(folderPath, candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static string? FindSceneVisual(int sceneNo, string folderPath)
    {
        foreach (var candidate in BuildSceneCandidates(sceneNo, new[] { ".png", ".jpg", ".jpeg", ".webp", ".mp4" }))
        {
            var fullPath = Path.Combine(folderPath, candidate);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> BuildSceneCandidates(int sceneNo, IEnumerable<string> extensions)
    {
        foreach (var ext in extensions)
        {
            yield return $"scene-{sceneNo:D3}{ext}";
            yield return $"scene_{sceneNo:D3}{ext}";
            yield return $"scene-{sceneNo:D2}{ext}";
            yield return $"scene_{sceneNo:D2}{ext}";
            yield return $"scene-{sceneNo}{ext}";
            yield return $"scene_{sceneNo}{ext}";
        }
    }

    private static string BuildConcatFile(IEnumerable<string> clipPaths)
    {
        var sb = new StringBuilder();

        foreach (var clipPath in clipPaths)
        {
            sb.AppendLine($"file '{EscapeConcatPath(clipPath)}'");
        }

        return sb.ToString();
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