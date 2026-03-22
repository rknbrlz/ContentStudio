using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Hgerman.ContentStudio.Application.Interfaces;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class SubtitleService : ISubtitleService
{
    public async Task<string?> GenerateAssSubtitleAsync(
        string bilingualScript,
        string outputFolderPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bilingualScript))
        {
            return null;
        }

        Directory.CreateDirectory(outputFolderPath);

        var subtitlePath = Path.Combine(outputFolderPath, "subtitles.ass");

        var englishLines = ExtractEnglishLines(bilingualScript);
        if (englishLines.Count == 0)
        {
            return null;
        }

        var cues = BuildCues(englishLines);
        if (cues.Count == 0)
        {
            return null;
        }

        var ass = BuildAssFile(cues);
        await File.WriteAllTextAsync(subtitlePath, ass, new UTF8Encoding(false), cancellationToken);

        return subtitlePath;
    }

    private static List<string> ExtractEnglishLines(string bilingualScript)
    {
        var rawLines = bilingualScript
            .Replace("\r\n", "\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        var cleaned = new List<string>();

        foreach (var line in rawLines)
        {
            var value = line.Trim();

            if (value.StartsWith("PL:", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("[PL]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (value.StartsWith("EN:", StringComparison.OrdinalIgnoreCase))
            {
                cleaned.Add(value.Substring(3).Trim());
                continue;
            }

            if (value.StartsWith("[EN]", StringComparison.OrdinalIgnoreCase))
            {
                cleaned.Add(value.Substring(4).Trim());
                continue;
            }

            cleaned.Add(value);
        }

        if (cleaned.Count >= 2)
        {
            var alternatingEnglish = new List<string>();

            for (int i = 1; i < cleaned.Count; i += 2)
            {
                alternatingEnglish.Add(cleaned[i]);
            }

            if (alternatingEnglish.Count > 0)
            {
                return NormalizeLines(alternatingEnglish);
            }
        }

        return NormalizeLines(cleaned);
    }

    private static List<string> NormalizeLines(IEnumerable<string> lines)
    {
        return lines
            .Select(CleanText)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
    }

    private static string CleanText(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var text = input.Trim();

        text = Regex.Replace(text, @"\s+", " ");
        text = text.Replace("…", "...");
        text = text.Replace("—", "-");
        text = text.Replace("–", "-");

        return text.Trim();
    }

    private static List<SubtitleCue> BuildCues(List<string> englishLines)
    {
        var cues = new List<SubtitleCue>();
        double currentTime = 0.00;

        foreach (var line in englishLines)
        {
            var chunks = SplitIntoCinematicChunks(line);

            foreach (var chunk in chunks)
            {
                if (string.IsNullOrWhiteSpace(chunk))
                {
                    continue;
                }

                var wordCount = CountWords(chunk);

                double duration = wordCount switch
                {
                    <= 2 => 0.90,
                    3 => 1.05,
                    4 => 1.20,
                    5 => 1.35,
                    _ => 1.50
                };

                var cue = new SubtitleCue
                {
                    Start = currentTime,
                    End = currentTime + duration,
                    Text = chunk
                };

                cues.Add(cue);
                currentTime += duration + 0.08;
            }
        }

        return MergeTinyCues(cues);
    }

    private static List<SubtitleCue> MergeTinyCues(List<SubtitleCue> cues)
    {
        if (cues.Count == 0)
        {
            return cues;
        }

        var result = new List<SubtitleCue>();
        SubtitleCue? pending = null;

        foreach (var cue in cues)
        {
            if (pending == null)
            {
                pending = cue;
                continue;
            }

            var pendingWords = CountWords(pending.Text);
            var cueWords = CountWords(cue.Text);

            if (pendingWords <= 1 && cueWords <= 2)
            {
                pending.Text = $"{pending.Text}\\N{cue.Text}";
                pending.End = cue.End;
            }
            else
            {
                result.Add(pending);
                pending = cue;
            }
        }

        if (pending != null)
        {
            result.Add(pending);
        }

        return result;
    }

    private static List<string> SplitIntoCinematicChunks(string sentence)
    {
        var words = sentence
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (words.Count == 0)
        {
            return new List<string>();
        }

        var chunks = new List<string>();

        if (words.Count <= 3)
        {
            chunks.Add(string.Join(" ", words).ToUpperInvariant());
            return chunks;
        }

        if (words.Count <= 6)
        {
            var line1 = string.Join(" ", words.Take(Math.Min(3, words.Count)));
            var line2 = string.Join(" ", words.Skip(Math.Min(3, words.Count)));

            if (!string.IsNullOrWhiteSpace(line2))
            {
                chunks.Add($"{line1.ToUpperInvariant()}\\N{line2.ToUpperInvariant()}");
            }
            else
            {
                chunks.Add(line1.ToUpperInvariant());
            }

            return chunks;
        }

        int index = 0;
        while (index < words.Count)
        {
            var remaining = words.Count - index;

            if (remaining <= 3)
            {
                chunks.Add(string.Join(" ", words.Skip(index)).ToUpperInvariant());
                break;
            }

            if (remaining <= 6)
            {
                var first = string.Join(" ", words.Skip(index).Take(3));
                var second = string.Join(" ", words.Skip(index + 3).Take(3));
                chunks.Add($"{first.ToUpperInvariant()}\\N{second.ToUpperInvariant()}");
                break;
            }

            var part1 = string.Join(" ", words.Skip(index).Take(3));
            var part2 = string.Join(" ", words.Skip(index + 3).Take(3));
            chunks.Add($"{part1.ToUpperInvariant()}\\N{part2.ToUpperInvariant()}");
            index += 6;
        }

        return chunks;
    }

    private static int CountWords(string text)
    {
        return text
            .Replace("\\N", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Length;
    }

    private static string BuildAssFile(List<SubtitleCue> cues)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[Script Info]");
        sb.AppendLine("Title: Hgerman Content Studio Subtitles");
        sb.AppendLine("ScriptType: v4.00+");
        sb.AppendLine("WrapStyle: 2");
        sb.AppendLine("ScaledBorderAndShadow: yes");
        sb.AppendLine("PlayResX: 1080");
        sb.AppendLine("PlayResY: 1920");
        sb.AppendLine("YCbCr Matrix: TV.601");
        sb.AppendLine();

        sb.AppendLine("[V4+ Styles]");
        sb.AppendLine("Format: Name,Fontname,Fontsize,PrimaryColour,SecondaryColour,OutlineColour,BackColour,Bold,Italic,Underline,StrikeOut,ScaleX,ScaleY,Spacing,Angle,BorderStyle,Outline,Shadow,Alignment,MarginL,MarginR,MarginV,Encoding");
        sb.AppendLine("Style: Default,Arial,20,&H00FFFFFF,&H000000FF,&H00000000,&H64000000,-1,0,0,0,100,100,0,0,1,2.2,0.8,2,70,70,170,1");
        sb.AppendLine();

        sb.AppendLine("[Events]");
        sb.AppendLine("Format: Layer,Start,End,Style,Name,MarginL,MarginR,MarginV,Effect,Text");

        foreach (var cue in cues)
        {
            sb.AppendLine(
                $"Dialogue: 0,{ToAssTime(cue.Start)},{ToAssTime(cue.End)},Default,,0,0,0,,{EscapeAssText(cue.Text)}");
        }

        return sb.ToString();
    }

    private static string EscapeAssText(string value)
    {
        return value
            .Replace(@"\", @"\\")
            .Replace("{", "(")
            .Replace("}", ")");
    }

    private static string ToAssTime(double totalSeconds)
    {
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var ts = TimeSpan.FromSeconds(totalSeconds);
        int hours = (int)ts.TotalHours;
        int minutes = ts.Minutes;
        int seconds = ts.Seconds;
        int centiseconds = ts.Milliseconds / 10;

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}:{1:00}:{2:00}.{3:00}",
            hours,
            minutes,
            seconds,
            centiseconds);
    }

    private sealed class SubtitleCue
    {
        public double Start { get; set; }
        public double End { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}