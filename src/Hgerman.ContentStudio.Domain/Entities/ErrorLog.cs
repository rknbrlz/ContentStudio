using Hgerman.ContentStudio.Domain.Common;

namespace Hgerman.ContentStudio.Domain.Entities;

public class ErrorLog : BaseEntity
{
    public int ErrorLogId { get; set; }
    public int? VideoJobId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? ErrorType { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public string? RawResponse { get; set; }

    public VideoJob? VideoJob { get; set; }
}
