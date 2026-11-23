namespace NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;

public class WebhookRecommendation
{
    public int Id { get; set; }
    public int FieldId { get; set; }
    public int? DeviceId { get; set; }
    public int? FertilityZoneId { get; set; }
    public string Zone { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string Priority { get; set; } = "MEDIUM";
    public string Reason { get; set; } = string.Empty;
    public string Category { get; set; } = "IRRIGATION";
    public string Source { get; set; } = "ANALYSIS_ENGINE";
    public string? ExternalId { get; set; }
    public DateTime CreatedAt { get; set; }
}
