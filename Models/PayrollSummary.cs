namespace PayrollIntegrationDashboard.Models;

public class PayrollSummary
{
    public int EmployeeId { get; set; }

    public double TotalHours { get; set; }

    public string Period { get; set; } = "";

    public decimal HourlyRate { get; set; }

    public decimal TotalPay { get; set; }

    public string CustomerName { get; set; } = "Demo customer";
}
