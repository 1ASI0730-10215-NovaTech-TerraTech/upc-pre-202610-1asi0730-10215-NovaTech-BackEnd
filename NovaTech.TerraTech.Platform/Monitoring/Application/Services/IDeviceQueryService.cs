using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;

namespace NovaTech.TerraTech.Platform.Monitoring.Application.Services;

public interface IDeviceQueryService
{
    Task<Device?> GetDeviceByIdAsync(int deviceId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Device>> GetAllDevicesAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Device>> GetDevicesByFieldIdAsync(int fieldId, CancellationToken cancellationToken = default);
}