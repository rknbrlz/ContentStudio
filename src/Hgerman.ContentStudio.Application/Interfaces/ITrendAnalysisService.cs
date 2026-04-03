using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface ITrendAnalysisService
{
    Task<List<TrendIdeaResult>> BuildTrendIdeasAsync(
        AutomationProfile profile,
        CancellationToken cancellationToken = default);
}

public sealed class TrendIdeaResult
{
    public string Keyword { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string SourceName { get; set; } = "internal";
    public string? Notes { get; set; }
}