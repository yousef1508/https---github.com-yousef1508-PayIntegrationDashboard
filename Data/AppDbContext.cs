using Microsoft.EntityFrameworkCore;
using PayrollIntegrationDashboard.Models;

namespace PayrollIntegrationDashboard.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<IntegrationRunLog> IntegrationRunLogs => Set<IntegrationRunLog>();
    public DbSet<FailedExport> FailedExports => Set<FailedExport>();
}
