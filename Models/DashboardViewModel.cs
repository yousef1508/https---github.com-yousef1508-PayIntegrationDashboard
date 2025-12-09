namespace PayrollIntegrationDashboard.Models;

public class DashboardViewModel
{
    public List<PayrollSummary> Summaries { get; set; } = new();
    public List<IntegrationRunLog> Logs { get; set; } = new();

    public int TotalEmployees { get; set; }
    public double TotalHours { get; set; }
    public int FailedExports { get; set; }
    public DateTime? LastRun { get; set; }

    public List<string> HoursLabels { get; set; } = new();
    public List<double> HoursValues { get; set; } = new();

    // New: total payout KPI
    public decimal TotalPayout { get; set; }
}
