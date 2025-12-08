using PayrollIntegrationDashboard.Data;
using PayrollIntegrationDashboard.Models;

namespace PayrollIntegrationDashboard.GraphQL;

public class Query
{
    public IQueryable<TimeEntry> GetTimeEntries(AppDbContext db) => db.TimeEntries;
    public IQueryable<IntegrationRunLog> GetLogs(AppDbContext db) => db.IntegrationRunLogs;
}
