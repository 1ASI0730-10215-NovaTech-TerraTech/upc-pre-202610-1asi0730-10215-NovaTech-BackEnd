using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Mvc;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Application.Internal.CommandServices;
using NovaTech.TerraTech.Platform.Monitoring.Application.Internal.QueryServices;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST;
[ApiController, Route("api/v1/devices")]
public class SensorReadingsController(SensorReadingQueryService queries, SensorReadingCommandService commands) : ControllerBase
{
    [HttpPost("~/api/v1/sensor-readings")]
    [ProducesResponseType<ReadingResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(Summary = "Register a sensor reading", Description = "Registers a sensor reading for an existing device owned by the authenticated user with Source = MANUAL and evaluates fertility zone alert thresholds.", OperationId = "CreateSensorReading")]
    public async Task<ActionResult<ReadingResource>> CreateReading([FromBody] CreateSensorReadingResource r, CancellationToken ct)
    {
        var reading = await commands.Register(r.DeviceId, r.MoisturePercent!.Value, r.SoilTemperatureC!.Value, r.NitrogenPpm!.Value, r.PhosphorusPpm!.Value, r.PotassiumPpm!.Value, ct, "MANUAL");
        return Created($"/api/v1/devices/{reading.DeviceId}/readings/{reading.Id}", Resource(reading));
    }

    [HttpGet("{id:int}/readings/latest")]
    [ProducesResponseType<LatestReadingResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Latest soil reading", Description = "UTC timestamp; moisture in %, soil temperature in °C, N/P/K in ppm. SIMULATED identifies demo data. isStale becomes true after 30 minutes; NO_READINGS is returned when empty.")]
    public async Task<ActionResult<LatestReadingResource>> Latest(int id, CancellationToken ct)
    {
        var(reading, isStale) = await queries.Latest(id, ct);
        return new LatestReadingResource(Resource(reading), isStale);
    }

    [HttpGet("{id:int}/readings")]
    [ProducesResponseType<ReadingHistoryResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "7 or 30 day history", Description = "Default 7 days. Chronological UTC range, minimum moisture reference in %, and readings. Empty collection when no measurements. Independent of statistical reports.")]
    public async Task<ActionResult<ReadingHistoryResource>> History(int id, [FromQuery] int days = 7, CancellationToken ct = default)
    {
        var(from, to, minimumMoisture, readings) = await queries.History(id, days, ct);
        return new ReadingHistoryResource(id, from, to, minimumMoisture, readings.Select(Resource).ToList());
    }

    [HttpGet("{deviceId:int}/readings/{readingId:int}")]
    [ProducesResponseType<ReadingResource>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(Summary = "Reading detail", Description = "A persisted reading belonging to an owned device, including readings older than 30 days. Returns READING_NOT_FOUND for missing or mismatched readings. Same units and resource as history.")]
    public async Task<ActionResult<ReadingResource>> Detail(int deviceId, int readingId, CancellationToken ct)
    {
        return Resource(await queries.Detail(deviceId, readingId, ct));
    }

    private static ReadingResource Resource(SensorReading x) => new(x.Id, x.DeviceId, DateTime.SpecifyKind(x.RecordedAt, DateTimeKind.Utc), x.MoisturePercent, x.SoilTemperatureC, x.NitrogenPpm, x.PhosphorusPpm, x.PotassiumPpm, x.Source);
}
