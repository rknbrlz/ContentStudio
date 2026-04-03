namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IAutomationService
{
    Task<int> RunScheduledAutomationsAsync(CancellationToken cancellationToken = default);
    Task<int> RunAllActiveNowAsync(CancellationToken cancellationToken = default);
    Task<int> RunProfileNowAsync(int automationProfileId, CancellationToken cancellationToken = default);
    Task<int> PublishCompletedAutoJobsAsync(CancellationToken cancellationToken = default);
}