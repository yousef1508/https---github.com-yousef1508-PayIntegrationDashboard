namespace PayrollIntegrationDashboard.Models;

public class PayrollSummary
{
    public int EmployeeId { get; set; }
    public double TotalHours { get; set; }
    public string Period { get; set; } = string.Empty;
}
