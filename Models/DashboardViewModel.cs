namespace PayrollIntegrationDashboard.Models;

public class DashboardViewModel
{
    public List<PayrollSummary> Summaries { get; set; } = new();
    public List<IntegrationRunLog> Logs { get; set; } = new();

    // KPI numbers
    public int TotalEmployees { get; set; }
    public double TotalHours { get; set; }
    public decimal TotalPayout { get; set; }

    // Queued / failed exports
    public int FailedExportsQueued { get; set; }

    // Chart
    public List<string> HoursLabels { get; set; } = new();
    public List<double> HoursValues { get; set; } = new();

    // Integration health
    public DateTime? LastImport { get; set; }
    public DateTime? LastExport { get; set; }
    public int FailedExportsLast24h { get; set; }
    public string HealthStatus { get; set; } = string.Empty;
    public string HealthDescription { get; set; } = string.Empty;
}
