namespace Hgerman.ContentStudio.Domain.Enums;

public enum VideoPipelineStep
{
    Draft = 1,
    ScriptGenerating = 2,
    ScriptReady = 3,
    ScenePlanning = 4,
    SceneReady = 5,
    ImagePromptGenerating = 6,
    ImageGenerating = 7,
    ImagesReady = 8,
    VoiceGenerating = 9,
    VoiceReady = 10,
    SubtitleGenerating = 11,
    SubtitleReady = 12,
    Rendering = 13,
    Completed = 14,
    Failed = 15
}
