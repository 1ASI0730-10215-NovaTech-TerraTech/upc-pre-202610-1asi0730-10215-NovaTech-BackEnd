using Microsoft.EntityFrameworkCore;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Repositories;
using NovaTech.TerraTech.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;

namespace NovaTech.TerraTech.Platform.Monitoring.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

public class WebhookRecommendationRepository(AppDbContext db) : IWebhookRecommendationRepository
{
    public async Task AddAsync(WebhookRecommendation recommendation, CancellationToken ct) =>
        await db.Set<WebhookRecommendation>().AddAsync(recommendation, ct);

    public Task<List<WebhookRecommendation>> ListAsync(int? fieldId, int? deviceId, CancellationToken ct)
    {
        var query = db.Set<WebhookRecommendation>().AsQueryable();
        if (fieldId is int targetFieldId)
            query = query.Where(x => x.FieldId == targetFieldId);
        if (deviceId is int targetDeviceId)
            query = query.Where(x => x.DeviceId == targetDeviceId);
        return query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public Task<WebhookRecommendation?> FindByIdAsync(int id, CancellationToken ct) =>
        db.Set<WebhookRecommendation>().SingleOrDefaultAsync(x => x.Id == id, ct);
}
