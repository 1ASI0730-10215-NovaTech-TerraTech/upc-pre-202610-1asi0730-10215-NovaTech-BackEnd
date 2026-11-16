using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NovaTech.TerraTech.Platform.Iam.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using Xunit;

public partial class JourneyTests
{
    private static Dictionary<string, object?> Registration(string? email = null) => new()
    {
        ["fullName"] = "Test Farmer",
        ["emailAddress"] = email ?? Guid.NewGuid() + "@test.example",
        ["password"] = "test-password",
        ["confirmPassword"] = "test-password"
    };
    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode expected, string? code = null)
    {
        Assert.Equal(expected, response.StatusCode);
        var error = await Json(response);
        Assert.Equal((int)expected, error.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(error.GetProperty("title").GetString()));
        Assert.False(error.TryGetProperty("stackTrace", out _));
        if (code != null)
            Assert.Equal(code, error.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("fullName", null)]
    [InlineData("fullName", "a")]
    [InlineData("fullName", "  ")]
    [InlineData("emailAddress", null)]
    [InlineData("emailAddress", "invalid-email")]
    [InlineData("password", null)]
    [InlineData("password", "12345")]
    [InlineData("confirmPassword", null)]
    [InlineData("confirmPassword", "")]
    public async Task RegistrationFieldsFailIndependentlyWithoutPersistence(string field, string? value)
    {
        var body = Registration();
        if (value == null)
            body.Remove(field);
        else
            body[field] = value;
        if (field == "password")
            body["confirmPassword"] = value;
        await AssertProblem(await server.CreateClient().PostAsJsonAsync("/api/v1/authentication/sign-up", body), HttpStatusCode.BadRequest);
        if (body.TryGetValue("emailAddress", out var email) && email is string emailAddress)
            await server.WithDb(async db => Assert.False(await db.Set<User>().AnyAsync(x => x.EmailAddress.Value == emailAddress)));
    }

    [Theory]
    [InlineData("TEST-password")]
    [InlineData(" test-password")]
    [InlineData("test-password ")]
    public async Task ConfirmationIsExactAndRejectedSignupDoesNotReserveEmail(string confirmation)
    {
        var client = server.CreateClient();
        var body = Registration();
        body["confirmPassword"] = confirmation;
        await AssertProblem(await client.PostAsJsonAsync("/api/v1/authentication/sign-up", body), HttpStatusCode.BadRequest, "PASSWORD_CONFIRMATION_MISMATCH");
        body["confirmPassword"] = body["password"];
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/authentication/sign-up", body)).StatusCode);
    }

    [Fact]
    public async Task RegistrationAcceptsMinimumsNormalizesEmailAndDoesNotExposePasswords()
    {
        var client = server.CreateClient();
        var email = "Farmer-" + Guid.NewGuid() + "@TEST.EXAMPLE";
        var body = Registration(email);
        body["fullName"] = " Ab ";
        body["password"] = "123456";
        body["confirmPassword"] = "123456";
        var created = await client.PostAsJsonAsync("/api/v1/authentication/sign-up", body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await Json(created);
        Assert.Equal("Ab", user.GetProperty("fullName").GetString());
        Assert.Equal(email.ToLowerInvariant(), user.GetProperty("emailAddress").GetString());
        Assert.False(user.TryGetProperty("password", out _));
        Assert.False(user.TryGetProperty("confirmPassword", out _));
        Assert.False(user.TryGetProperty("passwordHash", out _));
        body["emailAddress"] = email.ToLowerInvariant();
        await AssertProblem(await client.PostAsJsonAsync("/api/v1/authentication/sign-up", body), HttpStatusCode.Conflict, "EMAIL_EXISTS");
        await AssertProblem(await client.PostAsJsonAsync("/api/v1/authentication/sign-in", new { emailAddress = Guid.NewGuid() + "@test.example", password = "test-password" }), HttpStatusCode.Unauthorized, "INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task JwtRequiresExpirationAndApprovedAlgorithmForExistingUser()
    {
        var a = await Account();
        Assert.Equal(HttpStatusCode.OK, (await a.client.GetAsync("/api/v1/users/me")).StatusCode);
        foreach (var kind in new[]
        {
            "no-expiration",
            "unsupported-algorithm",
            "unsigned",
            "future"
        }

        )
        {
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([new Claim("sub", a.userId.ToString())]),
                IssuedAt = DateTime.UtcNow.AddMinutes(-1),
                NotBefore = DateTime.UtcNow.AddMinutes(-1),
                Expires = kind == "no-expiration" ? null : DateTime.UtcNow.AddHours(1),
                SigningCredentials = kind == "unsigned" ? null : new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestServer.Secret)), kind == "unsupported-algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256)
            };
            if (kind == "future")
                descriptor.NotBefore = DateTime.UtcNow.AddMinutes(5);
            var handler = new JsonWebTokenHandler
            {
                SetDefaultTimesOnTokenCreation = false
            };
            using var client = server.CreateClient();
            client.DefaultRequestHeaders.Authorization = new("Bearer", handler.CreateToken(descriptor));
            await AssertProblem(await client.GetAsync("/api/v1/users/me"), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        }
    }

    [Fact]
    public async Task ProfileEditPersistsPersonalTerrainDataAndPreservesEmailAndThresholds()
    {
        var a = await Account();
        var original = await Json(await a.client.GetAsync("/api/v1/users/me"));
        var edited = await a.client.PutAsJsonAsync("/api/v1/profiles/me", new { fullName = "Updated Farmer", fundoName = "South Farm", contactPhone = "988777666", location = "Huaral", sizeM2 = 25000, moistureThreshold = 42, tempThreshold = 29, emailAddress = "intruder@test.example", userId = int.MaxValue });
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var profile = await Json(await a.client.GetAsync("/api/v1/profiles/me"));
        Assert.Equal(a.profileId, profile.GetProperty("id").GetInt32());
        Assert.Equal(a.userId, profile.GetProperty("userId").GetInt32());
        Assert.Equal("Updated Farmer", profile.GetProperty("fullName").GetString());
        Assert.Equal("South Farm", profile.GetProperty("fundoName").GetString());
        Assert.Equal("988777666", profile.GetProperty("contactPhone").GetString());
        Assert.Equal("Huaral", profile.GetProperty("location").GetString());
        Assert.Equal(25000, profile.GetProperty("sizeM2").GetDouble());
        Assert.Equal(original.GetProperty("emailAddress").GetString(), profile.GetProperty("emailAddress").GetString());
        var account = await Json(await a.client.GetAsync("/api/v1/users/me"));
        Assert.Equal("Updated Farmer", account.GetProperty("fullName").GetString());
        var login = await Json(await server.CreateClient().PostAsJsonAsync("/api/v1/authentication/sign-in", new { emailAddress = original.GetProperty("emailAddress").GetString(), password = "test-password" }));
        Assert.Equal("Updated Farmer", login.GetProperty("fullName").GetString());
        await a.client.PutAsJsonAsync("/api/v1/profiles/me", Profile());
        profile = await Json(await a.client.GetAsync("/api/v1/profiles/me"));
        Assert.Equal(42, profile.GetProperty("moistureThreshold").GetDouble());
        Assert.Equal(29, profile.GetProperty("tempThreshold").GetDouble());
    }

    [Theory]
    [InlineData("fullName", " ")]
    [InlineData("fundoName", "")]
    [InlineData("contactPhone", "")]
    [InlineData("location", " ")]
    [InlineData("sizeM2", "0")]
    [InlineData("moistureThreshold", "101")]
    public async Task InvalidProfileDoesNotChangePersistedData(string field, string value)
    {
        var a = await Account();
        var before = await Json(await a.client.GetAsync("/api/v1/profiles/me"));
        var body = new Dictionary<string, object?>
        {
            ["fullName"] = "Demo Farmer",
            ["fundoName"] = "Test Farm",
            ["contactPhone"] = "999888777",
            ["location"] = "Lima",
            ["sizeM2"] = 10000
        };
        body[field] = field is "sizeM2" or "moistureThreshold" ? double.Parse(value) : value;
        await AssertProblem(await a.client.PutAsJsonAsync("/api/v1/profiles/me", body), HttpStatusCode.BadRequest);
        var after = await Json(await a.client.GetAsync("/api/v1/profiles/me"));
        Assert.Equal(before.GetRawText(), after.GetRawText());
    }

    [Fact]
    public async Task ParcelSelectionReturnsOnlyItsDevicesAndSupportsEmptyParcel()
    {
        var a = await Account();
        var b = await Account();
        var id = await Sensor(a.client, a.fieldId);
        var second = await a.client.PostAsJsonAsync("/api/v1/fields", new { profileId = a.profileId, name = "South", sizeM2 = 1000, soilType = "SANDY", latitude = -12.1, longitude = -77.1, cropName = "Corn" });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondId = (await Json(second)).GetProperty("id").GetInt32();
        var fields = await Json(await a.client.GetAsync("/api/v1/fields"));
        Assert.Contains(fields.EnumerateArray(), x => x.GetProperty("id").GetInt32() == a.fieldId);
        Assert.Contains(fields.EnumerateArray(), x => x.GetProperty("id").GetInt32() == secondId);
        Assert.DoesNotContain(fields.EnumerateArray(), x => x.GetProperty("id").GetInt32() == b.fieldId);
        var devices = await Json(await a.client.GetAsync($"/api/v1/fields/{a.fieldId}/devices"));
        var device = Assert.Single(devices.EnumerateArray());
        Assert.Equal(id, device.GetProperty("id").GetInt32());
        Assert.Equal(a.fieldId, device.GetProperty("fieldId").GetInt32());
        var empty = await Json(await a.client.GetAsync($"/api/v1/fields/{secondId}/devices"));
        Assert.Empty(empty.EnumerateArray());
        await AssertProblem(await b.client.GetAsync($"/api/v1/fields/{a.fieldId}/devices"), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("TT-12345")]
    [InlineData("tt-ABC123")]
    [InlineData("")]
    public async Task MalformedSensorCodesDoNotCreateAssociations(string code)
    {
        var a = await Account();
        await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/devices/register", new { sensorCode = code, fieldId = a.fieldId, name = "Demo" }), HttpStatusCode.BadRequest);
        var devices = await Json(await a.client.GetAsync("/api/v1/devices"));
        Assert.Empty(devices.EnumerateArray());
    }

    private static SensorReading Measurement(int deviceId, DateTime at) => new()
    {
        DeviceId = deviceId,
        RecordedAt = at,
        MoisturePercent = 42,
        SoilTemperatureC = 23,
        NitrogenPpm = 35,
        PhosphorusPpm = 18,
        PotassiumPpm = 60,
        Source = "SIMULATED"
    };
    [Theory]
    [InlineData(1799, false)]
    [InlineData(1800, false)]
    [InlineData(1801, true)]
    public async Task LatestStalenessUsesStrictThirtyMinuteBoundary(int seconds, bool expected)
    {
        var a = await Account();
        var device = await Sensor(a.client, a.fieldId);
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        server.Clock.Frozen = now;
        try
        {
            await server.WithDb(async db =>
            {
                db.Add(Measurement(device, now.UtcDateTime.AddSeconds(-seconds)));
                db.Add(Measurement(device, now.UtcDateTime.AddDays(-1)));
                await db.SaveChangesAsync();
            });
            var latest = await Json(await a.client.GetAsync($"/api/v1/devices/{device}/readings/latest"));
            Assert.Equal(expected, latest.GetProperty("isStale").GetBoolean());
            var reading = latest.GetProperty("reading");
            Assert.Equal(now.UtcDateTime.AddSeconds(-seconds), reading.GetProperty("recordedAt").GetDateTime());
            Assert.Equal(42, reading.GetProperty("moisturePercent").GetDouble());
            Assert.Equal(23, reading.GetProperty("soilTemperatureC").GetDouble());
            Assert.Equal(35, reading.GetProperty("nitrogenPpm").GetDouble());
            Assert.Equal(18, reading.GetProperty("phosphorusPpm").GetDouble());
            Assert.Equal(60, reading.GetProperty("potassiumPpm").GetDouble());
        }
        finally
        {
            server.Clock.Frozen = null;
        }
    }

    [Fact]
    public async Task HistoryIncludesUtcBoundariesExcludesFutureAndSupportsDefaultRange()
    {
        var a = await Account();
        var id = await Sensor(a.client, a.fieldId);
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        server.Clock.Frozen = now;
        try
        {
            var dates = new[]
            {
                now.AddDays(-30).AddSeconds(-1),
                now.AddDays(-30),
                now.AddDays(-7).AddSeconds(-1),
                now.AddDays(-7),
                now,
                now.AddSeconds(1)
            };
            await server.WithDb(async db =>
            {
                foreach (var at in dates.Reverse())
                    db.Add(Measurement(id, at.UtcDateTime));
                await db.SaveChangesAsync();
            });
            var week = await Json(await a.client.GetAsync($"/api/v1/devices/{id}/readings"));
            var explicitWeek = await Json(await a.client.GetAsync($"/api/v1/devices/{id}/readings?days=7"));
            Assert.Equal(week.GetRawText(), explicitWeek.GetRawText());
            Assert.Equal(now.AddDays(-7).UtcDateTime, week.GetProperty("fromUtc").GetDateTime());
            Assert.Equal(now.UtcDateTime, week.GetProperty("toUtc").GetDateTime());
            Assert.Equal(2, week.GetProperty("readings").GetArrayLength());
            var month = await Json(await a.client.GetAsync($"/api/v1/devices/{id}/readings?days=30"));
            var rows = month.GetProperty("readings").EnumerateArray().ToList();
            Assert.Equal(4, rows.Count);
            Assert.Equal(now.AddDays(-30).UtcDateTime, rows.First().GetProperty("recordedAt").GetDateTime());
            Assert.Equal(now.UtcDateTime, rows.Last().GetProperty("recordedAt").GetDateTime());
            Assert.Equal(rows.Select(x => x.GetProperty("recordedAt").GetDateTime()).Order(), rows.Select(x => x.GetProperty("recordedAt").GetDateTime()));
            foreach (var range in new[]
            {
                "0",
                "-7",
                "14",
                "abc"
            }

            )
                await AssertProblem(await a.client.GetAsync($"/api/v1/devices/{id}/readings?days={range}"), HttpStatusCode.BadRequest);
        }
        finally
        {
            server.Clock.Frozen = null;
        }
    }

    [Fact]
    public async Task ReadingDetailPreservesCacheContractAndRejectsOtherDeviceAndOwner()
    {
        var a = await Account();
        var b = await Account();
        var device = await Sensor(a.client, a.fieldId);
        var otherDevice = await Sensor(a.client, a.fieldId);
        var at = DateTime.UtcNow.AddMinutes(-5);
        var fresh = Measurement(device, at);
        var old = Measurement(device, at.AddDays(-40));
        await server.WithDb(async db =>
        {
            db.AddRange(fresh, old);
            await db.SaveChangesAsync();
        });
        var route = $"/api/v1/devices/{device}/readings/{fresh.Id}";
        var detail = await Json(await a.client.GetAsync(route));
        var again = await Json(await a.client.GetAsync(route));
        Assert.Equal(detail.GetRawText(), again.GetRawText());
        var latest = await Json(await a.client.GetAsync($"/api/v1/devices/{device}/readings/latest"));
        Assert.Equal(latest.GetProperty("reading").GetRawText(), detail.GetRawText());
        var history = await Json(await a.client.GetAsync($"/api/v1/devices/{device}/readings"));
        var historic = Assert.Single(history.GetProperty("readings").EnumerateArray());
        Assert.Equal(historic.GetRawText(), detail.GetRawText());
        Assert.Equal(fresh.Id, detail.GetProperty("id").GetInt32());
        Assert.Equal(device, detail.GetProperty("deviceId").GetInt32());
        Assert.Equal("SIMULATED", detail.GetProperty("source").GetString());
        Assert.EndsWith("Z", detail.GetProperty("recordedAt").GetString());
        Assert.Equal(HttpStatusCode.OK, (await a.client.GetAsync($"/api/v1/devices/{device}/readings/{old.Id}")).StatusCode);
        await AssertProblem(await a.client.GetAsync($"/api/v1/devices/{device}/readings/{int.MaxValue}"), HttpStatusCode.NotFound, "READING_NOT_FOUND");
        await AssertProblem(await a.client.GetAsync($"/api/v1/devices/{otherDevice}/readings/{fresh.Id}"), HttpStatusCode.NotFound, "READING_NOT_FOUND");
        await AssertProblem(await b.client.GetAsync(route), HttpStatusCode.NotFound, "DEVICE_NOT_FOUND");
        await AssertProblem(await server.CreateClient().GetAsync(route), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        await AssertProblem(await a.client.GetAsync($"/api/v1/devices/{int.MaxValue}/readings/{fresh.Id}"), HttpStatusCode.NotFound, "DEVICE_NOT_FOUND");
        await server.WithDb(async db => Assert.Equal(2, await db.Set<SensorReading>().IgnoreQueryFilters().CountAsync(x => x.DeviceId == device)));
    }

    [Fact]
    public async Task DirectDeviceRegistrationWorksWithoutCatalogAndValidatesSensorCodeAndMac()
    {
        var a = await Account();
        var b = await Account();
        var mac1 = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("x2")));
        var mac2 = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("x2")));
        var mac3 = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("x2")));
        var uncatalogedCode = "TT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var createdWithoutCode = await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = mac1.Replace(':', '-'),
            status = "OFFLINE",
            lastSync = DateTimeOffset.UtcNow
        });
        Assert.Equal(HttpStatusCode.Created, createdWithoutCode.StatusCode);
        var d1 = await Json(createdWithoutCode);
        Assert.Equal(mac1.ToUpperInvariant(), d1.GetProperty("macAddress").GetString());
        Assert.Equal(JsonValueKind.Null, d1.GetProperty("sensorCode").ValueKind);

        var createdWithUncatalogedCode = await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = mac2,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = uncatalogedCode,
            name = "  Sensor Norte  "
        });
        Assert.Equal(HttpStatusCode.Created, createdWithUncatalogedCode.StatusCode);
        var d2 = await Json(createdWithUncatalogedCode);
        Assert.Equal(uncatalogedCode, d2.GetProperty("sensorCode").GetString());
        Assert.Equal("Sensor Norte", d2.GetProperty("name").GetString());

        await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = mac2.ToUpperInvariant(),
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow
        }), HttpStatusCode.Conflict, "DEVICE_ALREADY_REGISTERED");

        await AssertProblem(await b.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = b.fieldId,
            macAddress = mac3,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = uncatalogedCode,
            name = "Duplicate Code"
        }), HttpStatusCode.Conflict, "SENSOR_OCCUPIED");

        await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = mac3,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = "TT-123"
        }), HttpStatusCode.BadRequest, "INVALID_SENSOR_CODE");

        await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = "not-a-mac",
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow
        }), HttpStatusCode.BadRequest, "INVALID_MAC_ADDRESS");

        await AssertProblem(await b.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = mac3,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow
        }), HttpStatusCode.NotFound, "FIELD_NOT_FOUND");
    }

    [Fact]
    public async Task SensorReadingPostPersistsManualReadingsEvaluatesAlertsAndPreventsOrphans()
    {
        var a = await Account();
        var b = await Account();
        var mappedFieldResponse = await a.client.PostAsJsonAsync("/api/v1/fields", MappedField(a.profileId, Rectangle()));
        Assert.Equal(HttpStatusCode.Created, mappedFieldResponse.StatusCode);
        var mappedFieldId = (await Json(mappedFieldResponse)).GetProperty("id").GetInt32();
        var macA = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("X2")));
        var macB = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("X2")));
        var codeA = "TT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var codeB = "TT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        var devAResponse = await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = mappedFieldId,
            macAddress = macA,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = codeA,
            name = "Sensor A"
        });
        Assert.Equal(HttpStatusCode.Created, devAResponse.StatusCode);
        var deviceA = (await Json(devAResponse)).GetProperty("id").GetInt32();

        var devBResponse = await b.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = b.fieldId,
            macAddress = macB,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = codeB,
            name = "Sensor B"
        });
        Assert.Equal(HttpStatusCode.Created, devBResponse.StatusCode);
        var deviceB = (await Json(devBResponse)).GetProperty("id").GetInt32();

        var zoneResponse = await a.client.PostAsJsonAsync($"/api/v1/fields/{mappedFieldId}/fertility-zones", new
        {
            deviceId = deviceA,
            name = "Zona Norte",
            boundary = new[]
            {
                new { latitude = -14.0002, longitude = -74.9998 },
                new { latitude = -14.0002, longitude = -74.9992 },
                new { latitude = -14.0008, longitude = -74.9992 },
                new { latitude = -14.0008, longitude = -74.9998 }
            }
        });
        Assert.Equal(HttpStatusCode.Created, zoneResponse.StatusCode);
        await AssertProblem(await a.client.DeleteAsync($"/api/v1/devices/{deviceA}"), HttpStatusCode.Conflict, "DEPENDENT_RESOURCES");
        await AssertProblem(await a.client.DeleteAsync($"/api/v1/fields/{mappedFieldId}"), HttpStatusCode.Conflict, "DEPENDENT_RESOURCES");

        var frozen = new DateTimeOffset(2026, 11, 15, 18, 0, 0, TimeSpan.Zero);
        server.Clock.Frozen = frozen;
        try
        {
            var readingPost = await a.client.PostAsJsonAsync("/api/v1/sensor-readings", new
            {
                deviceId = deviceA,
                moisturePercent = 15.0,
                soilTemperatureC = 22.5,
                nitrogenPpm = 30.0,
                phosphorusPpm = 20.0,
                potassiumPpm = 40.0
            });
            Assert.Equal(HttpStatusCode.Created, readingPost.StatusCode);
            var createdReading = await Json(readingPost);
            var readingId = createdReading.GetProperty("id").GetInt32();
            Assert.Equal(deviceA, createdReading.GetProperty("deviceId").GetInt32());
            Assert.Equal("MANUAL", createdReading.GetProperty("source").GetString());

            await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/sensor-readings", new
            {
                deviceId = deviceA,
                moisturePercent = 40.0,
                soilTemperatureC = 22.5,
                nitrogenPpm = 30.0,
                phosphorusPpm = 20.0,
                potassiumPpm = 40.0
            }), HttpStatusCode.Conflict, "READING_ALREADY_RECORDED");

            var sameTimestampOtherDevice = await b.client.PostAsJsonAsync("/api/v1/sensor-readings", new
            {
                deviceId = deviceB,
                moisturePercent = 50.0,
                soilTemperatureC = 21.0,
                nitrogenPpm = 25.0,
                phosphorusPpm = 20.0,
                potassiumPpm = 35.0
            });
            Assert.Equal(HttpStatusCode.Created, sameTimestampOtherDevice.StatusCode);

            var latestA = await Json(await a.client.GetAsync($"/api/v1/devices/{deviceA}/readings/latest"));
            Assert.Equal(readingId, latestA.GetProperty("reading").GetProperty("id").GetInt32());
            Assert.Equal("MANUAL", latestA.GetProperty("reading").GetProperty("source").GetString());

            var historyA = await Json(await a.client.GetAsync($"/api/v1/devices/{deviceA}/readings?days=7"));
            var singleA = Assert.Single(historyA.GetProperty("readings").EnumerateArray());
            Assert.Equal(readingId, singleA.GetProperty("id").GetInt32());

            var detailA = await Json(await a.client.GetAsync($"/api/v1/devices/{deviceA}/readings/{readingId}"));
            Assert.Equal(readingId, detailA.GetProperty("id").GetInt32());
            Assert.Equal(15.0, detailA.GetProperty("moisturePercent").GetDouble());

            var historyB = await Json(await b.client.GetAsync($"/api/v1/devices/{deviceB}/readings?days=7"));
            var singleB = Assert.Single(historyB.GetProperty("readings").EnumerateArray());
            Assert.Equal(deviceB, singleB.GetProperty("deviceId").GetInt32());

            await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/sensor-readings", new
            {
                deviceId = deviceB,
                moisturePercent = 45.0,
                soilTemperatureC = 20.0,
                nitrogenPpm = 25.0,
                phosphorusPpm = 20.0,
                potassiumPpm = 35.0
            }), HttpStatusCode.NotFound, "DEVICE_NOT_FOUND");

            await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/sensor-readings", new
            {
                deviceId = int.MaxValue,
                moisturePercent = 45.0,
                soilTemperatureC = 20.0,
                nitrogenPpm = 25.0,
                phosphorusPpm = 20.0,
                potassiumPpm = 35.0
            }), HttpStatusCode.NotFound, "DEVICE_NOT_FOUND");

            await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/sensor-readings", new
            {
                deviceId = deviceA,
                moisturePercent = 120.0,
                soilTemperatureC = 20.0,
                nitrogenPpm = 25.0,
                phosphorusPpm = 20.0,
                potassiumPpm = 35.0
            }), HttpStatusCode.BadRequest, "INVALID_READING");

            var notificationsA = await Json(await a.client.GetAsync($"/api/v1/notifications?profileId={a.profileId}"));
            Assert.Contains(notificationsA.EnumerateArray(), n => n.GetProperty("title").GetString()!.Contains("Zona Norte"));
            var notificationsB = await Json(await b.client.GetAsync($"/api/v1/notifications?profileId={b.profileId}"));
            Assert.Empty(notificationsB.EnumerateArray());
        }
        finally
        {
            server.Clock.Frozen = null;
        }
    }
}
