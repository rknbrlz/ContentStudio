namespace Hgerman.ContentStudio.Domain.Common;

public abstract class BaseEntity
{
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedDate { get; set; }
}
