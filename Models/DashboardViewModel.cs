using PayrollIntegrationDashboard.Models;

namespace PayrollIntegrationDashboard.Models;

public class DashboardViewModel
{
    public List<PayrollSummary> Summaries { get; set; } = new();
    public List<IntegrationRunLog> Logs { get; set; } = new();

    public int TotalEmployees { get; set; }
    public double TotalHours { get; set; }
    public decimal TotalPayout { get; set; }
    public int FailedExportsQueued { get; set; }

    public List<string> HoursLabels { get; set; } = new();
    public List<double> HoursValues { get; set; } = new();

    public DateTime? LastImport { get; set; }
    public DateTime? LastExport { get; set; }
    public int FailedExportsLast24h { get; set; }

    public string HealthStatus { get; set; } = "";
    public string HealthDescription { get; set; } = "";

    // Multi-tenant / customer selector
    public List<string> Customers { get; set; } = new();
    public string? SelectedCustomer { get; set; }

    // Schedule preview
    public string ImportScheduleDescription { get; set; } = "";
    public string ExportScheduleDescription { get; set; } = "";
    public DateTime? NextImportRun { get; set; }
    public DateTime? NextExportRun { get; set; }

    // Data quality summary
    public int? LastImportInvalidCount { get; set; }

    // Weather integration
    public string? WeatherLocation { get; set; }
    public double? WeatherTemperature { get; set; }
    public string? WeatherSummary { get; set; }
}
