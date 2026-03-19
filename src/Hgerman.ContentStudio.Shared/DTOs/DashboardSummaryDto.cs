namespace Hgerman.ContentStudio.Shared.DTOs;

public class DashboardSummaryDto
{
    public int TotalJobs { get; set; }
    public int CompletedJobs { get; set; }
    public int FailedJobs { get; set; }
    public int QueuedJobs { get; set; }
}
