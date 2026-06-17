using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;

namespace NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Queries;

/// <summary>
/// Query to get fields filtered by their soil type.
/// </summary>
/// <param name="SoilType">The type of soil (e.g., Sandy, Clay-loam, etc.).</param>
public record GetFieldBySoilTypeQuery(SoilType SoilType);