using System;
using System.Collections.Generic;
using System.Linq;

namespace PayrollIntegrationDashboard.Models
{
    public class EmployeeDetailsViewModel
    {
        public Employee Employee { get; set; } = null!;

        /// <summary>
        /// Month in "yyyy-MM" format (e.g. "2025-12").
        /// </summary>
        public string SelectedMonth { get; set; } = "";

        /// <summary>
        /// Hours per day in the selected month.
        /// </summary>
        public List<DayHours> CalendarDays { get; set; } = new();

        /// <summary>
        /// Available customers to select from (for assigning employee to a customer).
        /// </summary>
        public IEnumerable<string> Customers { get; set; } = Array.Empty<string>();

        public double MonthTotalHours => CalendarDays.Sum(d => d.TotalHours);

        public decimal MonthGrossPay => (decimal)MonthTotalHours * Employee.HourlyRate;

        public class DayHours
        {
            public DateTime Date { get; set; }
            public double TotalHours { get; set; }
        }
    }
}
