using NovaTech.TerraTech.Platform.CommunityManagement.Domain.Model.ValueObjects;

namespace NovaTech.TerraTech.Platform.CommunityManagement.Domain.Model.Commands;

public record CreateCommunityProfileCommand(
    string ProfileId,
    string Nickname,
    int ReputationScore,
    string PublicBio,
    VisibilityStatus VisibilityStatus
);