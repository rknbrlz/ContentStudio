using System.Text;
using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;
using Hgerman.ContentStudio.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class VoiceGenerationService : IVoiceGenerationService
{
    private readonly OpenAiApiClient _openAiApiClient;
    private readonly IStorageService _storageService;
    private readonly ILogger<VoiceGenerationService> _logger;

    public VoiceGenerationService(
        OpenAiApiClient openAiApiClient,
        IStorageService storageService,
        ILogger<VoiceGenerationService> logger)
    {
        _openAiApiClient = openAiApiClient;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<Asset?> GenerateVoiceAsync(
        VideoJob job,
        CancellationToken cancellationToken = default)
    {
        var script = string.Join(
            " ",
            job.Scenes
                .OrderBy(x => x.SceneNo)
                .Select(x => !string.IsNullOrWhiteSpace(x.VoiceText) ? x.VoiceText.Trim() : x.SceneText.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        if (string.IsNullOrWhiteSpace(script))
        {
            return null;
        }

        byte[] bytes;
        var provider = "OpenAI";
        var fileName = "voice.mp3";
        var mime = "audio/mpeg";

        try
        {
            if (_openAiApiClient.IsConfigured)
            {
                bytes = await _openAiApiClient.GenerateSpeechAsync(
                    script,
                    job.VoiceName ?? string.Empty,
                    cancellationToken);
            }
            else
            {
                provider = "FallbackText";
                fileName = "voice.txt";
                mime = "text/plain";
                bytes = Encoding.UTF8.GetBytes(script);
            }
        }
        catch
        {
            provider = "FallbackText";
            fileName = "voice.txt";
            mime = "text/plain";
            bytes = Encoding.UTF8.GetBytes(script);
        }

        var blobPath = $"projects/{job.ProjectId}/jobs/{job.VideoJobId}/audio/{fileName}";
        var publicUrl = await _storageService.UploadBytesAsync(blobPath, bytes, mime, cancellationToken);

        return new Asset
        {
            VideoJobId = job.VideoJobId,
            AssetType = AssetType.VoiceAudio,
            ProviderName = provider,
            FileName = fileName,
            BlobPath = blobPath,
            PublicUrl = publicUrl,
            MimeType = mime,
            FileSize = bytes.LongLength,
            DurationMs = job.DurationTargetSec * 1000,
            Status = VideoJobStatus.Completed,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
    }
}