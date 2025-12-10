namespace PayrollIntegrationDashboard.Models;

public class ExportPreviewViewModel
{
    public string? CustomerName { get; set; }

    public List<PayrollSummary> Summaries { get; set; } = new();

    public int TotalEmployees => Summaries.Count;

    public double TotalHours => Summaries.Sum(s => s.TotalHours);

    public decimal TotalPayout => Summaries.Sum(s => s.TotalPay);

    public string BatchId { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
