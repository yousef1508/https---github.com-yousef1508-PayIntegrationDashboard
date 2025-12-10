using Microsoft.AspNetCore.Mvc;
using PayrollIntegrationDashboard.Models;
using PayrollIntegrationDashboard.Services;

namespace PayrollIntegrationDashboard.Controllers;

public class IntegrationController : Controller
{
    private readonly IntegrationService _integrationService;

    public IntegrationController(IntegrationService integrationService)
    {
        _integrationService = integrationService;
    }

    // Dashboard with optional customer filter
    [HttpGet]
    public async Task<IActionResult> Index(string? customer)
    {
        var model = await _integrationService.GetDashboardAsync(customer);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(string? customer)
    {
        await _integrationService.ImportTimeEntriesAsync();
        return RedirectToAction(nameof(Index), new { customer });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Export(string? customer)
    {
        await _integrationService.ExportPayrollAsync(customer);
        return RedirectToAction(nameof(Index), new { customer });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(string? customer)
    {
        await _integrationService.RetryFailedExportsAsync();
        return RedirectToAction(nameof(Index), new { customer });
    }

    // Manual entry (simple)
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TimeEntry entry)
    {
        if (!ModelState.IsValid)
            return View(entry);

        try
        {
            await _integrationService.ManualAddAsync(entry);
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(entry);
        }
    }

    // Logs view with filters
    [HttpGet]
    public async Task<IActionResult> Logs(string operation = "All", string status = "All", string range = "24h")
    {
        var logs = await _integrationService.GetLogsAsync();
        var now = DateTime.UtcNow;

        DateTime? since = range switch
        {
            "1h" => now.AddHours(-1),
            "24h" => now.AddDays(-1),
            "7d" => now.AddDays(-7),
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(operation) && operation != "All")
        {
            logs = logs.Where(l => l.Operation == operation).ToList();
        }

        if (status == "Success")
        {
            logs = logs.Where(l => l.Success).ToList();
        }
        else if (status == "Failed")
        {
            logs = logs.Where(l => !l.Success).ToList();
        }

        if (since.HasValue)
        {
            logs = logs.Where(l => l.RunAt >= since.Value).ToList();
        }

        var vm = new LogsViewModel
        {
            Logs = logs,
            SelectedOperation = operation,
            SelectedStatus = status,
            SelectedRange = range
        };

        return View(vm);
    }

    // Export preview / reconciliation
    [HttpGet]
    public async Task<IActionResult> ExportPreview(string? customer)
    {
        var summaries = await _integrationService.GetCurrentPayrollSummaryAsync(customer);
        var vm = new ExportPreviewViewModel
        {
            CustomerName = string.IsNullOrWhiteSpace(customer) || customer == "All" ? null : customer,
            Summaries = summaries
        };
        return View(vm);
    }

    // Data quality & mapping overview
    [HttpGet]
    public async Task<IActionResult> DataQuality()
    {
        var vm = await _integrationService.GetDataQualityAsync();
        return View(vm);
    }

    // Existing GraphQL tools page is already wired as GraphQLTools action
    [HttpGet]
    public IActionResult GraphQLTools()
    {
        return View();
    }
}
