using System.Diagnostics;
using System.Globalization;
using System.Text;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
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

        if (string.IsNullOrWhiteSpace(scenesFolderPath) || !Directory.Exists(scenesFolderPath))
        {
            throw new DirectoryNotFoundException($"Scenes folder not found: {scenesFolderPath}");
        }

        Directory.CreateDirectory(outputFolderPath);

        var orderedScenes = scenes
            .OrderBy(x => x.SceneNo)
            .ToList();

        var ffmpegExe = ResolveFfmpegExecutablePath();
        var outputFilePath = Path.Combine(outputFolderPath, "final-video.mp4");
        var tempConcatFilePath = Path.Combine(outputFolderPath, "scene-inputs.txt");

        _logger.LogInformation("=== VIDEO RENDER PRECHECK START ===");
        _logger.LogInformation("VideoJobId: {VideoJobId}", job.VideoJobId);
        _logger.LogInformation("FFmpeg exe: {FfmpegExe}", ffmpegExe);
        _logger.LogInformation("Scenes folder path: {ScenesFolderPath}", scenesFolderPath);
        _logger.LogInformation("Scenes folder exists: {Exists}", Directory.Exists(scenesFolderPath));
        _logger.LogInformation("Audio file path: {AudioFilePath}", audioFilePath ?? "(null)");
        _logger.LogInformation("Audio file exists: {Exists}", !string.IsNullOrWhiteSpace(audioFilePath) && File.Exists(audioFilePath));
        _logger.LogInformation("Subtitle file path: {SubtitleFilePath}", subtitleFilePath ?? "(null)");
        _logger.LogInformation("Subtitle file exists: {Exists}", !string.IsNullOrWhiteSpace(subtitleFilePath) && File.Exists(subtitleFilePath));
        _logger.LogInformation("Output folder path: {OutputFolderPath}", outputFolderPath);
        _logger.LogInformation("Output file path: {OutputFilePath}", outputFilePath);

        foreach (var scene in orderedScenes)
        {
            var sceneImagePath = ResolveSceneImagePath(scene, scenesFolderPath);
            _logger.LogInformation(
                "Scene #{SceneNo} | Duration={Duration} | ImagePath={ImagePath} | Exists={Exists}",
                scene.SceneNo,
                scene.DurationSecond,
                sceneImagePath,
                File.Exists(sceneImagePath));
        }

        _logger.LogInformation("=== VIDEO RENDER PRECHECK END ===");

        CreateConcatFile(tempConcatFilePath, orderedScenes, scenesFolderPath);

        var filterComplex = BuildVideoFilter(job, subtitleFilePath);
        var arguments = BuildFfmpegArguments(
            tempConcatFilePath,
            audioFilePath,
            outputFilePath,
            filterComplex);

        _logger.LogInformation(
            "Starting final video render for VideoJobId {VideoJobId}. Output: {OutputFilePath}",
            job.VideoJobId,
            outputFilePath);

        _logger.LogInformation("FFmpeg filter: {FilterComplex}", filterComplex);
        _logger.LogInformation("FFmpeg arguments: {Arguments}", arguments);

        await RunProcessAsync(
            ffmpegExe,
            arguments,
            outputFolderPath,
            cancellationToken);

        if (!File.Exists(outputFilePath))
        {
            throw new FileNotFoundException("Final video was not created.", outputFilePath);
        }

        _logger.LogInformation(
            "Final video render completed for VideoJobId {VideoJobId}. Output: {OutputFilePath}",
            job.VideoJobId,
            outputFilePath);

        return outputFilePath;
    }

    private void CreateConcatFile(
        string concatFilePath,
        IReadOnlyList<VideoScene> scenes,
        string scenesFolderPath)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < scenes.Count; i++)
        {
            var scene = scenes[i];
            var imagePath = ResolveSceneImagePath(scene, scenesFolderPath);

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException(
                    $"Scene image file not found for scene #{scene.SceneNo}.",
                    imagePath);
            }

            var safePath = EscapeConcatFilePath(imagePath);
            var duration = Math.Max(0.2m, scene.DurationSecond);

            sb.AppendLine($"file '{safePath}'");
            sb.AppendLine($"duration {duration.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        var lastScene = scenes.Last();
        var lastImagePath = ResolveSceneImagePath(lastScene, scenesFolderPath);
        sb.AppendLine($"file '{EscapeConcatFilePath(lastImagePath)}'");

        File.WriteAllText(concatFilePath, sb.ToString(), Encoding.UTF8);

        _logger.LogInformation("Concat file created: {ConcatFilePath}", concatFilePath);
        _logger.LogInformation("Concat file content:{NewLine}{Content}", Environment.NewLine, sb.ToString());
    }

    private static string ResolveSceneImagePath(VideoScene scene, string scenesFolderPath)
    {
        var candidates = new[]
        {
            Path.Combine(scenesFolderPath, $"scene_{scene.SceneNo:00}.png"),
            Path.Combine(scenesFolderPath, $"scene_{scene.SceneNo:00}.jpg"),
            Path.Combine(scenesFolderPath, $"scene_{scene.SceneNo:00}.jpeg"),
            Path.Combine(scenesFolderPath, $"scene_{scene.SceneNo}.png"),
            Path.Combine(scenesFolderPath, $"scene_{scene.SceneNo}.jpg"),
            Path.Combine(scenesFolderPath, $"scene_{scene.SceneNo}.jpeg")
        };

        var existing = candidates.FirstOrDefault(File.Exists);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        return candidates[0];
    }

    private string BuildFfmpegArguments(
        string concatFilePath,
        string? audioFilePath,
        string outputFilePath,
        string filterComplex)
    {
        var args = new List<string>
        {
            "-y",
            "-f", "concat",
            "-safe", "0",
            "-i", Quote(concatFilePath)
        };

        var hasAudio = !string.IsNullOrWhiteSpace(audioFilePath) && File.Exists(audioFilePath);
        if (hasAudio)
        {
            args.Add("-i");
            args.Add(Quote(audioFilePath!));
        }

        args.Add("-vf");
        args.Add(Quote(filterComplex));

        args.Add("-r");
        args.Add("30");

        if (hasAudio)
        {
            args.Add("-c:a");
            args.Add("aac");
            args.Add("-b:a");
            args.Add("192k");
            args.Add("-shortest");
        }
        else
        {
            args.Add("-an");
        }

        args.Add("-c:v");
        args.Add("libx264");
        args.Add("-pix_fmt");
        args.Add("yuv420p");
        args.Add("-preset");
        args.Add("medium");
        args.Add("-crf");
        args.Add("20");
        args.Add("-movflags");
        args.Add("+faststart");
        args.Add(Quote(outputFilePath));

        return string.Join(" ", args);
    }

    private string BuildVideoFilter(VideoJob job, string? subtitleFilePath)
    {
        var filters = new List<string>();

        var aspectRatio = NormalizeAspectRatio(job.AspectRatio.ToString());
        var targetWidth = aspectRatio == "16:9" ? 1920 : 1080;
        var targetHeight = aspectRatio == "16:9" ? 1080 : 1920;

        filters.Add($"scale={targetWidth}:{targetHeight}:force_original_aspect_ratio=decrease");
        filters.Add($"pad={targetWidth}:{targetHeight}:(ow-iw)/2:(oh-ih)/2:color=black");
        filters.Add("setsar=1");
        filters.Add("format=yuv420p");

        if (!string.IsNullOrWhiteSpace(subtitleFilePath) && File.Exists(subtitleFilePath))
        {
            var escapedSubtitlePath = EscapeFilterPath(subtitleFilePath);

            filters.Add(
                $"subtitles='{escapedSubtitlePath}':force_style='Alignment=2,FontName=Arial,FontSize=11,MarginV=52,PrimaryColour=&H00FFFFFF,OutlineColour=&H00000000,BorderStyle=1,Outline=2,Shadow=0,BackColour=&H00000000'");
        }

        return string.Join(",", filters);
    }

    private static string NormalizeAspectRatio(string? aspectRatio)
    {
        if (string.IsNullOrWhiteSpace(aspectRatio))
        {
            return "9:16";
        }

        var value = aspectRatio.Trim();

        if (value.Contains("16:9", StringComparison.OrdinalIgnoreCase))
        {
            return "16:9";
        }

        return "9:16";
    }

    private string ResolveFfmpegExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_ffmpegOptions.BinaryPath) &&
            File.Exists(_ffmpegOptions.BinaryPath))
        {
            return _ffmpegOptions.BinaryPath;
        }

        throw new FileNotFoundException(
            "FFmpeg executable could not be found. Check Ffmpeg:BinaryPath in configuration.");
    }

    private async Task RunProcessAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

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

        _logger.LogInformation("Running ffmpeg: {FileName} {Arguments}", fileName, arguments);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }

            _logger.LogError("FFmpeg process timed out or was cancelled.");
            throw;
        }

        if (process.ExitCode != 0)
        {
            var error = stdErr.ToString();
            var output = stdOut.ToString();

            _logger.LogError(
                "FFmpeg failed with exit code {ExitCode}. StdErr: {StdErr} StdOut: {StdOut}",
                process.ExitCode,
                error,
                output);

            throw new InvalidOperationException(
                $"FFmpeg failed with exit code {process.ExitCode}.{Environment.NewLine}{error}");
        }

        _logger.LogInformation("FFmpeg completed successfully.");
    }

    private static string Quote(string value)
    {
        return $"\"{value}\"";
    }

    private static string EscapeConcatFilePath(string path)
    {
        return path.Replace("\\", "/").Replace("'", "'\\''");
    }

    private static string EscapeFilterPath(string path)
    {
        return path
            .Replace("\\", "/")
            .Replace(":", "\\:")
            .Replace("'", "\\'");
    }
}