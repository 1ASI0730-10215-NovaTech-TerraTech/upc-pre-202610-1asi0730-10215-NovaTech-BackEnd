using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;

namespace NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Commands;
public record CreateDeviceCommand(FieldId FieldId, MacAddress MacAddress, DeviceStatus Status, DateTimeOffset LastSync, string? SensorCode = null, string? Name = null, int? BatteryLevelPercent = null);
