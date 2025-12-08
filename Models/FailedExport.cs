namespace PayrollIntegrationDashboard.Models;

public class FailedExport
{
    public int Id { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime FailedAt { get; set; }
    public int RetryCount { get; set; }
}
