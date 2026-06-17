using NovaTech.TerraTech.Platform.ProfileManagement.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.ProfileManagement.Domain.Model.Commands;

namespace NovaTech.TerraTech.Platform.ProfileManagement.Application.CommandServices;

/// <summary>
///     Profile command service interface
/// </summary>
public interface IProfileCommandService
{
    /// <summary>
    ///     Handle create profile command
    /// </summary>
    /// <param name="command">
    ///     The <see cref="CreateProfileCommand" /> command
    /// </param>
    /// <returns>
    ///     The <see cref="Profile" /> object with the created profile
    /// </returns>
    Task<Profile?> Handle(CreateProfileCommand command);
    
    Task<Profile?> Handle(UpdateProfileCommand command);
    
    Task Handle(DeleteProfileCommand command);
}