using Hgerman.ContentStudio.Domain.Common;

namespace Hgerman.ContentStudio.Domain.Entities;

public class Project : BaseEntity
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<VideoJob> VideoJobs { get; set; } = new List<VideoJob>();
}
