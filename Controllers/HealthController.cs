using Microsoft.AspNetCore.Mvc;
using PayrollIntegrationDashboard.Models;
using PayrollIntegrationDashboard.Services;

namespace PayrollIntegrationDashboard.Controllers
{
    public class HealthController : Controller
    {
        private readonly IntegrationService _integrationService;

        public HealthController(IntegrationService integrationService)
        {
            _integrationService = integrationService;
        }

        // Maps exactly to /health
        [HttpGet("/health")]
        public async Task<IActionResult> Index()
        {
            // Pass null = "All customers" for the dashboard health
            var dashboard = await _integrationService.GetDashboardAsync(null);

            var model = new HealthViewModel
            {
                Status = dashboard.HealthStatus,
                Description = dashboard.HealthDescription,
                FailedLast24h = dashboard.FailedExportsLast24h,
                LastImport = dashboard.LastImport,
                LastExport = dashboard.LastExport
            };

            return View(model);
        }
    }
}
