using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NovaTech.TerraTech.Platform.Iam.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Application.Internal.CommandServices;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Repositories;
using NovaTech.TerraTech.Platform.Shared.Domain.Repositories;
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
    public async Task AutomaticCatalogProvisioningReusesCompatibleEntriesAndRejectsConflicts()
    {
        var a = await Account();
        var b = await Account();

        var mac1 = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("x2")));
        var mac2 = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("x2")));
        var mac3 = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("x2")));
        var code2 = "TT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var codeOther = "TT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        // Caso A1: New MAC without SensorCode -> auto-provisions SensorCatalogItem with generated TT-XXXXXX and creates Device
        var createdWithoutCode = await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = mac1.Replace(':', '-'),
            status = "OFFLINE",
            lastSync = DateTimeOffset.UtcNow
        });
        Assert.Equal(HttpStatusCode.Created, createdWithoutCode.StatusCode);
        var d1 = await Json(createdWithoutCode);
        var generatedCode = d1.GetProperty("sensorCode").GetString()!;
        Assert.Equal(mac1.ToUpperInvariant(), d1.GetProperty("macAddress").GetString());
        Assert.Matches("^TT-[0-9A-Z]{6}$", generatedCode);
        await server.WithDb(async db =>
        {
            var cat = await db.Set<SensorCatalogItem>().SingleAsync(x => x.SensorCode == generatedCode);
            Assert.Equal(mac1.ToUpperInvariant(), cat.MacAddress);
            Assert.False(cat.IsDemo);
        });

        // Caso A2: New MAC with explicit SensorCode -> auto-provisions SensorCatalogItem with that SensorCode and creates Device
        var createdWithCode = await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = mac2,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = code2,
            name = "  Sensor Norte  "
        });
        Assert.Equal(HttpStatusCode.Created, createdWithCode.StatusCode);
        var d2 = await Json(createdWithCode);
        Assert.Equal(code2, d2.GetProperty("sensorCode").GetString());
        Assert.Equal("Sensor Norte", d2.GetProperty("name").GetString());
        Assert.Equal(mac2.ToUpperInvariant(), d2.GetProperty("macAddress").GetString());
        await server.WithDb(async db =>
        {
            var cat = await db.Set<SensorCatalogItem>().SingleAsync(x => x.SensorCode == code2);
            Assert.Equal(mac2.ToUpperInvariant(), cat.MacAddress);
        });

        // Caso B: Existing compatible unassociated SensorCatalogItem is reused safely without duplication
        var preseededCode = await Catalog();
        string preseededMac = "";
        await server.WithDb(async db => preseededMac = (await db.Set<SensorCatalogItem>().SingleAsync(x => x.SensorCode == preseededCode)).MacAddress);
        var reused = await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = preseededMac.ToLowerInvariant().Replace(':', '-'),
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = preseededCode,
            name = "Reused Catalog Sensor"
        });
        Assert.Equal(HttpStatusCode.Created, reused.StatusCode);
        var dReused = await Json(reused);
        Assert.Equal(preseededCode, dReused.GetProperty("sensorCode").GetString());
        await server.WithDb(async db =>
        {
            Assert.Equal(1, await db.Set<SensorCatalogItem>().CountAsync(x => x.SensorCode == preseededCode));
            Assert.True((await db.Set<SensorCatalogItem>().SingleAsync(x => x.SensorCode == preseededCode)).IsDemo);
        });

        // Caso C: Duplicate MAC with a different SensorCode -> 409 Conflict (MAC_ADDRESS_CONFLICT)
        var unassociatedCode = await Catalog();
        string unassociatedMac = "";
        await server.WithDb(async db => unassociatedMac = (await db.Set<SensorCatalogItem>().SingleAsync(x => x.SensorCode == unassociatedCode)).MacAddress);
        await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = unassociatedMac,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = codeOther
        }), HttpStatusCode.Conflict, "MAC_ADDRESS_CONFLICT");

        // Caso D: SensorCode associated with another MAC -> 409 Conflict (SENSOR_CODE_CONFLICT)
        await AssertProblem(await a.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = a.fieldId,
            macAddress = mac3,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow,
            sensorCode = unassociatedCode
        }), HttpStatusCode.Conflict, "SENSOR_CODE_CONFLICT");

        // Already registered MAC or occupied SensorCode -> 409 Conflict
        await AssertProblem(await b.client.PostAsJsonAsync("/api/v1/devices", new
        {
            fieldId = b.fieldId,
            macAddress = mac2.ToUpperInvariant(),
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow
        }), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeviceRegistrationFailureRollsBackCatalogProvisioningAtomically()
    {
        var a = await Account();
        Field existingField = null!;
        await server.WithDb(async db => existingField = await db.Set<Field>().IgnoreQueryFilters().SingleAsync(f => f.Id == a.fieldId));

        var codeRepoFail = "TT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var macRepoFail = ("02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("X2")))).ToUpperInvariant();

        // Caso E1: Failure in IDeviceRepository.AddAsync after SensorCatalogItem is staged/saved inside transaction
        using (var scope = server.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var sensors = sp.GetRequiredService<ISensorDataRepository>();
            var innerDevices = sp.GetRequiredService<IDeviceRepository>();
            var uow = sp.GetRequiredService<IUnitOfWork>();
            var service = new SensorRegistrationService(sensors, new StubFieldRepository(existingField), new FailingDeviceRepository(innerDevices, uow), uow);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.Register(codeRepoFail, macRepoFail, a.fieldId, "Rollback Test", "ONLINE", DateTimeOffset.UtcNow, CancellationToken.None));
        }

        await server.WithDb(async db =>
        {
            Assert.False(await db.Set<SensorCatalogItem>().AnyAsync(x => x.SensorCode == codeRepoFail || x.MacAddress == macRepoFail));
            Assert.False(await db.Set<Device>().IgnoreQueryFilters().AnyAsync(x => x.SensorCode == codeRepoFail));
        });

        // Caso E2: Relational FK violation when inserting Device after SensorCatalogItem insert in SaveChangesAsync
        var codeDbFail = "TT-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var macDbFail = ("02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("X2")))).ToUpperInvariant();
        using (var scope = server.Services.CreateScope())
        {
            var sp = scope.ServiceProvider;
            var sensors = sp.GetRequiredService<ISensorDataRepository>();
            var devices = sp.GetRequiredService<IDeviceRepository>();
            var uow = sp.GetRequiredService<IUnitOfWork>();
            var service = new SensorRegistrationService(sensors, new StubFieldRepository(existingField), devices, uow);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                service.Register(codeDbFail, macDbFail, int.MaxValue - 1, "FK Rollback Test", "ONLINE", DateTimeOffset.UtcNow, CancellationToken.None));
        }

        await server.WithDb(async db =>
        {
            Assert.False(await db.Set<SensorCatalogItem>().AnyAsync(x => x.SensorCode == codeDbFail || x.MacAddress == macDbFail));
            Assert.False(await db.Set<Device>().IgnoreQueryFilters().AnyAsync(x => x.SensorCode == codeDbFail));
        });
    }

    [Fact]
    public async Task ConcurrentDirectDeviceRegistrationsEnforceUniqueness()
    {
        var a = await Account();
        var sharedMac = "02:" + string.Join(":", Guid.NewGuid().ToByteArray().Take(5).Select(x => x.ToString("X2")));
        var payload = new
        {
            fieldId = a.fieldId,
            macAddress = sharedMac,
            status = "ONLINE",
            lastSync = DateTimeOffset.UtcNow
        };

        var macAttempts = await Task.WhenAll(
            a.client.PostAsJsonAsync("/api/v1/devices", payload),
            a.client.PostAsJsonAsync("/api/v1/devices", payload),
            a.client.PostAsJsonAsync("/api/v1/devices", payload));

        Assert.Single(macAttempts, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Equal(2, macAttempts.Count(x => x.StatusCode == HttpStatusCode.Conflict));

        await server.WithDb(async db =>
        {
            Assert.Equal(1, await db.Set<SensorCatalogItem>().CountAsync(x => x.MacAddress == sharedMac));
            Assert.Equal(1, await db.Set<Device>().IgnoreQueryFilters().CountAsync(x => x.MacAddress.Value == sharedMac));
        });
    }

    private sealed class FailingDeviceRepository(IDeviceRepository inner, IUnitOfWork uow) : IDeviceRepository
    {
        public async Task AddAsync(Device entity, CancellationToken cancellationToken = default)
        {
            // Flush the staged SensorCatalogItem inside the active DB transaction before failing on Device
            await uow.CompleteAsync(cancellationToken);
            throw new InvalidOperationException("Simulated failure while creating Device.");
        }

        public void Detach(Device device) => inner.Detach(device);
        public Task<bool> ExistsByMacAddressAsync(MacAddress macAddress, CancellationToken cancellationToken = default) => inner.ExistsByMacAddressAsync(macAddress, cancellationToken);
        public Task<bool> ExistsBySensorCodeAsync(string sensorCode, CancellationToken cancellationToken = default) => inner.ExistsBySensorCodeAsync(sensorCode, cancellationToken);
        public Task<IEnumerable<Device>> FindByFieldIdAsync(FieldId fieldId, CancellationToken cancellationToken = default) => inner.FindByFieldIdAsync(fieldId, cancellationToken);
        public Task<Device?> FindByIdAsync(int id, CancellationToken cancellationToken = default) => inner.FindByIdAsync(id, cancellationToken);
        public Task<Device?> FindByMacAddressAsync(MacAddress macAddress, CancellationToken cancellationToken = default) => inner.FindByMacAddressAsync(macAddress, cancellationToken);
        public Task<IEnumerable<Device>> FindByStatusAsync(DeviceStatus status, CancellationToken cancellationToken = default) => inner.FindByStatusAsync(status, cancellationToken);
        public Task<IEnumerable<Device>> ListAsync(CancellationToken cancellationToken = default) => inner.ListAsync(cancellationToken);
        public void Remove(Device entity) => inner.Remove(entity);
        public void Update(Device entity) => inner.Update(entity);
    }

    private sealed class StubFieldRepository(Field field) : IFieldRepository
    {
        public Task AddAsync(Field entity, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Field?> FindByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<Field?>(field);
        public Task<Field?> FindBySoilTypeAndLocationLatLongAsync(SoilType SoilType, LocationLatLong LocationLatLong, CancellationToken cancellationToken = default) => Task.FromResult<Field?>(null);
        public Task<IEnumerable<Field>> FindBySoilTypeAsync(SoilType soilType, CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Field>>([]);
        public Task<IEnumerable<Field>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<Field>>([field]);
        public void Remove(Field entity) { }
        public void Update(Field entity) { }
    }
}
