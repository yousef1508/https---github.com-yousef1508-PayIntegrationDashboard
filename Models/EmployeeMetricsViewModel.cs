namespace PayrollIntegrationDashboard.Models;

public class EmployeeMetricsViewModel
{
    public int EmployeeId { get; set; }
    public string Period { get; set; } = string.Empty;

    public double TotalHours { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal GrossPay { get; set; }

    public decimal TaxAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetPay { get; set; }

    public decimal TaxRatePercent { get; set; }
    public decimal CommissionRatePercent { get; set; }

    // Activity snapshot
    public double AverageHoursPerDay { get; set; }
    public DateTime? FirstEntryDate { get; set; }
    public DateTime? LastEntryDate { get; set; }
    public string TrendLabel { get; set; } = string.Empty;
}
