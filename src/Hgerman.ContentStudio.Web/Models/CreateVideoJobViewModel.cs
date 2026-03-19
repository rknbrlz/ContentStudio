using System.ComponentModel.DataAnnotations;
using Hgerman.ContentStudio.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Hgerman.ContentStudio.Web.Models;

public class CreateVideoJobViewModel
{
    [Required]
    public int ProjectId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Topic { get; set; }

    public string? SourcePrompt { get; set; }

    [Required]
    [StringLength(10)]
    public string LanguageCode { get; set; } = "en";

    [Required]
    public PlatformType PlatformType { get; set; } = PlatformType.YouTubeShorts;

    [Required]
    public ToneType ToneType { get; set; } = ToneType.Inspirational;

    [Range(15, 120)]
    public int DurationTargetSec { get; set; } = 45;

    [Required]
    public AspectRatioType AspectRatio { get; set; } = AspectRatioType.Vertical916;

    [StringLength(50)]
    public string? VoiceProvider { get; set; }

    [StringLength(100)]
    public string? VoiceName { get; set; }

    public bool SubtitleEnabled { get; set; } = true;
    public bool ThumbnailEnabled { get; set; } = true;

    public InputModeType InputMode { get; set; } = InputModeType.AiOnly;

    public IFormFile? SourceImage { get; set; }
}