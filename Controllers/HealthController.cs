using Microsoft.AspNetCore.Mvc;

namespace PayrollIntegrationDashboard.Controllers;

[Route("health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Healthy");
}
