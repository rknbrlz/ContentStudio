using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class ScenePlannerService : IScenePlannerService
{
    public Task<IReadOnlyList<VideoScene>> BuildScenesAsync(
        VideoJob job,
        string script,
        CancellationToken cancellationToken = default)
    {
        var result = new List<VideoScene>();

        if (string.IsNullOrWhiteSpace(script))
        {
            return Task.FromResult<IReadOnlyList<VideoScene>>(result);
        }

        var lines = script
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (lines.Count == 0)
        {
            lines.Add(job.Topic ?? job.Title);
        }

        var count = Math.Min(Math.Max(lines.Count, 1), 8);
        var totalDuration = Math.Max(15, job.DurationTargetSec);
        var sceneDuration = Math.Round((decimal)totalDuration / count, 2);

        decimal current = 0;

        for (int i = 0; i < count; i++)
        {
            var voiceText = i < lines.Count ? lines[i] : lines.Last();

            var scene = new VideoScene
            {
                SceneNo = i + 1,
                VoiceText = voiceText,
                SceneText = voiceText,
                SubtitleText = null,

                // IMPORTANT: disable overlay text
                OverlayText = null,
                OverlayStartSecond = null,
                OverlayEndSecond = null,

                StartSecond = current,
                EndSecond = current + sceneDuration,
                DurationSecond = sceneDuration,
                TransitionType = "fade",
                SourceType = job.InputMode.ToString().ToLowerInvariant(),
                VisualType = "photo",
                CameraMotion = (i % 4) switch
                {
                    0 => "zoom_in",
                    1 => "pan_left",
                    2 => "pan_right",
                    _ => "drift_up"
                },
                CropFocusX = 0.50m,
                CropFocusY = 0.50m,
                MotionIntensity = 0.18m,
                Status = VideoJobStatus.Draft,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            result.Add(scene);
            current += sceneDuration;
        }

        return Task.FromResult<IReadOnlyList<VideoScene>>(result);
    }
}