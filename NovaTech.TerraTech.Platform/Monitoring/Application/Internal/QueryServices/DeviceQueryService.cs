using NovaTech.TerraTech.Platform.Monitoring.Application.Services;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Repositories;

namespace NovaTech.TerraTech.Platform.Monitoring.Application.Internal.QueryServices;

public class DeviceQueryService(IDeviceRepository deviceRepository) : IDeviceQueryService
{
    public async Task<Device?> GetDeviceByIdAsync(int deviceId, CancellationToken cancellationToken = default)
        => await deviceRepository.FindByIdAsync(deviceId, cancellationToken);

    public async Task<IEnumerable<Device>> GetAllDevicesAsync(CancellationToken cancellationToken = default)
        => await deviceRepository.ListAsync(cancellationToken);

    public async Task<IEnumerable<Device>> GetDevicesByFieldIdAsync(int fieldId, CancellationToken cancellationToken = default)
        => await deviceRepository.FindByFieldIdAsync(new FieldId(fieldId), cancellationToken);
}