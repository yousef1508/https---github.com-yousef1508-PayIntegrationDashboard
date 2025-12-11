using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PayrollIntegrationDashboard.Data;
using PayrollIntegrationDashboard.Models;

namespace PayrollIntegrationDashboard.Controllers
{
    public class EmployeeController : Controller
    {
        private readonly AppDbContext _db;

        public EmployeeController(AppDbContext db)
        {
            _db = db;
        }

        // GET: /Employee/Details/123?month=2025-12
        public async Task<IActionResult> Details(int id, string? month)
        {
            var employee = await _db.Employees.FindAsync(id);
            if (employee == null)
            {
                // If employee record doesn't exist yet, create a basic one so it can be edited.
                employee = new Employee
                {
                    Id = id,
                    HourlyRate = 280m,
                    ContractType = "Hourly"
                };
                _db.Employees.Add(employee);
                await _db.SaveChangesAsync();
            }

            var customers = await _db.Employees
                .Select(e => e.Customer!)
                .Where(c => c != null && c != "")
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            // Determine month
            DateTime monthStart;
            if (!string.IsNullOrWhiteSpace(month) &&
                DateTime.TryParseExact(month + "-01", "yyyy-MM-dd",
                    CultureInfo.InvariantCulture, DateTimeStyles.None,
                    out var parsed))
            {
                monthStart = parsed.Date;
            }
            else
            {
                var today = DateTime.Today;
                monthStart = new DateTime(today.Year, today.Month, 1);
                month = monthStart.ToString("yyyy-MM");
            }

            var monthEnd = monthStart.AddMonths(1);

            // Aggregate hours per day
            var dayHours = await _db.TimeEntries
                .Where(t => t.EmployeeId == id &&
                            t.Date >= monthStart &&
                            t.Date < monthEnd)
                .GroupBy(t => t.Date.Date)
                .Select(g => new EmployeeDetailsViewModel.DayHours
                {
                    Date = g.Key,
                    TotalHours = g.Sum(x => x.Hours)
                })
                .ToListAsync();

            var vm = new EmployeeDetailsViewModel
            {
                Employee = employee,
                SelectedMonth = month!,
                CalendarDays = dayHours,
                Customers = customers
            };

            return View(vm);
        }

        // POST: /Employee/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(EmployeeDetailsViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                // Reload customers and calendar for redisplay
                var customers = await _db.Employees
                    .Select(e => e.Customer!)
                    .Where(c => c != null && c != "")
                    .Distinct()
                    .OrderBy(c => c)
                    .ToListAsync();

                vm.Customers = customers;
                vm.CalendarDays ??= new System.Collections.Generic.List<EmployeeDetailsViewModel.DayHours>();
                return View("Details", vm);
            }

            var employee = await _db.Employees.FindAsync(vm.Employee.Id);
            if (employee == null)
            {
                return NotFound();
            }

            employee.Name = vm.Employee.Name;
            employee.Customer = vm.Employee.Customer;
            employee.HourlyRate = vm.Employee.HourlyRate;
            employee.ContractType = vm.Employee.ContractType;
            employee.HireDate = vm.Employee.HireDate;
            employee.EndDate = vm.Employee.EndDate;

            await _db.SaveChangesAsync();

            // After saving, redirect back to same month
            return RedirectToAction(nameof(Details), new { id = employee.Id, month = vm.SelectedMonth });
        }
    }
}
