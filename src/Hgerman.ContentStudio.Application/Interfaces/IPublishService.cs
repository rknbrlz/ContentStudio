using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IPublishService
{
    Task<PublishTask> CreateDraftAsync(VideoJob job, CancellationToken cancellationToken = default);
}
