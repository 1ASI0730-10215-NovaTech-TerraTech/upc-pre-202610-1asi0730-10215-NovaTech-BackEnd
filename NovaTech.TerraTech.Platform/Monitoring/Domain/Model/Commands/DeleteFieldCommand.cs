namespace NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Commands;

/// <summary>
/// Command to delete an existing field by its ID.
/// </summary>
/// <param name="Id">Field identifier.</param>
public record DeleteFieldCommand(int Id);