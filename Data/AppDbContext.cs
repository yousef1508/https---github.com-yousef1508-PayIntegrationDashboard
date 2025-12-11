using Microsoft.EntityFrameworkCore;
using PayrollIntegrationDashboard.Models;

namespace PayrollIntegrationDashboard.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<TimeEntry> TimeEntries { get; set; } = null!;
        public DbSet<IntegrationRunLog> IntegrationRunLogs { get; set; } = null!;
        public DbSet<FailedExport> FailedExports { get; set; } = null!;
        public DbSet<Employee> Employees { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TimeEntry>()
                .HasIndex(t => new { t.EmployeeId, t.Date });

            modelBuilder.Entity<Employee>()
                .HasKey(e => e.Id);
        }
    }
}
