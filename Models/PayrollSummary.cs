namespace PayrollIntegrationDashboard.Models;

public class PayrollSummary
{
    public int EmployeeId { get; set; }
    public double TotalHours { get; set; }
    public string Period { get; set; } = string.Empty;

    // New: money-related fields
    public decimal HourlyRate { get; set; }   // NOK per hour
    public decimal TotalPay { get; set; }     // TotalHours * HourlyRate
}
