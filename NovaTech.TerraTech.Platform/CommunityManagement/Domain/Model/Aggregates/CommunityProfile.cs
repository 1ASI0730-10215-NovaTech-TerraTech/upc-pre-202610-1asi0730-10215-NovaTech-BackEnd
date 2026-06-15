using NovaTech.TerraTech.Platform.CommunityManagement.Domain.Model.Commands;
using NovaTech.TerraTech.Platform.CommunityManagement.Domain.Model.ValueObjects;

namespace NovaTech.TerraTech.Platform.CommunityManagement.Domain.Model.Aggregates;

public partial class CommunityProfile
{
    protected CommunityProfile() { }

    public CommunityProfile(CreateCommunityProfileCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        ProfileId = command.ProfileId;
        Nickname = new CommunityNickname(command.Nickname);
        ReputationScore = new Reputation(command.ReputationScore);
        PublicBio = new PublicBio(command.PublicBio);
        VisibilityStatus = command.VisibilityStatus; 
    }

    public int Id { get; private set; }
    public string ProfileId { get; private set; }
    
    public CommunityNickname Nickname { get; private set; }
    public Reputation ReputationScore { get; private set; }
    public PublicBio PublicBio { get; private set; }
    
    
    public VisibilityStatus VisibilityStatus { get; private set; }
    
    public void UpdateInformation(string nickname, string publicBio, VisibilityStatus visibilityStatus)
    {
        Nickname = new CommunityNickname(nickname);
        PublicBio = new PublicBio(publicBio);
        VisibilityStatus = visibilityStatus;
    }
    
}