namespace PayrollIntegrationDashboard.Models;

public class LogsViewModel
{
    public List<IntegrationRunLog> Logs { get; set; } = new();

    public string SelectedOperation { get; set; } = "All";

    public string SelectedStatus { get; set; } = "All";

    public string SelectedRange { get; set; } = "24h";
}
