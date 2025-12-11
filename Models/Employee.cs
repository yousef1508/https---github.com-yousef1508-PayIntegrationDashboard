using System;

namespace PayrollIntegrationDashboard.Models
{
    public class Employee
    {
        // Same ID as used in TimeEntry.EmployeeId
        public int Id { get; set; }

        public string? Name { get; set; }

        /// <summary>
        /// Which customer/tenant this employee belongs to
        /// (e.g. "Acme AS", "Demo customer").
        /// </summary>
        public string? Customer { get; set; }

        /// <summary>
        /// Hourly rate in NOK.
        /// </summary>
        public decimal HourlyRate { get; set; }

        public string? ContractType { get; set; }  // e.g. "Full-time", "Part-time", "Hourly"

        public DateTime? HireDate { get; set; }

        public DateTime? EndDate { get; set; }
    }
}
