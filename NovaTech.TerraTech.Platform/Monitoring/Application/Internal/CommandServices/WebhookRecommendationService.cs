using Microsoft.EntityFrameworkCore;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Repositories;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;
using NovaTech.TerraTech.Platform.Shared.Domain.Repositories;
using NovaTech.TerraTech.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;

namespace NovaTech.TerraTech.Platform.Monitoring.Application.Internal.CommandServices;

public class WebhookRecommendationService(
    IWebhookRecommendationRepository recommendations,
    AppDbContext db,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.OrdinalIgnoreCase)
    {
        "LOW", "MEDIUM", "HIGH", "CRITICAL", "INFO", "WARNING"
    };

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "IRRIGATION", "FERTILIZATION", "CLIMATE", "GENERAL"
    };

    public async Task<WebhookRecommendation> IngestAsync(CreateWebhookRecommendationResource r, CancellationToken ct)
    {
        var zone = RequireNonBlank(r.Zone, "Zone");
        var action = RequireNonBlank(r.Action, "Action");
        var reason = RequireNonBlank(r.Reason, "Reason");
        var priority = RequireNonBlank(r.Priority, "Priority").ToUpperInvariant();
        if (!AllowedPriorities.Contains(priority))
            throw new ApiFailure(400, "INVALID_RECOMMENDATION", "Priority must be LOW, MEDIUM, HIGH, CRITICAL, INFO, or WARNING.");

        var category = string.IsNullOrWhiteSpace(r.Category) ? "IRRIGATION" : r.Category.Trim().ToUpperInvariant();
        if (!AllowedCategories.Contains(category))
            throw new ApiFailure(400, "INVALID_RECOMMENDATION", "Category must be IRRIGATION, FERTILIZATION, CLIMATE, or GENERAL.");

        var fieldId = r.FieldId!.Value;
        var fieldExists = await db.Set<Field>().IgnoreQueryFilters().AnyAsync(f => f.Id == fieldId, ct);
        if (!fieldExists)
            throw new ApiFailure(404, "FIELD_NOT_FOUND", "Field was not found.");

        if (r.DeviceId is int deviceId)
        {
            var deviceExists = await db.Set<Device>().IgnoreQueryFilters()
                .AnyAsync(d => d.Id == deviceId && d.FieldId.Value == fieldId, ct);
            if (!deviceExists)
                throw new ApiFailure(404, "DEVICE_NOT_FOUND", "Device was not found for field.");
        }

        if (r.FertilityZoneId is int fertilityZoneId)
        {
            var zoneExists = await db.Set<FertilityZone>().IgnoreQueryFilters()
                .AnyAsync(z => z.Id == fertilityZoneId && z.FieldId == fieldId, ct);
            if (!zoneExists)
                throw new ApiFailure(404, "ZONE_NOT_FOUND", "Fertility zone was not found for field.");
        }

        var entity = new WebhookRecommendation
        {
            FieldId = fieldId,
            DeviceId = r.DeviceId,
            FertilityZoneId = r.FertilityZoneId,
            Zone = zone,
            Action = action,
            DurationMinutes = r.DurationMinutes!.Value,
            Priority = priority,
            Reason = reason,
            Category = category,
            Source = string.IsNullOrWhiteSpace(r.Source) ? "ANALYSIS_ENGINE" : r.Source.Trim(),
            ExternalId = string.IsNullOrWhiteSpace(r.ExternalId) ? null : r.ExternalId.Trim(),
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        await recommendations.AddAsync(entity, ct);
        await unitOfWork.CompleteAsync(ct);
        return entity;
    }

    public Task<List<WebhookRecommendation>> ListForOwnerAsync(int? fieldId, int? deviceId, CancellationToken ct) =>
        recommendations.ListAsync(fieldId, deviceId, ct);

    public async Task<WebhookRecommendation> GetByIdForOwnerAsync(int id, CancellationToken ct) =>
        await recommendations.FindByIdAsync(id, ct)
        ?? throw new ApiFailure(404, "RESOURCE_NOT_FOUND", "Webhook recommendation was not found.");

    private static string RequireNonBlank(string? value, string fieldName)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new ApiFailure(400, "INVALID_RECOMMENDATION", $"{fieldName} is required.");
        return trimmed;
    }
}
