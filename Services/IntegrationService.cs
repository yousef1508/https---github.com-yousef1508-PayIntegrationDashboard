using System.Net.Http.Json;
using System.Text.RegularExpressions;
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

    public async Task<DashboardViewModel> GetDashboardAsync(string? customerName)
    {
        var summaries = await GetCurrentPayrollSummaryAsync(customerName);
        var logs = await GetLogsAsync();

        // Last 7 days for chart (filtered by customer if provided)
        var from = DateTime.Today.AddDays(-6);
        var timeQuery = _db.TimeEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerName) && customerName != "All")
        {
            timeQuery = timeQuery.Where(t => t.CustomerName == customerName);
        }

        var daily = await timeQuery
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

        // Customers (multi-tenant feel)
        var customers = await _db.TimeEntries
            .Select(t => t.CustomerName)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();

        if (!customers.Contains("All"))
        {
            customers.Insert(0, "All");
        }

        var selectedCustomer = string.IsNullOrWhiteSpace(customerName) ? "All" : customerName;

        // Schedule preview
        var now = DateTime.UtcNow;
        var nextImport = GetNextImportRun(now);
        var nextExport = GetNextExportRun(now);

        // Data quality: parse last invalid count from last import log message
        int? lastInvalid = null;
        var lastImportLog = logs
            .Where(l => l.Operation == "Import")
            .OrderByDescending(l => l.RunAt)
            .FirstOrDefault();

        if (lastImportLog?.Message != null)
        {
            // Message format: "Imported X entries, Y invalid."
            var match = Regex.Match(lastImportLog.Message, @"Imported\s+\d+\s+entries,\s+(\d+)\s+invalid", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
            {
                lastInvalid = parsed;
            }
        }

        // Weather integration (Oslo / Skøyen)
        var weather = await FetchWeatherAsync();

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
            HealthDescription = description,

            Customers = customers,
            SelectedCustomer = selectedCustomer,

            ImportScheduleDescription = "Imports every 15 minutes between 07:00–18:00 (Mon–Fri).",
            ExportScheduleDescription = "Exports nightly at 01:00.",
            NextImportRun = nextImport,
            NextExportRun = nextExport,

            LastImportInvalidCount = lastInvalid,

            WeatherLocation = weather?.Location,
            WeatherTemperature = weather?.Temperature,
            WeatherSummary = weather?.Summary
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

    private static DateTime GetNextImportRun(DateTime nowUtc)
    {
        // 15 min interval between 07:00–18:00, Mon–Fri
        var local = nowUtc; // assume app runs in local time for demo
        var baseSlot = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute / 15 * 15, 0);
        var next = baseSlot.AddMinutes(15);

        if (next.Hour < 7)
            next = next.Date.AddHours(7);

        if (next.Hour >= 18)
            next = next.Date.AddDays(1).AddHours(7);

        return next;
    }

    private static DateTime GetNextExportRun(DateTime nowUtc)
    {
        var local = nowUtc;
        var todayRun = local.Date.AddHours(1); // 01:00
        if (local < todayRun) return todayRun;
        return todayRun.AddDays(1);
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
        // DummyJSON users = real, public REST API
        var response = await _http.GetFromJsonAsync<ApiUserResponse>("https://dummyjson.com/users");

        var date = DateTime.Today;
        return response?.Users?
            .Take(30)
            .Select(u => new TimeEntry
            {
                EmployeeId = u.Id,
                Date = date,
                Hours = (u.Id % 5) + 5,
                Source = "API",
                CustomerName = u.Company?.Name ?? "Demo customer"
            }).ToList() ?? new List<TimeEntry>();
    }

    private class ApiUserResponse
    {
        public List<ApiUser> Users { get; set; } = new();
    }

    private class ApiUser
    {
        public int Id { get; set; }
        public ApiCompany Company { get; set; } = new();
    }

    private class ApiCompany
    {
        public string Name { get; set; } = "Demo customer";
    }

    // --------------------------------------------------------------------
    // SUMMARY (includes money, filtered by customer)
    // --------------------------------------------------------------------

    public async Task<List<PayrollSummary>> GetCurrentPayrollSummaryAsync(string? customerName = null)
    {
        var now = DateTime.Now;
        var period = $"{now:yyyy-MM}";

        var query = _db.TimeEntries.AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerName) && customerName != "All")
        {
            query = query.Where(t => t.CustomerName == customerName);
        }

        var grouped = await query
            .GroupBy(t => t.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                TotalHours = g.Sum(x => x.Hours),
                CustomerName = g.Select(x => x.CustomerName).FirstOrDefault() ?? "Demo customer"
            })
            .ToListAsync();

        var summaries = grouped.Select(g =>
        {
            var rate = _employeeRates.TryGetValue(g.EmployeeId, out var r) ? r : 280m;
            return new PayrollSummary
            {
                EmployeeId = g.EmployeeId,
                TotalHours = g.TotalHours,
                Period = period,
                HourlyRate = rate,
                TotalPay = rate * (decimal)g.TotalHours,
                CustomerName = g.CustomerName
            };
        }).ToList();

        return summaries;
    }

    // --------------------------------------------------------------------
    // EMPLOYEE METRICS (for inspector + GraphQL)
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

            var today = DateTime.Today;
            var last7Start = today.AddDays(-6);
            var prev7Start = today.AddDays(-13);
            var prev7End = today.AddDays(-7);

            var last7 = entries.Where(e => e.Date >= last7Start && e.Date <= today).Sum(e => e.Hours);
            var prev7 = entries.Where(e => e.Date >= prev7Start && e.Date <= prev7End).Sum(e => e.Hours);

            if (last7 > prev7 * 1.1) trendLabel = "Increasing";
            else if (last7 < prev7 * 0.9) trendLabel = "Decreasing";
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

    public async Task<ExportResult> ExportPayrollAsync(string? customerName = null)
    {
        try
        {
            var summaries = await GetCurrentPayrollSummaryAsync(customerName);
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
    // DATA QUALITY
    // --------------------------------------------------------------------

    public async Task<DataQualityViewModel> GetDataQualityAsync()
    {
        var logs = await GetLogsAsync();

        var lastImport = logs
            .Where(l => l.Operation == "Import")
            .OrderByDescending(l => l.RunAt)
            .FirstOrDefault();

        int? lastInvalid = null;
        if (lastImport?.Message != null)
        {
            var match = Regex.Match(lastImport.Message, @"Imported\s+\d+\s+entries,\s+(\d+)\s+invalid", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var parsed))
            {
                lastInvalid = parsed;
            }
        }

        var rules = new List<string>
        {
            "EmployeeId must be a positive number.",
            "Hours must be greater than 0 and less than 24.",
            "Date must be within the current payroll period."
        };

        var mapping = "HR fields UserId, WorkDate and HoursWorked are mapped to payroll EmployeeId, Date and Hours.";

        return new DataQualityViewModel
        {
            LastImportAt = lastImport?.RunAt,
            LastImportInvalidCount = lastInvalid,
            ValidationRules = rules,
            MappingDescription = mapping
        };
    }

    // --------------------------------------------------------------------
    // WEATHER (Open-Meteo)
    // --------------------------------------------------------------------

    private class OpenMeteoResponse
    {
        public OpenMeteoCurrentWeather? current_weather { get; set; }
    }

    private class OpenMeteoCurrentWeather
    {
        public double temperature { get; set; }
        public int weathercode { get; set; }
    }

    private async Task<(string Location, double Temperature, string Summary)?> FetchWeatherAsync()
    {
        try
        {
            // Oslo / Skøyen
            var url = "https://api.open-meteo.com/v1/forecast?latitude=59.91&longitude=10.75&current_weather=true";
            var resp = await _http.GetFromJsonAsync<OpenMeteoResponse>(url);
            if (resp?.current_weather == null) return null;

            var temp = resp.current_weather.temperature;
            var summary = WeatherCodeToText(resp.current_weather.weathercode);

            return ("Oslo (Skøyen)", temp, summary);
        }
        catch
        {
            return null;
        }
    }

    private static string WeatherCodeToText(int code) => code switch
    {
        0 => "Clear sky",
        1 or 2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Foggy",
        51 or 53 or 55 => "Drizzle",
        61 or 63 or 65 => "Rain",
        71 or 73 or 75 => "Snow",
        95 => "Thunderstorm",
        _ => "Mixed conditions"
    };

    // --------------------------------------------------------------------
    // MANUAL ADD
    // --------------------------------------------------------------------

    public async Task ManualAddAsync(TimeEntry entry)
    {
        entry.Source = "Manual";
        if (string.IsNullOrWhiteSpace(entry.CustomerName))
            entry.CustomerName = "Manual entry";

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
