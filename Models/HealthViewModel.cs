namespace PayrollIntegrationDashboard.Models
{
    public class HealthViewModel
    {
        public string Status { get; set; } = "Unknown";
        public string? Description { get; set; }
        public int FailedLast24h { get; set; }
        public DateTime? LastImport { get; set; }
        public DateTime? LastExport { get; set; }
    }
}
