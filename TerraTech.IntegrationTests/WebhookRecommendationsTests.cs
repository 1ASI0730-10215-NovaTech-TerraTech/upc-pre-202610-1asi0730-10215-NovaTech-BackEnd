using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using Xunit;

public partial class JourneyTests
{
    [Fact]
    public async Task Us21WebhookValidatesApiKeyPayloadStoresAndIsolatesByOwner()
    {
        var owner = await Account();
        var otherUser = await Account();
        var deviceId = await Sensor(owner.client, owner.fieldId);

        var anonClient = server.CreateClient();
        var unauthenticatedMissingKey = await anonClient.PostAsJsonAsync("/api/v1/recommendations/webhook", new { });
        await AssertProblem(unauthenticatedMissingKey, HttpStatusCode.Unauthorized, "INVALID_API_KEY");

        anonClient.DefaultRequestHeaders.Remove("X-Api-Key");
        anonClient.DefaultRequestHeaders.Add("X-Api-Key", "wrong-api-key");
        var wrongKeyResponse = await anonClient.PostAsJsonAsync("/api/v1/recommendations/webhook", new
        {
            fieldId = owner.fieldId,
            zone = "North Sector",
            action = "Irrigate 15 minutes",
            durationMinutes = 15,
            priority = "HIGH",
            reason = "Moisture below 25%"
        });
        await AssertProblem(wrongKeyResponse, HttpStatusCode.Unauthorized, "INVALID_API_KEY");

        anonClient.DefaultRequestHeaders.Remove("X-Api-Key");
        anonClient.DefaultRequestHeaders.Add("X-Api-Key", TestServer.WebhookApiKey);

        var missingZoneResponse = await anonClient.PostAsJsonAsync("/api/v1/recommendations/webhook", new
        {
            fieldId = owner.fieldId,
            action = "Irrigate 20 minutes",
            durationMinutes = 20,
            priority = "HIGH",
            reason = "Dry topsoil"
        });
        Assert.Equal(HttpStatusCode.BadRequest, missingZoneResponse.StatusCode);
        var validationProblem = await Json(missingZoneResponse);
        Assert.True(validationProblem.GetProperty("errors").TryGetProperty("Zone", out _));
        await server.WithDb(async db =>
            Assert.False(await db.Set<WebhookRecommendation>().IgnoreQueryFilters()
                .AnyAsync(x => x.FieldId == owner.fieldId && x.Action == "Irrigate 20 minutes")));

        var validResponse = await anonClient.PostAsJsonAsync("/api/v1/recommendations/webhook", new
        {
            fieldId = owner.fieldId,
            deviceId,
            zone = "North Sector",
            action = "Activate drip irrigation",
            durationMinutes = 25,
            priority = "HIGH",
            reason = "Moisture dropped to 21% in sandy loam zone",
            category = "IRRIGATION",
            externalId = "ext-us21-001"
        });
        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);
        Assert.NotNull(validResponse.Headers.Location);
        var created = await Json(validResponse);
        var createdId = created.GetProperty("id").GetInt32();
        Assert.Equal($"/api/v1/recommendations/webhooks/{createdId}", validResponse.Headers.Location!.ToString());
        Assert.Equal(owner.fieldId, created.GetProperty("fieldId").GetInt32());
        Assert.Equal("North Sector", created.GetProperty("zone").GetString());
        Assert.Equal("Activate drip irrigation", created.GetProperty("action").GetString());
        Assert.Equal(25, created.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("HIGH", created.GetProperty("priority").GetString());

        var ownerList = await Json(await owner.client.GetAsync($"/api/v1/recommendations/webhooks?fieldId={owner.fieldId}"));
        Assert.Equal(JsonValueKind.Array, ownerList.ValueKind);
        Assert.Contains(ownerList.EnumerateArray(), item => item.GetProperty("id").GetInt32() == createdId);

        var ownerGetById = await owner.client.GetAsync(validResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, ownerGetById.StatusCode);

        var otherList = await Json(await otherUser.client.GetAsync("/api/v1/recommendations/webhooks"));
        Assert.DoesNotContain(otherList.EnumerateArray(), item => item.GetProperty("id").GetInt32() == createdId);

        var otherGetById = await otherUser.client.GetAsync(validResponse.Headers.Location);
        Assert.Equal(HttpStatusCode.NotFound, otherGetById.StatusCode);
    }
}
