using HotChocolate;
using HotChocolate.Data;
using PayrollIntegrationDashboard.Data;
using PayrollIntegrationDashboard.Models;
using PayrollIntegrationDashboard.Services;

namespace PayrollIntegrationDashboard.GraphQL;

public class Query
{
    [UseFiltering]
    [UseSorting]
    public IQueryable<TimeEntry> GetTimeEntries([Service] AppDbContext db) =>
        db.TimeEntries;

    [UseFiltering]
    [UseSorting]
    public IQueryable<IntegrationRunLog> GetIntegrationLogs([Service] AppDbContext db) =>
        db.IntegrationRunLogs;

    public Task<List<PayrollSummary>> GetPayrollSummaries([Service] IntegrationService service) =>
        service.GetCurrentPayrollSummaryAsync();
}
