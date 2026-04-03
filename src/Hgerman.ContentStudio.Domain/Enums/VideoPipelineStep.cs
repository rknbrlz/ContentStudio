namespace Hgerman.ContentStudio.Domain.Enums;

public enum VideoPipelineStep
{
    Draft = 1,
    Queued = 2,

    ScriptGenerating = 10,
    ScriptReady = 11,

    ScenePlanning = 20,
    SceneMetadataEnriching = 21,
    SceneReady = 22,

    ImagePromptGenerating = 30,
    ImageGenerating = 31,
    ImagesReady = 32,

    VoiceGenerating = 40,
    VoiceReady = 41,

    SubtitleGenerating = 50,
    SubtitleReady = 51,

    Rendering = 60,
    RenderingScenePrep = 61,
    RenderingSceneComposing = 62,
    RenderingFinalMux = 63,

    PublishDraftGenerating = 70,
    Completed = 90,

    Recovering = 95,
    Failed = 99
}