using Swashbuckle.AspNetCore.Annotations;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;

[SwaggerSchema(Description = "A Device resource")]
public record DeviceResource(
    [SwaggerParameter(Description = "Server-generated ID")] int Id,
    [SwaggerParameter(Description = "Field ID")] int FieldId,
    [SwaggerParameter(Description = "MAC address")] string MacAddress,
    [SwaggerParameter(Description = "Device status")] string Status,
    [SwaggerParameter(Description = "Last synchronization")] DateTimeOffset LastSync
);