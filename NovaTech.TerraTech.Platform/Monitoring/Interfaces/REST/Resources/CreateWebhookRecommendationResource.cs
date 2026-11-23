using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;

[SwaggerSchema(Description = "Webhook payload to ingest an analysis engine recommendation")]
public record CreateWebhookRecommendationResource(
    [Required(ErrorMessage = "FieldId is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "FieldId must be a positive integer.")]
    [SwaggerParameter(Description = "Target field ID")]
    int? FieldId,
    [Required(ErrorMessage = "Zone is required.")]
    [StringLength(120, MinimumLength = 1, ErrorMessage = "Zone must be between 1 and 120 characters.")]
    [SwaggerParameter(Description = "Target zone name or identifier")]
    string? Zone,
    [Required(ErrorMessage = "Action is required.")]
    [StringLength(160, MinimumLength = 1, ErrorMessage = "Action must be between 1 and 160 characters.")]
    [SwaggerParameter(Description = "Recommended agronomic action")]
    string? Action,
    [Required(ErrorMessage = "DurationMinutes is required.")]
    [Range(1, 10080, ErrorMessage = "DurationMinutes must be between 1 and 10080.")]
    [SwaggerParameter(Description = "Duration of the action in minutes")]
    int? DurationMinutes,
    [Required(ErrorMessage = "Priority is required.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "Priority is required.")]
    [SwaggerParameter(Description = "Recommendation priority (LOW, MEDIUM, HIGH, CRITICAL, INFO, WARNING)")]
    string? Priority,
    [Required(ErrorMessage = "Reason is required.")]
    [StringLength(1000, MinimumLength = 1, ErrorMessage = "Reason must be between 1 and 1000 characters.")]
    [SwaggerParameter(Description = "Explanation or analysis trigger")]
    string? Reason,
    [SwaggerParameter(Description = "Optional associated device ID")]
    int? DeviceId = null,
    [SwaggerParameter(Description = "Optional associated fertility zone ID")]
    int? FertilityZoneId = null,
    [MaxLength(40)]
    [SwaggerParameter(Description = "Optional category (IRRIGATION, FERTILIZATION, CLIMATE, GENERAL)")]
    string? Category = null,
    [MaxLength(80)]
    [SwaggerParameter(Description = "Optional source analysis engine identifier")]
    string? Source = null,
    [MaxLength(120)]
    [SwaggerParameter(Description = "Optional external correlation ID")]
    string? ExternalId = null
);
