namespace Hgerman.ContentStudio.Application.Interfaces;

public interface ITitleOptimizationService
{
    Task<string> GenerateTitleAsync(
        string topic,
        string languageCode,
        string? hookTemplate,
        string? viralPatternTemplate,
        CancellationToken cancellationToken = default);

    Task<string> GenerateDescriptionAsync(
        string topic,
        string title,
        string languageCode,
        CancellationToken cancellationToken = default);
}