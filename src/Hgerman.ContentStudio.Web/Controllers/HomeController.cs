using Hgerman.ContentStudio.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Hgerman.ContentStudio.Web.Controllers;

public class HomeController : Controller
{
    private readonly IVideoJobService _videoJobService;

    public HomeController(IVideoJobService videoJobService)
    {
        _videoJobService = videoJobService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var summary = await _videoJobService.GetDashboardSummaryAsync(cancellationToken);
        return View(summary);
    }
}
