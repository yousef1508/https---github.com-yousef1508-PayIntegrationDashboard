using Microsoft.AspNetCore.Mvc;
using PayrollIntegrationDashboard.Services;

namespace PayrollIntegrationDashboard.Controllers;

[ApiController]
[Route("api/integration")]
public class ApiController : ControllerBase
{
    private readonly IntegrationService _service;

    public ApiController(IntegrationService service)
    {
        _service = service;
    }

    [HttpPost("import")]
    public async Task<ActionResult<ImportResult>> Import()
    {
        var result = await _service.ImportTimeEntriesAsync();
        return Ok(result);
    }

    [HttpPost("export")]
    public async Task<ActionResult<ExportResult>> Export()
    {
        var result = await _service.ExportPayrollAsync();
        return Ok(result);
    }

    [HttpPost("retry")]
    public async Task<ActionResult<int>> Retry()
    {
        var retried = await _service.RetryFailedExportsAsync();
        return Ok(retried);
    }
}
