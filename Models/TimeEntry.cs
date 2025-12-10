namespace PayrollIntegrationDashboard.Models;

public class TimeEntry
{
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    public DateTime Date { get; set; }

    public double Hours { get; set; }

    // "API", "Manual", etc.
    public string Source { get; set; } = "API";

    // Multi-tenant feel: which customer/company this entry belongs to
    public string CustomerName { get; set; } = "Demo customer";
}
