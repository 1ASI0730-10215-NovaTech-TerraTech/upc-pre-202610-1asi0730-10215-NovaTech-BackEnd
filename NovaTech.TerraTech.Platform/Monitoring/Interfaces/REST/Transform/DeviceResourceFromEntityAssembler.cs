using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Transform;

public static class DeviceResourceFromEntityAssembler
{
    public static DeviceResource ToResourceFromEntity(Device entity) =>
        new(
            entity.Id,
            entity.FieldId.Value,
            entity.MacAddress.Value,
            entity.Status.Value,
            entity.LastSync.Value
        );
}