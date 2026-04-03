namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IAnalyticsFeedbackLoopService
{
    Task<int> EvaluateProfileAsync(int automationProfileId, CancellationToken cancellationToken = default);
    Task<int> EvaluateAllAsync(CancellationToken cancellationToken = default);
}