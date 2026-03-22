using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Entities;

namespace Hgerman.ContentStudio.Infrastructure.Services;

public class ImagePromptService : IImagePromptService
{
    public Task<string> GenerateScenePromptAsync(
        VideoJob job,
        VideoScene scene,
        CancellationToken cancellationToken = default)
    {
        var spokenText = !string.IsNullOrWhiteSpace(scene.VoiceText)
            ? scene.VoiceText
            : scene.SceneText;

        var prompt = $"""
Create a cinematic vertical 9:16 realistic study scene for a short-form motivational video.

Scene mood:
- warm desk lamp lighting
- evening study atmosphere
- realistic student emotion
- focused, serious, relatable
- social-media friendly composition
- clean background
- high contrast subject focus

Visual direction:
- student studying at desk
- book, notebook, pen, lamp
- realistic skin
- realistic hands
- natural composition
- no extra fingers
- no distorted hands
- no duplicated objects

Critical restrictions:
- NO TEXT
- NO WORDS
- NO LETTERS
- NO TYPOGRAPHY
- NO CAPTIONS
- NO SUBTITLES
- NO QUOTES
- NO WATERMARK
- NO UI ELEMENTS
- NO POSTER LOOK

Absolutely no typography of any kind.
If any text appears, the image is invalid.

Do not render any written message inside the image.

Use this only as emotional context, not as visible text:
{spokenText}
""";

        return Task.FromResult(prompt);
    }
}