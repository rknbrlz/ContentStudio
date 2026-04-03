namespace Hgerman.ContentStudio.Shared.Options;

public class AiProviderOptions
{
    public string OpenAiApiKey { get; set; } = string.Empty;
    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1";
    public string OpenAiModel { get; set; } = "gpt-5";
    public string ImageModel { get; set; } = "gpt-image-1";
    public string SpeechModel { get; set; } = "gpt-4o-mini-tts";
    public string DefaultVoice { get; set; } = "alloy";
    public string OpenAiProject { get; set; } = string.Empty;
    public string ImageProviderApiKey { get; set; } = string.Empty;
    public string VoiceProviderApiKey { get; set; } = string.Empty;
    public string VoiceProviderName { get; set; } = "OpenAI";
}
