namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IJobProcessor
{
    Task<bool> ProcessNextPendingJobAsync(CancellationToken cancellationToken = default);
    Task<int> RecoverTimedOutJobsAsync(CancellationToken cancellationToken = default);
}