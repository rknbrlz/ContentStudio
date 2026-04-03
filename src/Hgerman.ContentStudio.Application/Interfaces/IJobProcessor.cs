namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IJobProcessor
{
    Task<int> RecoverTimedOutJobsAsync(CancellationToken cancellationToken = default);
    Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken = default);
}