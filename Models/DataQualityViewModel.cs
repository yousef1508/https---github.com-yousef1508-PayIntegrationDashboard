namespace PayrollIntegrationDashboard.Models;

public class DataQualityViewModel
{
    public DateTime? LastImportAt { get; set; }

    public int? LastImportInvalidCount { get; set; }

    public List<string> ValidationRules { get; set; } = new();

    public string MappingDescription { get; set; } = "";
}
