using Microsoft.Extensions.Hosting;

namespace PayrollIntegrationDashboard.Services;

public class BackgroundSyncService : BackgroundService
{
    private readonly IServiceProvider _provider;

    public BackgroundSyncService(IServiceProvider provider)
    {
        _provider = provider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _provider.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IntegrationService>();

            await svc.ImportTimeEntriesAsync();
            await svc.ExportPayrollAsync();

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
