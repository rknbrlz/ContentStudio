using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hgerman.ContentStudio.Web.Controllers;

[Route("jobs")]
public class JobsController : Controller
{
    private readonly IJobProcessor _jobProcessor;
    private readonly ContentStudioDbContext _db;

    public JobsController(IJobProcessor jobProcessor, ContentStudioDbContext db)
    {
        _jobProcessor = jobProcessor;
        _db = db;
    }

    [HttpGet("run")]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        var recovered = await _jobProcessor.RecoverTimedOutJobsAsync(cancellationToken);
        var processed = await _jobProcessor.ProcessNextPendingJobAsync(cancellationToken);

        var lastJobs = await _db.VideoJobs
            .OrderByDescending(x => x.VideoJobId)
            .Take(10)
            .Select(x => new
            {
                x.VideoJobId,
                x.JobNo,
                x.Title,
                Status = x.Status.ToString(),
                CurrentStep = x.CurrentStep.ToString(),
                x.ProgressPercent,
                x.UpdatedDate
            })
            .ToListAsync(cancellationToken);

        return Json(new
        {
            recovered,
            processed,
            jobs = lastJobs
        });
    }
}