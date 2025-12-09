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

    // For displaying the assumptions
    public decimal TaxRatePercent { get; set; }
    public decimal CommissionRatePercent { get; set; }
}
