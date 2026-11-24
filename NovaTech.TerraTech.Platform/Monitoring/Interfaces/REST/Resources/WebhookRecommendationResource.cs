namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;

public record WebhookRecommendationResource(
    int Id,
    int FieldId,
    string Zone,
    string Action,
    int DurationMinutes,
    string Priority,
    string Reason,
    string Category,
    int? DeviceId,
    int? FertilityZoneId,
    string Source,
    string? ExternalId,
    DateTime CreatedAt
);
