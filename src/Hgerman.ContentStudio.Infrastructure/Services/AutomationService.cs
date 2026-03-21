using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class AutomationService : IAutomationService
{
    private readonly ContentStudioDbContext _db;
    private readonly ITitleOptimizationService _titleOptimizationService;
    private readonly IPublishService _publishService;
    private readonly ILogger<AutomationService> _logger;

    public AutomationService(
        ContentStudioDbContext db,
        ITitleOptimizationService titleOptimizationService,
        IPublishService publishService,
        ILogger<AutomationService> logger)
    {
        _db = db;
        _titleOptimizationService = titleOptimizationService;
        _publishService = publishService;
        _logger = logger;
    }

    public async Task<int> RunScheduledAutomationsAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        var profiles = await _db.AutomationProfiles
            .Where(x => x.IsActive)
            .OrderBy(x => x.AutomationProfileId)
            .ToListAsync(cancellationToken);

        var createdCount = 0;

        foreach (var profile in profiles)
        {
            if (!ShouldRunNow(profile, nowUtc))
                continue;

            var autoPrefix = GetAutoPrefix(profile.AutomationProfileId);

            var createdToday = await _db.VideoJobs.CountAsync(
                x => x.CreatedDate.Date == nowUtc.Date &&
                     x.JobNo.StartsWith(autoPrefix),
                cancellationToken);

            if (createdToday >= profile.DailyVideoLimit)
            {
                _logger.LogInformation(
                    "Automation profile {ProfileId} skipped because daily limit reached.",
                    profile.AutomationProfileId);
                continue;
            }

            var topic = BuildTopic(profile, createdToday + 1, nowUtc);

            var title = await _titleOptimizationService.GenerateTitleAsync(
                topic,
                profile.LanguageCode,
                profile.HookTemplate,
                profile.ViralPatternTemplate,
                cancellationToken);

            var description = await _titleOptimizationService.GenerateDescriptionAsync(
                topic,
                title,
                profile.LanguageCode,
                cancellationToken);

            var job = new VideoJob
            {
                ProjectId = profile.ProjectId,
                JobNo = $"{autoPrefix}{nowUtc:yyyyMMddHHmmss}",
                Title = title,
                Topic = topic,
                SourcePrompt = BuildSourcePrompt(profile, topic, description),
                LanguageCode = profile.LanguageCode,
                PlatformType = (PlatformType)profile.PlatformType,
                ToneType = (ToneType)profile.ToneType,
                DurationTargetSec = profile.DurationTargetSec,
                AspectRatio = (AspectRatioType)profile.AspectRatio,
                SubtitleEnabled = profile.SubtitleEnabled,
                ThumbnailEnabled = profile.ThumbnailEnabled,
                Status = VideoJobStatus.Queued,
                CurrentStep = VideoPipelineStep.Queued,
                ProgressPercent = 0,
                OverlayEnabled = true,
                MotionMode = "cinematic",
                RenderProfile = "cinematic",
                CreatedDate = nowUtc,
                UpdatedDate = nowUtc
            };

            _db.VideoJobs.Add(job);

            profile.LastRunAtUtc = nowUtc;
            profile.LastGeneratedDateUtc = nowUtc.Date;
            profile.UpdatedDate = nowUtc;

            createdCount++;

            _logger.LogInformation(
                "Auto video job created. ProfileId={ProfileId}, JobNo={JobNo}, Title={Title}",
                profile.AutomationProfileId,
                job.JobNo,
                job.Title);
        }

        if (createdCount > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return createdCount;
    }

    public async Task<int> PublishCompletedAutoJobsAsync(CancellationToken cancellationToken = default)
    {
        var completedAutoJobs = await _db.VideoJobs
            .Where(x =>
                x.Status == VideoJobStatus.Completed &&
                x.JobNo.StartsWith("AUTO-"))
            .OrderBy(x => x.VideoJobId)
            .Take(5)
            .ToListAsync(cancellationToken);

        var publishedCount = 0;

        foreach (var job in completedAutoJobs)
        {
            try
            {
                var publishUrl = await _publishService.PublishToYouTubeAsync(job.VideoJobId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(publishUrl))
                {
                    publishedCount++;

                    _logger.LogInformation(
                        "Auto published VideoJobId={VideoJobId}, Url={PublishUrl}",
                        job.VideoJobId,
                        publishUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Auto publish failed for VideoJobId={VideoJobId}",
                    job.VideoJobId);
            }
        }

        return publishedCount;
    }

    private static bool ShouldRunNow(AutomationProfile profile, DateTime nowUtc)
    {
        var hours = ParseHours(profile.PreferredHoursCsv);

        if (hours.Count == 0)
            return false;

        if (!hours.Contains(nowUtc.Hour))
            return false;

        if (profile.LastRunAtUtc.HasValue &&
            profile.LastRunAtUtc.Value.Date == nowUtc.Date &&
            profile.LastRunAtUtc.Value.Hour == nowUtc.Hour)
        {
            return false;
        }

        return true;
    }

    private static HashSet<int> ParseHours(string csv)
    {
        var result = new HashSet<int>();

        if (string.IsNullOrWhiteSpace(csv))
            return result;

        var parts = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (int.TryParse(part, out var hour) && hour >= 0 && hour <= 23)
                result.Add(hour);
        }

        return result;
    }

    private static string GetAutoPrefix(int automationProfileId)
        => $"AUTO-{automationProfileId}-";

    private static string BuildTopic(AutomationProfile profile, int sequence, DateTime nowUtc)
    {
        return $"{profile.TopicPrompt} | slot {sequence} | {nowUtc:yyyy-MM-dd}";
    }

    private static string BuildSourcePrompt(AutomationProfile profile, string topic, string description)
    {
        return $"""
Create a short-form motivational video script.

Topic:
{topic}

Hook style:
{profile.HookTemplate}

Viral pattern:
{profile.ViralPatternTemplate}

Extra context:
{description}

Rules:
- Strong first 2 seconds
- Emotional hook
- Simple words
- Short sentences
- Natural CTA at end
""";
    }
}