using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Transform;

public class FieldResourceFromEntityAssembler
{
    public static FieldResource ToResourceFromEntity(Field entity) =>
        new(
            entity.Id,
            entity.ProfileId.Value,
            entity.Name.Value,
            entity.SizeM2.Value,
            entity.SoilType.Value,
            entity.LocationLatLong.ToString()
        );
}