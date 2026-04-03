using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Application.Interfaces;

public interface ITitleFeedbackService
{
    Task<List<TitleVariantResult>> GenerateVariantsAsync(
        AutomationProfile profile,
        string topic,
        string currentTitle,
        CancellationToken cancellationToken = default);

    Task RecordSyntheticPerformanceAsync(
        int videoJobId,
        int automationProfileId,
        IEnumerable<TitleVariantResult> variants,
        CancellationToken cancellationToken = default);
}

public sealed class TitleVariantResult
{
    public int VariantNo { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? HookType { get; set; }
    public string? PatternType { get; set; }
    public decimal PredictedScore { get; set; }
    public bool IsWinner { get; set; }
}