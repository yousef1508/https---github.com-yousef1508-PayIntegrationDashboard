using Microsoft.AspNetCore.Mvc;
using PayrollIntegrationDashboard.Models;
using PayrollIntegrationDashboard.Services;

namespace PayrollIntegrationDashboard.Controllers;

public class IntegrationController : Controller
{
    private readonly IntegrationService _service;

    public IntegrationController(IntegrationService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var vm = await _service.GetDashboardAsync();
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Import()
    {
        await _service.ImportTimeEntriesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Export()
    {
        await _service.ExportPayrollAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Retry()
    {
        await _service.RetryFailedExportsAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new TimeEntry { Date = DateTime.Today });
    }

    [HttpPost]
    public async Task<IActionResult> Create(TimeEntry entry)
    {
        if (!ModelState.IsValid)
            return View(entry);

        try
        {
            await _service.ManualAddAsync(entry);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(entry);
        }
    }
}
