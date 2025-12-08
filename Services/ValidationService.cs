using PayrollIntegrationDashboard.Models;

namespace PayrollIntegrationDashboard.Services;

public class ValidationService
{
    public List<string> Validate(TimeEntry entry)
    {
        var errors = new List<string>();

        if (entry.EmployeeId <= 0)
            errors.Add("EmployeeId is required.");

        if (entry.Hours < 0 || entry.Hours > 24)
            errors.Add("Hours must be between 0–24.");

        if (entry.Date > DateTime.Today)
            errors.Add("Date cannot be in the future.");

        return errors;
    }
}
