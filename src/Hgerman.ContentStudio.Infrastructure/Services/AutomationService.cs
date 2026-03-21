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

            var created = await CreateJobFromProfileAsync(
                profile,
                nowUtc,
                bypassDailyLimit: false,
                updateLastRun: true,
                cancellationToken);

            createdCount += created;
        }

        if (createdCount > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return createdCount;
    }

    public async Task<int> RunAllActiveNowAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        var profiles = await _db.AutomationProfiles
            .Where(x => x.IsActive)
            .OrderBy(x => x.AutomationProfileId)
            .ToListAsync(cancellationToken);

        var createdCount = 0;

        foreach (var profile in profiles)
        {
            var created = await CreateJobFromProfileAsync(
                profile,
                nowUtc,
                bypassDailyLimit: true,
                updateLastRun: false,
                cancellationToken);

            createdCount += created;
        }

        if (createdCount > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return createdCount;
    }

    public async Task<int> RunProfileNowAsync(int automationProfileId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.AutomationProfiles
            .FirstOrDefaultAsync(x => x.AutomationProfileId == automationProfileId, cancellationToken);

        if (profile == null || !profile.IsActive)
            return 0;

        var created = await CreateJobFromProfileAsync(
            profile,
            DateTime.UtcNow,
            bypassDailyLimit: true,
            updateLastRun: false,
            cancellationToken);

        if (created > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return created;
    }

    public async Task<int> PublishCompletedAutoJobsAsync(CancellationToken cancellationToken = default)
    {
        var completedAutoJobs = await _db.VideoJobs
            .Where(x =>
                x.Status == VideoJobStatus.Completed &&
                x.JobNo.StartsWith("AUTO-") &&
                !x.IsPublished)
            .OrderBy(x => x.VideoJobId)
            .Take(10)
            .ToListAsync(cancellationToken);

        var publishedCount = 0;

        foreach (var job in completedAutoJobs)
        {
            try
            {
                var profileId = ExtractProfileIdFromJobNo(job.JobNo);
                if (!profileId.HasValue)
                    continue;

                var profile = await _db.AutomationProfiles
                    .FirstOrDefaultAsync(x => x.AutomationProfileId == profileId.Value, cancellationToken);

                if (profile == null || !profile.AutoPublishYouTube)
                    continue;

                var publishUrl = await _publishService.PublishToYouTubeAsync(job.VideoJobId, cancellationToken);

                if (!string.IsNullOrWhiteSpace(publishUrl))
                {
                    job.IsPublished = true;
                    job.PublishedDate = DateTime.UtcNow;
                    job.PublishedUrl = TrimToLength(publishUrl, 1000);

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

        if (publishedCount > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return publishedCount;
    }

    private async Task<int> CreateJobFromProfileAsync(
        AutomationProfile profile,
        DateTime nowUtc,
        bool bypassDailyLimit,
        bool updateLastRun,
        CancellationToken cancellationToken)
    {
        var autoPrefix = GetAutoPrefix(profile.AutomationProfileId);

        var createdToday = await _db.VideoJobs.CountAsync(
            x => x.CreatedDate.Date == nowUtc.Date &&
                 x.JobNo.StartsWith(autoPrefix),
            cancellationToken);

        if (!bypassDailyLimit && createdToday >= profile.DailyVideoLimit)
        {
            _logger.LogInformation(
                "Automation profile {ProfileId} skipped because daily limit reached.",
                profile.AutomationProfileId);

            return 0;
        }

        var rawTopic = BuildTopic(profile, createdToday + 1, nowUtc);

        var rawTitle = await _titleOptimizationService.GenerateTitleAsync(
            rawTopic,
            profile.LanguageCode,
            profile.HookTemplate,
            profile.ViralPatternTemplate,
            cancellationToken);

        var safeTitle = TrimToLength(rawTitle, 200);
        var safeTopic = TrimToLength(rawTopic, 500);

        var description = await _titleOptimizationService.GenerateDescriptionAsync(
            safeTopic,
            safeTitle,
            profile.LanguageCode,
            cancellationToken);

        var safeSourcePrompt = TrimToLength(
            BuildSourcePrompt(profile, safeTopic, description),
            4000);

        var job = new VideoJob
        {
            ProjectId = profile.ProjectId,
            JobNo = BuildJobNo(profile.AutomationProfileId, nowUtc),
            Title = safeTitle,
            Topic = safeTopic,
            SourcePrompt = safeSourcePrompt,
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
            IsPublished = false,
            CreatedDate = nowUtc,
            UpdatedDate = nowUtc
        };

        _db.VideoJobs.Add(job);

        if (updateLastRun)
        {
            profile.LastRunAtUtc = nowUtc;
            profile.LastGeneratedDateUtc = nowUtc.Date;
            profile.UpdatedDate = nowUtc;
        }

        _logger.LogInformation(
            "Auto video job created. ProfileId={ProfileId}, JobNo={JobNo}, Title={Title}",
            profile.AutomationProfileId,
            job.JobNo,
            job.Title);

        return 1;
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

    private static string BuildJobNo(int automationProfileId, DateTime nowUtc)
    {
        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        return $"AUTO-{automationProfileId}-{nowUtc:yyyyMMddHHmmss}-{suffix}";
    }

    private static int? ExtractProfileIdFromJobNo(string? jobNo)
    {
        if (string.IsNullOrWhiteSpace(jobNo))
            return null;

        var parts = jobNo.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return null;

        if (!string.Equals(parts[0], "AUTO", StringComparison.OrdinalIgnoreCase))
            return null;

        return int.TryParse(parts[1], out var profileId) ? profileId : null;
    }

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

    private static string TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var cleaned = value.Trim();

        if (cleaned.Length <= maxLength)
            return cleaned;

        var shortened = cleaned.Substring(0, maxLength).Trim();

        var lastSpace = shortened.LastIndexOf(' ');
        if (lastSpace > 20)
            shortened = shortened.Substring(0, lastSpace).Trim();

        return shortened;
    }
}