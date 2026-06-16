namespace NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Commands;

/// <summary>
/// Command to delete an existing device by its ID.
/// </summary>
/// <param name="Id">Device identifier.</param>
public record DeleteDeviceCommand(int Id);