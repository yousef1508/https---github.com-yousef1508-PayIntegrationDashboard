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

    // DASHBOARD MODEL ----------------------------------------------------

    public async Task<DashboardViewModel> GetDashboardAsync()
    {
        var summaries = await GetCurrentPayrollSummaryAsync();
        var logs = await GetLogsAsync();

        // last 7 days hours for chart
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

        return new DashboardViewModel
        {
            Summaries = summaries,
            Logs = logs,
            TotalEmployees = summaries.Count,
            TotalHours = summaries.Sum(s => s.TotalHours),
            FailedExports = await _db.FailedExports.CountAsync(),
            LastRun = logs.FirstOrDefault()?.RunAt,
            HoursLabels = labels,
            HoursValues = values
        };
    }

    // IMPORT -------------------------------------------------------------

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

        var today = DateTime.Today;
        return response?.Users?
            .Take(30)
            .Select(u => new TimeEntry
            {
                EmployeeId = u.Id,
                Date = today.AddDays(-1),
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

    // SUMMARY ------------------------------------------------------------

    public async Task<List<PayrollSummary>> GetCurrentPayrollSummaryAsync()
    {
        var now = DateTime.Now;
        var period = $"{now:yyyy-MM}";

        return await _db.TimeEntries
            .GroupBy(t => t.EmployeeId)
            .Select(g => new PayrollSummary
            {
                EmployeeId = g.Key,
                TotalHours = g.Sum(x => x.Hours),
                Period = period
            })
            .ToListAsync();
    }

    // EXPORT -------------------------------------------------------------

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

    // MANUAL ADD ---------------------------------------------------------

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

    // LOGS ---------------------------------------------------------------

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
