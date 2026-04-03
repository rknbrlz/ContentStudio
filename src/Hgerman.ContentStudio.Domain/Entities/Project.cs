using Hgerman.ContentStudio.Domain.Common;

namespace Hgerman.ContentStudio.Domain.Entities;

public class Project : BaseEntity
{
    public int ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<VideoJob> VideoJobs { get; set; } = new List<VideoJob>();
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
}