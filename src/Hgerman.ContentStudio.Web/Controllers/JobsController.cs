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
        var lastJobs = await _db.VideoJobs
            .OrderByDescending(x => x.VideoJobId)
            .Take(5)
            .Select(x => new
            {
                x.VideoJobId,
                x.Title,
                x.Status,
                x.CurrentStep,
                x.CreatedDate
            })
            .ToListAsync(cancellationToken);

        var processed = await _jobProcessor.ProcessNextPendingJobAsync(cancellationToken);

        return Json(new
        {
            processed,
            jobs = lastJobs
        });
    }
}