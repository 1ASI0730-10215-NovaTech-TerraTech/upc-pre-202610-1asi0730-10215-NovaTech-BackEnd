using Swashbuckle.AspNetCore.Annotations;

namespace NovaTech.TerraTech.Platform.ProfileManagement.Interfaces.REST.Resources;

[SwaggerSchema(Description = "Profile resource")]
public record ProfileResource(
    [SwaggerParameter(Description = "Profile identifier")] int Id,
    [SwaggerParameter(Description = "User identifier")] string UserId,
    [SwaggerParameter(Description = "Name of the fundo/farm")] string FundoName,
    [SwaggerParameter(Description = "Contact phone number")] string ContactPhone,
    [SwaggerParameter(Description = "Moisture threshold")] double MoistureThreshold,
    [SwaggerParameter(Description = "Temperature threshold")] double TempThreshold
);