using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PayrollIntegrationDashboard.Data;
using PayrollIntegrationDashboard.Models;

namespace PayrollIntegrationDashboard.Services;

public record ImportResult(int Imported, int Invalid);
public record ExportResult(int ExportedCount, int FailedQueued, bool Success);

public class IntegrationService
{
    private readonly AppDbContext _db;
    private readonly ValidationService _validator;
    private readonly HttpClient _http;
    private readonly ILogger<IntegrationService> _logger;

    // Simple hourly rate table (could come from DB in a real system)
    private readonly Dictionary<int, decimal> _employeeRates = new()
    {
        { 1, 250m },
        { 2, 260m },
        { 3, 275m },
        { 4, 280m },
        { 5, 290m }
        // others will use default 280
    };

    public IntegrationService(
        AppDbContext db,
        ValidationService validator,
        ILogger<IntegrationService> logger)
    {
        _db = db;
        _validator = validator;
        _logger = logger;
        _http = new HttpClient();
    }

    // --------------------------------------------------------------------
    // DASHBOARD MODEL
    // --------------------------------------------------------------------

    public async Task<DashboardViewModel> GetDashboardAsync()
    {
        var summaries = await GetCurrentPayrollSummaryAsync();
        var logs = await GetLogsAsync();

        // Last 7 days for chart
        var from = DateTime.Today.AddDays(-6);
        var daily = await _db.TimeEntries
            .Where(t => t.Date >= from)
            .GroupBy(t => t.Date.Date)
            .Select(g => new { Date = g.Key, Hours = g.Sum(x => x.Hours) })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var labels = new List<string>();
        var values = new List<double>();

        for (int i = 0; i < 7; i++)
        {
            var d = from.AddDays(i).Date;
            labels.Add(d.ToString("dd MMM"));
            values.Add(daily.FirstOrDefault(x => x.Date == d)?.Hours ?? 0);
        }

        // Integration health
        var utcNow = DateTime.UtcNow;
        var lastImport = logs
            .Where(l => l.Operation == "Import")
            .OrderByDescending(l => l.RunAt)
            .FirstOrDefault()?.RunAt;

        var lastExport = logs
            .Where(l => l.Operation == "Export")
            .OrderByDescending(l => l.RunAt)
            .FirstOrDefault()?.RunAt;

        var failedLast24h = logs.Count(l => !l.Success && l.RunAt >= utcNow.AddDays(-1));

        var (status, description) = BuildHealthStatus(lastImport, lastExport, failedLast24h);

        return new DashboardViewModel
        {
            Summaries = summaries,
            Logs = logs,
            TotalEmployees = summaries.Count,
            TotalHours = summaries.Sum(s => s.TotalHours),
            TotalPayout = summaries.Sum(s => s.TotalPay),
            FailedExportsQueued = await _db.FailedExports.CountAsync(),
            HoursLabels = labels,
            HoursValues = values,
            LastImport = lastImport,
            LastExport = lastExport,
            FailedExportsLast24h = failedLast24h,
            HealthStatus = status,
            HealthDescription = description
        };
    }

    private static (string status, string description) BuildHealthStatus(
        DateTime? lastImport,
        DateTime? lastExport,
        int failedLast24h)
    {
        if (lastImport == null && lastExport == null)
        {
            return ("No data yet",
                "Run your first import to start monitoring integrations.");
        }

        var now = DateTime.UtcNow;
        bool recentImport = lastImport != null && lastImport > now.AddHours(-2);
        bool recentExport = lastExport != null && lastExport > now.AddHours(-2);

        if (failedLast24h == 0 && recentImport && recentExport)
        {
            return ("Healthy",
                "Imports and exports are running as expected.");
        }

        if (failedLast24h > 0 && (recentImport || recentExport))
        {
            return ("Degraded",
                "Some operations have failed recently, but data is still flowing.");
        }

        return ("Attention needed",
            "No recent successful runs. Investigate the integration log.");
    }

    // --------------------------------------------------------------------
    // IMPORT
    // --------------------------------------------------------------------

