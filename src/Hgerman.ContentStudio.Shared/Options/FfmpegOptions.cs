namespace Hgerman.ContentStudio.Shared.Options;

public class FfmpegOptions
{
    public string ExecutablePath { get; set; } = @"C:\ffmpeg\bin\ffmpeg.exe";
    public string ProbeExecutablePath { get; set; } = @"C:\ffmpeg\bin\ffprobe.exe";
    public string WorkingDirectory { get; set; } = @"C:\HgermanContentStudioTemp";
    public int Width { get; set; } = 1080;
    public int Height { get; set; } = 1920;
    public int Fps { get; set; } = 30;
}