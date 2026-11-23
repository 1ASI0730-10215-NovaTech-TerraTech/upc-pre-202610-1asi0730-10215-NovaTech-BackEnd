using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;

namespace NovaTech.TerraTech.Platform.Monitoring.Domain.Repositories;

public interface IWebhookRecommendationRepository
{
    Task AddAsync(WebhookRecommendation recommendation, CancellationToken ct);
    Task<List<WebhookRecommendation>> ListAsync(int? fieldId, int? deviceId, CancellationToken ct);
    Task<WebhookRecommendation?> FindByIdAsync(int id, CancellationToken ct);
}