    public async Task<ImportResult> ImportTimeEntriesAsync()
    {
        try
        {
            var external = await FetchExternalTimeDataAsync();
            int imported = 0, invalid = 0;

            foreach (var entry in external)
            {
                var errors = _validator.Validate(entry);
                if (errors.Any())
                {
                    invalid++;
                    _logger.LogWarning("Invalid entry for employee {Id}: {Errors}",
                        entry.EmployeeId, string.Join(", ", errors));
                    continue;
                }

                _db.TimeEntries.Add(entry);
                imported++;
            }

            await _db.SaveChangesAsync();
            await AddLogAsync("Import", true, $"Imported {imported} entries, {invalid} invalid.");
            return new ImportResult(imported, invalid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            await AddLogAsync("Import", false, ex.Message);
            return new ImportResult(0, 0);
        }
    }

    private async Task<List<TimeEntry>> FetchExternalTimeDataAsync()
    {
        // Dummy REST API – simulates HR/time system
        var response = await _http.GetFromJsonAsync<ApiUserResponse>("https://dummyjson.com/users");

        var date = DateTime.Today;   // <-- Dagens dato, ikke i går
        return response?.Users?
            .Take(30)
            .Select(u => new TimeEntry
            {
                EmployeeId = u.Id,
                Date = date,
                Hours = (u.Id % 5) + 5,
                Source = "API"
            }).ToList() ?? new List<TimeEntry>();
    }

    private class ApiUserResponse
    {
        public List<ApiUser> Users { get; set; } = new();
    }

    private class ApiUser
    {
        public int Id { get; set; }
    }

    // --------------------------------------------------------------------
    // SUMMARY (includes money)
    // --------------------------------------------------------------------

    public async Task<List<PayrollSummary>> GetCurrentPayrollSummaryAsync()
    {
        var now = DateTime.Now;
        var period = $"{now:yyyy-MM}";

        // First: let EF / SQL do grouping + sum, then materialize
        var grouped = await _db.TimeEntries
            .GroupBy(t => t.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                TotalHours = g.Sum(x => x.Hours)
            })
            .ToListAsync();

        // Second: in memory, apply hourly rates and compute pay
        var summaries = grouped.Select(g =>
        {
            var rate = _employeeRates.TryGetValue(g.EmployeeId, out var r) ? r : 280m;
            return new PayrollSummary
            {
                EmployeeId = g.EmployeeId,
                TotalHours = g.TotalHours,
                Period = period,
                HourlyRate = rate,
                TotalPay = rate * (decimal)g.TotalHours
            };
        }).ToList();

        return summaries;
    }

    // --------------------------------------------------------------------
    // EMPLOYEE METRICS (for inspector)
    // --------------------------------------------------------------------

    public async Task<EmployeeMetricsViewModel?> GetEmployeeMetricsAsync(int employeeId)
    {
        var summaries = await GetCurrentPayrollSummaryAsync();
        var summary = summaries.FirstOrDefault(s => s.EmployeeId == employeeId);
        if (summary == null)
            return null;

        const decimal taxRate = 0.32m;        // 32% tax assumption
        const decimal commissionRate = 0.05m; // 5% company cut

        var gross = summary.TotalPay;
        var tax = gross * taxRate;
        var commission = gross * commissionRate;
        var net = gross - tax - commission;

        // Activity snapshot based on time entries
        var entries = await _db.TimeEntries
            .Where(t => t.EmployeeId == employeeId)
            .OrderBy(t => t.Date)
            .ToListAsync();

        DateTime? first = entries.FirstOrDefault()?.Date;
        DateTime? last = entries.LastOrDefault()?.Date;

        double avgPerDay = 0;
        string trendLabel = "Stable";

        if (entries.Any())
        {
            var spanDays = (entries.Max(e => e.Date) - entries.Min(e => e.Date)).TotalDays + 1;
            if (spanDays > 0)
            {
                avgPerDay = entries.Sum(e => e.Hours) / spanDays;
            }

            // crude trend: last 7 days vs previous 7 days
            var today = DateTime.Today;
            var last7Start = today.AddDays(-6);
            var prev7Start = today.AddDays(-13);
            var prev7End = today.AddDays(-7);

            var last7 = entries.Where(e => e.Date >= last7Start && e.Date <= today).Sum(e => e.Hours);
            var prev7 = entries.Where(e => e.Date >= prev7Start && e.Date <= prev7End).Sum(e => e.Hours);

            if (last7 > prev7 * 1.1)
                trendLabel = "Increasing";
            else if (last7 < prev7 * 0.9)
                trendLabel = "Decreasing";
        }

        return new EmployeeMetricsViewModel
        {
            EmployeeId = summary.EmployeeId,
            Period = summary.Period,
            TotalHours = summary.TotalHours,
            HourlyRate = summary.HourlyRate,
            GrossPay = gross,
            TaxAmount = tax,
            CommissionAmount = commission,
            NetPay = net,
            TaxRatePercent = taxRate * 100,
            CommissionRatePercent = commissionRate * 100,
            AverageHoursPerDay = avgPerDay,
            FirstEntryDate = first,
            LastEntryDate = last,
            TrendLabel = trendLabel
        };
    }

