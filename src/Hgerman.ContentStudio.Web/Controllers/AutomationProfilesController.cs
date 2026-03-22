using Hgerman.ContentStudio.Application.Interfaces;
using Hgerman.ContentStudio.Domain.Enums;
using Hgerman.ContentStudio.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Hgerman.ContentStudio.Web.Controllers;

public class AutomationProfilesController : Controller
{
    private readonly IAutomationProfileService _automationProfileService;
    private readonly IAutomationService _automationService;

    public AutomationProfilesController(
        IAutomationProfileService automationProfileService,
        IAutomationService automationService)
    {
        _automationProfileService = automationProfileService;
        _automationService = automationService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _automationProfileService.GetListAsync(cancellationToken);
        return View(items);
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _automationProfileService.GetDashboardAsync(id, cancellationToken);
        if (model == null)
            return NotFound();

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        PopulateLookups();
        return View("Edit", new AutomationProfileEditViewModel
        {
            IsActive = true,
            ProjectId = 1,
            LanguageCode = "en",
            PlatformType = (int)PlatformType.YouTubeShorts,
            ToneType = (int)ToneType.Inspirational,
            DurationTargetSec = 45,
            AspectRatio = (int)AspectRatioType.Vertical916,
            SubtitleEnabled = true,
            ThumbnailEnabled = true,
            DailyVideoLimit = 3,
            PreferredHoursCsv = "09,14,20",
            GrowthMode = "balanced",
            TitleTestVariants = 3,
            MinSuccessScore = 55
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AutomationProfileEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            PopulateLookups();
            return View("Edit", model);
        }

        var id = await _automationProfileService.CreateAsync(model.ToRequest(), cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var entity = await _automationProfileService.GetByIdAsync(id, cancellationToken);
        if (entity == null)
            return NotFound();

        PopulateLookups();
        return View(AutomationProfileEditViewModel.FromEntity(entity));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AutomationProfileEditViewModel model, CancellationToken cancellationToken)
    {
        if (!model.AutomationProfileId.HasValue)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            PopulateLookups();
            return View(model);
        }

        await _automationProfileService.UpdateAsync(model.AutomationProfileId.Value, model.ToRequest(), cancellationToken);
        return RedirectToAction(nameof(Details), new { id = model.AutomationProfileId.Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id, CancellationToken cancellationToken)
    {
        await _automationProfileService.ToggleActiveAsync(id, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunNow(int id, CancellationToken cancellationToken)
    {
        await _automationService.RunProfileNowAsync(id, cancellationToken);
        return RedirectToAction(nameof(Details), new { id });
    }

    private void PopulateLookups()
    {
        ViewBag.PlatformTypes = Enum.GetValues<PlatformType>()
            .Select(x => new SelectListItem(x.ToString(), ((int)x).ToString()))
            .ToList();

        ViewBag.ToneTypes = Enum.GetValues<ToneType>()
            .Select(x => new SelectListItem(x.ToString(), ((int)x).ToString()))
            .ToList();

        ViewBag.AspectRatios = Enum.GetValues<AspectRatioType>()
            .Select(x => new SelectListItem(x.ToString(), ((int)x).ToString()))
            .ToList();

        ViewBag.GrowthModes = new List<SelectListItem>
        {
            new("Safe", "safe"),
            new("Balanced", "balanced"),
            new("Aggressive", "aggressive")
        };
    }
}