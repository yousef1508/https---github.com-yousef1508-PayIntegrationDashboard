using PayrollIntegrationDashboard.Data;
using PayrollIntegrationDashboard.Models;

namespace PayrollIntegrationDashboard.GraphQL;

public class Mutation
{
    public async Task<TimeEntry> AddTimeEntry(TimeEntry entry, AppDbContext db)
    {
        db.TimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }
}
