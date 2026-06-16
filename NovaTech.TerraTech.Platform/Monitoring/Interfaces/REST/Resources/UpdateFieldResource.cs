using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;

[SwaggerSchema(Description = "Request payload to update a Field")]
public record UpdateFieldResource(
    [Required]
    [MaxLength(100)]
    [SwaggerParameter(Description = "Field name (max 100 characters)")] string Name,
    
    [Required]
    [Range(0.01, 9999999)]
    [SwaggerParameter(Description = "Size in square meters")] double SizeM2,
    
    [Required]
    [MaxLength(50)]
    [SwaggerParameter(Description = "Soil type (max 50 characters)")] string SoilType,
    
    [Required]
    [SwaggerParameter(Description = "Latitude coordinate (-90 to 90)")] double Latitude,
    
    [Required]
    [SwaggerParameter(Description = "Longitude coordinate (-180 to 180)")] double Longitude
);