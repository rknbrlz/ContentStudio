using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Shared.DTOs;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IVideoJobService
{
    Task<int> CreateJobAsync(CreateVideoJobRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VideoJobListItemDto>> GetJobListAsync(CancellationToken cancellationToken = default);
    Task<VideoJob?> GetJobAsync(int videoJobId, CancellationToken cancellationToken = default);
    Task QueueJobAsync(int videoJobId, CancellationToken cancellationToken = default);
    Task RetryJobAsync(int videoJobId, CancellationToken cancellationToken = default);
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
    Task AttachUploadedSourceImageAsync(
        int videoJobId,
        string fileName,
        string contentType,
        byte[] fileBytes,
        CancellationToken cancellationToken = default);
}