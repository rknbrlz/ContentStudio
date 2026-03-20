using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class UploadMetadataService : IUploadMetadataService
{
    private readonly ILogger<UploadMetadataService> _logger;

    public UploadMetadataService(ILogger<UploadMetadataService> logger)
    {
        _logger = logger;
    }

    public Task<UploadMetadataResult> GenerateYouTubeMetadataAsync(
        VideoJob job,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var topic = string.IsNullOrWhiteSpace(job.Topic)
            ? job.Title ?? "Motivation"
            : job.Topic;

        var result = job.LanguageCode?.ToLowerInvariant() switch
        {
            "pl" => BuildPolishMetadata(job, topic),
            "tr" => BuildTurkishMetadata(job, topic),
            _ => BuildEnglishMetadata(job, topic)
        };

        _logger.LogInformation("Upload metadata generated for VideoJobId {VideoJobId}", job.VideoJobId);

        return Task.FromResult(result);
    }

    private static UploadMetadataResult BuildPolishMetadata(VideoJob job, string topic)
    {
        return new UploadMetadataResult
        {
            Title = TrimToLength($"{topic} | Krótka motywacja", 65),
            Description =
                $"Krótki film motywacyjny o temacie: {topic}.{Environment.NewLine}{Environment.NewLine}" +
                $"Język: {job.LanguageCode}{Environment.NewLine}" +
                $"Ton: {job.ToneType}{Environment.NewLine}{Environment.NewLine}" +
                $"#motywacja #rozwojosobisty #dyscyplina #shorts",
            Tags = new List<string>
            {
                "motywacja",
                "rozwojosobisty",
                "dyscyplina",
                "sukces",
                "mindset",
                "shorts",
                "viral",
                "inspiracja"
            }
        };
    }

    private static UploadMetadataResult BuildTurkishMetadata(VideoJob job, string topic)
    {
        return new UploadMetadataResult
        {
            Title = TrimToLength($"{topic} | Kısa Motivasyon", 65),
            Description =
                $"{topic} hakkında kısa motivasyon videosu.{Environment.NewLine}{Environment.NewLine}" +
                $"Dil: {job.LanguageCode}{Environment.NewLine}" +
                $"Ton: {job.ToneType}{Environment.NewLine}{Environment.NewLine}" +
                $"#motivasyon #başarı #disiplin #shorts",
            Tags = new List<string>
            {
                "motivasyon",
                "başarı",
                "disiplin",
                "gelişim",
                "mindset",
                "shorts",
                "viral",
                "ilham"
            }
        };
    }

    private static UploadMetadataResult BuildEnglishMetadata(VideoJob job, string topic)
    {
        return new UploadMetadataResult
        {
            Title = TrimToLength($"{topic} | Viral Motivation Short", 65),
            Description =
                $"Short motivational video about {topic}.{Environment.NewLine}{Environment.NewLine}" +
                $"Language: {job.LanguageCode}{Environment.NewLine}" +
                $"Tone: {job.ToneType}{Environment.NewLine}{Environment.NewLine}" +
                $"#motivation #success #discipline #shorts",
            Tags = new List<string>
            {
                "motivation",
                "success",
                "discipline",
                "mindset",
                "growth",
                "shorts",
                "viral",
                "inspiration"
            }
        };
    }

    private static string TrimToLength(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength].Trim();
    }
}