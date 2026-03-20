namespace Hgerman.ContentStudio.Application.Interfaces;

public interface IHookGenerationService
{
    Task<string> GenerateHookAsync(
        string topic,
        string languageCode,
        CancellationToken cancellationToken = default);
}