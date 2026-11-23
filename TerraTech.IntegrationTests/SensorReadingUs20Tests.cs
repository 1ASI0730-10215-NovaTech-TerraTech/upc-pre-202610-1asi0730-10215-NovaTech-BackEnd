using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

public partial class JourneyTests
{
    [Fact]
    public async Task Us20LatestReadingIncludesFieldSensorBatteryElapsedAndHandlesNotFound()
    {
        var a = await Account();
        var missing = await a.client.GetAsync("/api/v1/devices/999999/readings/latest");
        await AssertProblem(missing, HttpStatusCode.NotFound, "DEVICE_NOT_FOUND");

        var createDevice = await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = "02:AA:BB:CC:DD:01",
            status = "ONLINE",
            lastSync = "2026-11-10T12:00:00Z",
            sensorCode = "TT-US2001",
            name = "Parcel North Sensor",
            batteryLevelPercent = 76
        });
        Assert.Equal(HttpStatusCode.Created, createDevice.StatusCode);
        var deviceId = (await Json(createDevice)).GetProperty("id").GetInt32();

        var emptyReadings = await a.client.GetAsync($"/api/v1/devices/{deviceId}/readings/latest");
        await AssertProblem(emptyReadings, HttpStatusCode.NotFound, "NO_READINGS");

        var now = new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        server.Clock.Frozen = now;
        try
        {
            await server.WithDb(async db =>
            {
                db.Add(Measurement(deviceId, now.UtcDateTime.AddMinutes(-15)));
                await db.SaveChangesAsync();
            });

            var freshResponse = await a.client.GetAsync($"/api/v1/devices/{deviceId}/readings/latest");
            Assert.Equal(HttpStatusCode.OK, freshResponse.StatusCode);
            var fresh = await Json(freshResponse);
            Assert.False(fresh.GetProperty("isStale").GetBoolean());
            Assert.Equal(a.fieldId, fresh.GetProperty("fieldId").GetInt32());
            Assert.Equal("TT-US2001", fresh.GetProperty("sensorCode").GetString());
            Assert.Equal(76, fresh.GetProperty("batteryLevelPercent").GetInt32());
            Assert.Equal(15, fresh.GetProperty("elapsedMinutes").GetInt32());

            var lowBatteryDevice = await a.client.PostAsJsonAsync("/api/v1/devices", new
            {
                fieldId = a.fieldId,
                macAddress = "02:AA:BB:CC:DD:02",
                status = "LOW_BATTERY",
                lastSync = "2026-11-10T10:00:00Z",
                sensorCode = "TT-US2002",
                name = "Stale Low Battery Sensor"
            });
            Assert.Equal(HttpStatusCode.Created, lowBatteryDevice.StatusCode);
            var staleDeviceId = (await Json(lowBatteryDevice)).GetProperty("id").GetInt32();

            await server.WithDb(async db =>
            {
                db.Add(Measurement(staleDeviceId, now.UtcDateTime.AddMinutes(-90)));
                await db.SaveChangesAsync();
            });

            var staleResponse = await a.client.GetAsync($"/api/v1/devices/{staleDeviceId}/readings/latest");
            Assert.Equal(HttpStatusCode.OK, staleResponse.StatusCode);
            var stale = await Json(staleResponse);
            Assert.True(stale.GetProperty("isStale").GetBoolean());
            Assert.Equal(a.fieldId, stale.GetProperty("fieldId").GetInt32());
            Assert.Equal("TT-US2002", stale.GetProperty("sensorCode").GetString());
            Assert.Equal(15, stale.GetProperty("batteryLevelPercent").GetInt32());
            Assert.Equal(90, stale.GetProperty("elapsedMinutes").GetInt32());
        }
        finally
        {
            server.Clock.Frozen = null;
        }
    }
}
