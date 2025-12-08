namespace PayrollIntegrationDashboard.Models;

public class IntegrationRunLog
{
    public int Id { get; set; }
    public DateTime RunAt { get; set; }
    public string Operation { get; set; } = string.Empty; // Import / Export / Retry / ManualAdd
    public bool Success { get; set; }
    public string? Message { get; set; }
}