    // --------------------------------------------------------------------
    // EXPORT
    // --------------------------------------------------------------------

    public async Task<ExportResult> ExportPayrollAsync()
    {
        try
        {
            var summaries = await GetCurrentPayrollSummaryAsync();
            await Task.Delay(300); // simulate external payroll API call

            if (!summaries.Any())
            {
                await SaveFailedExportAsync("No data to export.");
                await AddLogAsync("Export", false, "No data to export – queued.");
                return new ExportResult(0, 1, false);
            }

            await AddLogAsync("Export", true, $"Exported {summaries.Count} employees.");
            return new ExportResult(summaries.Count, 0, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            await SaveFailedExportAsync(ex.Message);
            await AddLogAsync("Export", false, ex.Message);
            return new ExportResult(0, 1, false);
        }
    }

    private async Task SaveFailedExportAsync(string payload)
    {
        _db.FailedExports.Add(new FailedExport
        {
            Payload = payload,
            FailedAt = DateTime.UtcNow,
            RetryCount = 0
        });
        await _db.SaveChangesAsync();
    }

    public async Task<int> RetryFailedExportsAsync()
    {
        var failed = await _db.FailedExports.ToListAsync();
        int retried = 0;

        foreach (var exp in failed)
        {
            try
            {
                await Task.Delay(200); // simulate retry
                _db.FailedExports.Remove(exp);
                retried++;
                await AddLogAsync("Retry", true, $"Retried payload {exp.Id}");
            }
            catch (Exception ex)
            {
                exp.RetryCount++;
                _logger.LogError(ex, "Retry failed for FailedExport {Id}", exp.Id);
            }
        }

        await _db.SaveChangesAsync();
        return retried;
    }

    // --------------------------------------------------------------------
    // MANUAL ADD
    // --------------------------------------------------------------------

    public async Task ManualAddAsync(TimeEntry entry)
    {
        entry.Source = "Manual";
        var errors = _validator.Validate(entry);
        if (errors.Any())
            throw new InvalidOperationException(string.Join("; ", errors));

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync();
        await AddLogAsync("ManualAdd", true, $"Manual entry for employee {entry.EmployeeId}");
    }

    // --------------------------------------------------------------------
    // LOGS
    // --------------------------------------------------------------------

    public Task<List<IntegrationRunLog>> GetLogsAsync() =>
        _db.IntegrationRunLogs
           .OrderByDescending(l => l.RunAt)
           .Take(50)
           .ToListAsync();

    private async Task AddLogAsync(string op, bool success, string? msg)
    {
        _db.IntegrationRunLogs.Add(new IntegrationRunLog
        {
            RunAt = DateTime.UtcNow,
            Operation = op,
            Success = success,
            Message = msg
        });
        await _db.SaveChangesAsync();
    }
}
