using System.Text;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class PublishService : IPublishService
{
    public Task<PublishTask> CreateDraftAsync(VideoJob job, CancellationToken cancellationToken = default)
    {
        var tags = string.Join(",", new[]
        {
            job.LanguageCode,
            job.PlatformType.ToString(),
            job.ToneType.ToString(),
            "AI Short"
        });

        var description = new StringBuilder()
            .AppendLine(job.Title)
            .AppendLine()
            .AppendLine($"Language: {job.LanguageCode}")
            .AppendLine($"Tone: {job.ToneType}")
            .AppendLine($"Generated from Hgerman Content Studio job {job.JobNo}.")
            .ToString();

        var draft = new PublishTask
        {
            VideoJobId = job.VideoJobId,
            PlatformType = job.PlatformType,
            Title = job.Title,
            Description = description,
            Tags = tags,
            PublishStatus = PublishStatus.Draft,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };

        return Task.FromResult(draft);
    }
}