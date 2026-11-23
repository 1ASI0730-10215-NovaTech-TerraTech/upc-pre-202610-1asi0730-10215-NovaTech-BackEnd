namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;
public record LatestReadingResource(ReadingResource Reading, bool IsStale, int FieldId, string SensorCode, int? BatteryLevelPercent, int ElapsedMinutes);
