using HotChocolate;
using PayrollIntegrationDashboard.Services;

namespace PayrollIntegrationDashboard.GraphQL;

public class Mutation
{
    public Task<ImportResult> ImportTime([Service] IntegrationService service) =>
        service.ImportTimeEntriesAsync();

    public Task<ExportResult> ExportPayroll([Service] IntegrationService service) =>
        service.ExportPayrollAsync();
}
