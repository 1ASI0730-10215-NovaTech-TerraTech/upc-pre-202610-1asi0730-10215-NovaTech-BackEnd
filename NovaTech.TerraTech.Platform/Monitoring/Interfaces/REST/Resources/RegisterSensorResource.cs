using System.ComponentModel.DataAnnotations;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;
public record RegisterSensorResource([Required(ErrorMessage = "Sensor code is required."), RegularExpression("^TT-[0-9A-Z]{6}$", ErrorMessage = "Sensor code must match format TT-XXXXXX.")] string SensorCode, [Range(1, int.MaxValue, ErrorMessage = "FieldId must be a positive integer.")] int FieldId, [Required(ErrorMessage = "Device name is required."), MaxLength(100, ErrorMessage = "Device name cannot exceed 100 characters.")] string Name);
