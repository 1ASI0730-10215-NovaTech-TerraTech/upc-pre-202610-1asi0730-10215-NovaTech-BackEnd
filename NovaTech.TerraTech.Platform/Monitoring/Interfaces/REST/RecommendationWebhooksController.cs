using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaTech.TerraTech.Platform.Monitoring.Application.Internal.CommandServices;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Filters;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST;

[ApiController]
[Route("api/v1/recommendations")]
[Tags("Monitoring")]
public class RecommendationWebhooksController(WebhookRecommendationService service) : ControllerBase
{
    [AllowAnonymous]
    [ServiceFilter(typeof(WebhookApiKeyFilter))]
    [HttpPost("webhook")]
    [ProducesResponseType<WebhookRecommendationResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Ingest agronomic recommendation via webhook",
        Description = "Authenticated with X-Api-Key header. Validates zone, action, durationMinutes, priority, reason, and target fieldId, persists the recommendation, and returns 201 Created with Location.",
        OperationId = "IngestRecommendationWebhook")]
    public async Task<ActionResult<WebhookRecommendationResource>> Ingest(
        [FromBody] CreateWebhookRecommendationResource resource,
        CancellationToken ct)
    {
        var created = await service.IngestAsync(resource, ct);
        var dto = ToResource(created);
        return Created($"/api/v1/recommendations/webhooks/{dto.Id}", dto);
    }

    [Authorize]
    [HttpGet("webhooks")]
    [ProducesResponseType<IEnumerable<WebhookRecommendationResource>>(StatusCodes.Status200OK)]
    [SwaggerOperation(
        Summary = "List webhook recommendations for owned fields",
        Description = "Returns stored analysis engine recommendations belonging to fields owned by the authenticated farmer.",
        OperationId = "ListWebhookRecommendations")]
    public async Task<ActionResult<IEnumerable<WebhookRecommendationResource>>> List(
        [FromQuery] int? fieldId,
        [FromQuery] int? deviceId,
        CancellationToken ct)
    {
        var items = await service.ListForOwnerAsync(fieldId, deviceId, ct);
        return Ok(items.Select(ToResource).ToList());
    }

    [Authorize]
    [HttpGet("webhooks/{id:int}")]
    [ProducesResponseType<WebhookRecommendationResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Get webhook recommendation by ID",
        Description = "Returns a stored webhook recommendation belonging to an owned field.",
        OperationId = "GetWebhookRecommendationById")]
    public async Task<ActionResult<WebhookRecommendationResource>> GetById(int id, CancellationToken ct) =>
        ToResource(await service.GetByIdForOwnerAsync(id, ct));

    private static WebhookRecommendationResource ToResource(WebhookRecommendation x) =>
        new(
            x.Id,
            x.FieldId,
            x.Zone,
            x.Action,
            x.DurationMinutes,
            x.Priority,
            x.Reason,
            x.Category,
            x.DeviceId,
            x.FertilityZoneId,
            x.Source,
            x.ExternalId,
            DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)
        );
}
