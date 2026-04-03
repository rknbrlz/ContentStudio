using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hgerman.ContentStudio.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public sealed class OpenAiApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiProviderOptions _options;
    private readonly ILogger<OpenAiApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public OpenAiApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AiProviderOptions> options,
        ILogger<OpenAiApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.OpenAiApiKey);

    public async Task<string> GenerateStructuredTextAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var payload = new
        {
            model = _options.OpenAiModel,
            input = new object[]
            {
                new
                {
                    role = "system",
                    content = new object[]
                    {
                        new { type = "input_text", text = systemPrompt }
                    }
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = userPrompt }
                    }
                }
            }
        };

        using var response = await SendJsonWithRetryAsync("/responses", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty("output_text", out var outputTextElement))
        {
            var outputText = outputTextElement.GetString();
            if (!string.IsNullOrWhiteSpace(outputText))
            {
                return outputText.Trim();
            }
        }

        if (doc.RootElement.TryGetProperty("output", out var outputArray) &&
            outputArray.ValueKind == JsonValueKind.Array)
        {
            var textParts = new List<string>();

            foreach (var item in outputArray.EnumerateArray())
            {
                if (!item.TryGetProperty("content", out var contentArray) ||
                    contentArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contentArray.EnumerateArray())
                {
                    if (content.TryGetProperty("text", out var textElement))
                    {
                        var text = textElement.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            textParts.Add(text.Trim());
                        }
                    }
                }
            }

            if (textParts.Count > 0)
            {
                return string.Join(Environment.NewLine, textParts);
            }
        }

        _logger.LogWarning("OpenAI response did not contain usable text. Raw body: {Body}", body);
        throw new InvalidOperationException("OpenAI response did not contain usable text output.");
    }

    public async Task<byte[]> GenerateImageAsync(
        string prompt,
        string size = "1024x1536",
        string quality = "medium",
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var payload = new
        {
            model = _options.ImageModel,
            prompt,
            size,
            quality,
            output_format = "png"
        };

        using var response = await SendJsonWithRetryAsync("/images/generations", payload, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.TryGetProperty("data", out var data) &&
            data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("b64_json", out var b64))
                {
                    var base64 = b64.GetString();
                    if (!string.IsNullOrWhiteSpace(base64))
                    {
                        return Convert.FromBase64String(base64);
                    }
                }
            }
        }

        if (doc.RootElement.TryGetProperty("b64_json", out var directB64))
        {
            var base64 = directB64.GetString();
            if (!string.IsNullOrWhiteSpace(base64))
            {
                return Convert.FromBase64String(base64);
            }
        }

        _logger.LogWarning("OpenAI image response did not contain image bytes. Raw body: {Body}", body);
        throw new InvalidOperationException("OpenAI image response did not contain image bytes.");
    }

    public async Task<byte[]> GenerateSpeechAsync(
        string text,
        string voice,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var payload = new
        {
            model = _options.SpeechModel,
            voice = string.IsNullOrWhiteSpace(voice) ? _options.DefaultVoice : voice,
            input = text,
            format = "mp3"
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = CreateClient();
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_options.OpenAiBaseUrl.TrimEnd('/')}/audio/speech");

                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var failure = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (attempt < maxAttempts && IsRetriableStatusCode(response.StatusCode))
                    {
                        _logger.LogWarning(
                            "OpenAI speech request failed with {StatusCode} on attempt {Attempt}. Retrying...",
                            (int)response.StatusCode,
                            attempt);

                        await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"OpenAI speech generation failed: {(int)response.StatusCode} - {failure}");
                }

                return await response.Content.ReadAsByteArrayAsync(cancellationToken);
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientException(ex))
            {
                _logger.LogWarning(
                    ex,
                    "OpenAI speech request transient failure on attempt {Attempt}. Retrying...",
                    attempt);

                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("OpenAI speech generation failed after retry attempts.");
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(nameof(OpenAiApiClient));
        client.Timeout = TimeSpan.FromMinutes(5);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.OpenAiApiKey);

        if (!string.IsNullOrWhiteSpace(_options.OpenAiProject))
        {
            client.DefaultRequestHeaders.Remove("OpenAI-Project");
            client.DefaultRequestHeaders.Add("OpenAI-Project", _options.OpenAiProject);
        }

        return client;
    }

    private async Task<HttpResponseMessage> SendJsonWithRetryAsync(
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var client = CreateClient();
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{_options.OpenAiBaseUrl.TrimEnd('/')}{relativePath}");

                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var failure = await response.Content.ReadAsStringAsync(cancellationToken);

                    if (attempt < maxAttempts && IsRetriableStatusCode(response.StatusCode))
                    {
                        _logger.LogWarning(
                            "OpenAI request to {Path} failed with {StatusCode} on attempt {Attempt}. Retrying...",
                            relativePath,
                            (int)response.StatusCode,
                            attempt);

                        response.Dispose();
                        await Task.Delay(GetRetryDelay(attempt), cancellationToken);
                        continue;
                    }

                    throw new InvalidOperationException(
                        $"OpenAI request failed: {(int)response.StatusCode} - {failure}");
                }

                return response;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientException(ex))
            {
                _logger.LogWarning(
                    ex,
                    "OpenAI request to {Path} transient failure on attempt {Attempt}. Retrying...",
                    relativePath,
                    attempt);

                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException($"OpenAI request to {relativePath} failed after retry attempts.");
    }

    private static bool IsRetriableStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code == 408 || code == 429 || code >= 500;
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is TaskCanceledException
            || ex is TimeoutException
            || ex is HttpRequestException
            || ex.InnerException is TimeoutException
            || ex.InnerException is HttpRequestException;
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        return attempt switch
        {
            1 => TimeSpan.FromSeconds(2),
            2 => TimeSpan.FromSeconds(5),
            _ => TimeSpan.FromSeconds(10)
        };
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }
    }
}