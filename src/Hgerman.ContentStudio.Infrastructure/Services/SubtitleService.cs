using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class SubtitleService : ISubtitleService
{
    private readonly IStorageService _storageService;

    public SubtitleService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<Asset?> GenerateSubtitleAsync(
        VideoJob job,
        CancellationToken cancellationToken = default)
    {
        if (!job.SubtitleEnabled || job.Scenes == null || job.Scenes.Count == 0)
        {
            return null;
        }

        var scenes = job.Scenes
            .OrderBy(x => x.SceneNo)
            .ToList();

        var cues = BuildSceneAwareCues(scenes);
        if (cues.Count == 0)
        {
            return null;
        }

        var assContent = BuildAss(cues);

        var blobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/subtitles/subtitles.ass";
        var publicUrl = await _storageService.UploadTextAsync(
            blobPath,
            assContent,
            "text/x-ass",
            cancellationToken);

        return new Asset
        {
            VideoJobId = job.VideoJobId,
            AssetType = AssetType.SubtitleFile,
            ProviderName = "InternalSubtitleBuilder",
            FileName = "subtitles.ass",
            BlobPath = blobPath,
            PublicUrl = publicUrl,
            MimeType = "text/x-ass",
            FileSize = Encoding.UTF8.GetByteCount(assContent),
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow,
            DurationSec = scenes.Max(x => x.EndSecond)
        };
    }

    private static List<SubtitleCue> BuildSceneAwareCues(List<VideoScene> scenes)
    {
        var cues = new List<SubtitleCue>();

        foreach (var scene in scenes)
        {
            var rawText = ExtractSubtitleText(scene);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                continue;
            }

            var bursts = SplitIntoBursts(rawText);
            if (bursts.Count == 0)
            {
                continue;
            }

            var sceneStart = (double)scene.StartSecond;
            var sceneEnd = (double)(scene.EndSecond > scene.StartSecond
                ? scene.EndSecond
                : scene.StartSecond + Math.Max(scene.DurationSecond, 1m));

            var sceneDuration = Math.Max(0.80, sceneEnd - sceneStart);
            var totalWords = bursts.Sum(x => CountWords(x.Text));

            double cursor = sceneStart;

            for (int i = 0; i < bursts.Count; i++)
            {
                var burst = bursts[i];
                var words = Math.Max(1, CountWords(burst.Text));

                var proportional = sceneDuration * (words / (double)Math.Max(1, totalWords));
                var minDuration = burst.IsTwoLine ? 0.95 : 0.75;
                var maxDuration = burst.IsTwoLine ? 1.60 : 1.15;

                var cueDuration = Math.Clamp(proportional, minDuration, maxDuration);

                if (i == bursts.Count - 1)
                {
                    cueDuration = Math.Max(minDuration, sceneEnd - cursor);
                }

                var cueEnd = Math.Min(sceneEnd, cursor + cueDuration);

                if (cueEnd <= cursor + 0.20)
                {
                    cueEnd = Math.Min(sceneEnd, cursor + 0.45);
                }

                var style = scene.SceneNo == 1 && i == 0 && sceneStart <= 2.0
                    ? "Hook"
                    : "Default";

                cues.Add(new SubtitleCue
                {
                    Start = cursor,
                    End = cueEnd,
                    Text = burst.Text,
                    Style = style
                });

                cursor = cueEnd + 0.03;
                if (cursor >= sceneEnd)
                {
                    break;
                }
            }
        }

        return cues;
    }

    private static string ExtractSubtitleText(VideoScene scene)
    {
        var candidate = !string.IsNullOrWhiteSpace(scene.OverlayText)
            ? scene.OverlayText!
            : scene.SceneText;

        candidate = candidate.Trim();

        var lines = candidate
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (lines.Count == 0)
        {
            return string.Empty;
        }

        if (lines.Count >= 2)
        {
            var englishLike = lines
                .Where(x => Regex.IsMatch(x, @"[A-Za-z]"))
                .ToList();

            if (englishLike.Count > 0)
            {
                return string.Join(" ", englishLike);
            }
        }

        return candidate;
    }

    private static List<TextBurst> SplitIntoBursts(string text)
    {
        text = CleanText(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<TextBurst>();
        }

        var words = text
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var bursts = new List<TextBurst>();
        int index = 0;

        while (index < words.Count)
        {
            var remaining = words.Count - index;

            if (remaining <= 2)
            {
                bursts.Add(new TextBurst
                {
                    Text = string.Join(" ", words.Skip(index).Take(remaining)).ToUpperInvariant(),
                    IsTwoLine = false
                });
                break;
            }

            if (remaining == 3)
            {
                bursts.Add(new TextBurst
                {
                    Text = string.Join(" ", words.Skip(index).Take(3)).ToUpperInvariant(),
                    IsTwoLine = false
                });
                break;
            }

            if (remaining == 4)
            {
                bursts.Add(new TextBurst
                {
                    Text = $"{words[index].ToUpperInvariant()} {words[index + 1].ToUpperInvariant()}\\N{words[index + 2].ToUpperInvariant()} {words[index + 3].ToUpperInvariant()}",
                    IsTwoLine = true
                });
                break;
            }

            if (remaining == 5)
            {
                bursts.Add(new TextBurst
                {
                    Text = $"{words[index].ToUpperInvariant()} {words[index + 1].ToUpperInvariant()} {words[index + 2].ToUpperInvariant()}\\N{words[index + 3].ToUpperInvariant()} {words[index + 4].ToUpperInvariant()}",
                    IsTwoLine = true
                });
                break;
            }

            bursts.Add(new TextBurst
            {
                Text = $"{words[index].ToUpperInvariant()} {words[index + 1].ToUpperInvariant()} {words[index + 2].ToUpperInvariant()}",
                IsTwoLine = false
            });

            index += 3;
        }

        return bursts;
    }

    private static string CleanText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var value = input.Trim();
        value = value.Replace("…", "...");
        value = value.Replace("—", "-");
        value = value.Replace("–", "-");
        value = Regex.Replace(value, @"\s+", " ");
        return value;
    }

    private static int CountWords(string text)
    {
        return text
            .Replace("\\N", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }

    private static string BuildAss(List<SubtitleCue> cues)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[Script Info]");
        sb.AppendLine("Title: Hgerman Content Studio");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine("WrapStyle: 2");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine("PlayResX: 1080");
        sb.AppendLine("PlayResY: 1920");
        sb.AppendLine();

        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name,Fontname,Fontsize,PrimaryColour,SecondaryColour,OutlineColour,BackColour,Bold,Italic,Underline,StrikeOut,ScaleX,ScaleY,Spacing,Angle,BorderStyle,Outline,Shadow,Alignment,MarginL,MarginR,MarginV,Encoding");
        sb.AppendLine("Style: Default,Arial,24,&H00FFFFFF,&H00FFFFFF,&H00000000,&H64000000,-1,0,0,0,100,100,0,0,1,2.8,0.8,2,70,70,210,1");
        sb.AppendLine("Style: Hook,Arial,28,&H0000F8FF,&H0000F8FF,&H00000000,&H78000000,-1,0,0,0,102,102,0,0,1,3.2,1.0,2,60,60,255,1");
        sb.AppendLine();

        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer,Start,End,Style,Name,MarginL,MarginR,MarginV,Effect,Text");

        foreach (var cue in cues)
        {
            sb.AppendLine(
                $"Dialogue: 0,{ToAssTime(cue.Start)},{ToAssTime(cue.End)},{cue.Style},,0,0,0,,{EscapeAss(cue.Text)}");
        }

        return sb.ToString();
    }

    private static string EscapeAss(string text)
    {
        return text
            .Replace(@"\", @"\\")
            .Replace("{", "(")
            .Replace("}", ")");
    }

    private static string ToAssTime(double seconds)
    {
        if (seconds < 0)
        {
            seconds = 0;
        }

        var ts = TimeSpan.FromSeconds(seconds);
        int hours = (int)ts.TotalHours;
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1:00}:{2:00}.{3:00}",
            hours,
            ts.Minutes,
            ts.Seconds,
            ts.Milliseconds / 10);
    }

    private sealed class SubtitleCue
    {
        public double Start { get; set; }
        public double End { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Style { get; set; } = "Default";
    }

    private sealed class TextBurst
    {
        public string Text { get; set; } = string.Empty;
        public bool IsTwoLine { get; set; }
    }
}