using Microsoft.EntityFrameworkCore;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Repositories;
using NovaTech.TerraTech.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using NovaTech.TerraTech.Platform.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;

namespace NovaTech.TerraTech.Platform.Monitoring.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
public class DeviceRepository(AppDbContext context) : BaseRepository<Device>(context), IDeviceRepository
{
    public async Task<IEnumerable<Device>> FindByFieldIdAsync(FieldId fieldId, CancellationToken cancellationToken = default)
    {
        return await Context.Set<Device>().Where(d => d.FieldId.Value == fieldId.Value).ToListAsync(cancellationToken);
    }

    public async Task<Device?> FindByMacAddressAsync(MacAddress macAddress, CancellationToken cancellationToken = default)
    {
        var normalized = macAddress.Value.Trim().Replace("-", ":").ToUpper();
        return await Context.Set<Device>().IgnoreQueryFilters().FirstOrDefaultAsync(d => d.MacAddress.Value.Replace("-", ":").ToUpper() == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByMacAddressAsync(MacAddress macAddress, CancellationToken cancellationToken = default)
    {
        var normalized = macAddress.Value.Trim().Replace("-", ":").ToUpper();
        return await Context.Set<Device>().IgnoreQueryFilters().AnyAsync(d => d.MacAddress.Value.Replace("-", ":").ToUpper() == normalized, cancellationToken);
    }

    public async Task<bool> ExistsBySensorCodeAsync(string sensorCode, CancellationToken cancellationToken = default)
    {
        return await Context.Set<Device>().IgnoreQueryFilters().AnyAsync(d => d.SensorCode == sensorCode, cancellationToken);
    }

    public async Task<IEnumerable<Device>> FindByStatusAsync(DeviceStatus status, CancellationToken cancellationToken = default)
    {
        return await Context.Set<Device>().Where(d => d.Status.Value == status.Value).ToListAsync(cancellationToken);
    }
}
