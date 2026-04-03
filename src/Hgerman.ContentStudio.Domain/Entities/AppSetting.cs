using Hgerman.ContentStudio.Domain.Common;

namespace Hgerman.ContentStudio.Domain.Entities;

public class AppSetting : BaseEntity
{
    public int AppSettingId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
    public string SettingGroup { get; set; } = string.Empty;
    public bool IsEncrypted { get; set; }
}
