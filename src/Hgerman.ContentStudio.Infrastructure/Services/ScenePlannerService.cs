using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class ScenePlannerService : IScenePlannerService
{
    public Task<IReadOnlyList<VideoScene>> BuildScenesAsync(
        VideoJob job,
        string script,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            lines.Add(job.Topic ?? job.Title ?? "Scene");
        }

        var count = Math.Min(Math.Max(lines.Count, 1), 8);
        var totalDuration = Math.Max(15, job.DurationTargetSec);

        decimal currentStart = 0m;

        for (int i = 0; i < count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var text = i < lines.Count ? lines[i] : lines.Last();

            decimal duration;
            if (i == 0)
            {
                duration = 1.5m;
            }
            else
            {
                var remainingScenes = count - i;
                var remainingDuration = totalDuration - currentStart;
                duration = Math.Round(remainingDuration / remainingScenes, 2);
                if (duration < 2.2m)
                {
                    duration = 2.2m;
                }
            }

            var startSec = currentStart;
            var endSec = i == count - 1 ? totalDuration : Math.Round(currentStart + duration, 2);

            result.Add(new VideoScene
            {
                SceneNo = i + 1,
                SceneText = text,
                StartSecond = startSec,
                EndSecond = endSec,
                DurationSecond = endSec - startSec,
                TransitionType = "fade"
            });

            currentStart = endSec;
        }

        return Task.FromResult<IReadOnlyList<VideoScene>>(result);
    }
}