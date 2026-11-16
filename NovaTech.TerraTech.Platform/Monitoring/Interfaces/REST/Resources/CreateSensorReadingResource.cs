using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;

[SwaggerSchema(Description = "Request payload to register a sensor reading")]
public record CreateSensorReadingResource(
    [Required][Range(1, int.MaxValue, ErrorMessage = "DeviceId must be a positive integer.")][SwaggerParameter(Description = "Persistently registered device ID")] int DeviceId,
    [Required(ErrorMessage = "Moisture percent is required.")][Range(0, 100, ErrorMessage = "Moisture percent must be between 0 and 100.")][SwaggerParameter(Description = "Soil moisture percentage (0-100)")] double? MoisturePercent,
    [Required(ErrorMessage = "Soil temperature is required.")][SwaggerParameter(Description = "Soil temperature in Celsius")] double? SoilTemperatureC,
    [Required(ErrorMessage = "Nitrogen (ppm) is required.")][Range(0, double.MaxValue, ErrorMessage = "Nitrogen (ppm) must be zero or greater.")][SwaggerParameter(Description = "Nitrogen concentration in ppm (>= 0)")] double? NitrogenPpm,
    [Required(ErrorMessage = "Phosphorus (ppm) is required.")][Range(0, double.MaxValue, ErrorMessage = "Phosphorus (ppm) must be zero or greater.")][SwaggerParameter(Description = "Phosphorus concentration in ppm (>= 0)")] double? PhosphorusPpm,
    [Required(ErrorMessage = "Potassium (ppm) is required.")][Range(0, double.MaxValue, ErrorMessage = "Potassium (ppm) must be zero or greater.")][SwaggerParameter(Description = "Potassium concentration in ppm (>= 0)")] double? PotassiumPpm
);
