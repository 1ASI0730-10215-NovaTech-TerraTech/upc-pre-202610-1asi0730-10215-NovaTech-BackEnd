using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;

namespace NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Queries;

/// <summary>
/// Query to get all devices filtered by their status.
/// </summary>
/// <param name="Status">The device status to filter by (ONLINE, OFFLINE, LOW_BATTERY).</param>
public record GetDevicesByStatusQuery(DeviceStatus Status);