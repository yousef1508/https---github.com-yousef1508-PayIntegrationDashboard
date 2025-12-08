namespace PayrollIntegrationDashboard.Models;

public class TimeEntry
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime Date { get; set; }
    public double Hours { get; set; }
    public string Source { get; set; } = "API"; // API or Manual
}
