namespace NovaTech.TerraTech.Platform.CommunityManagement.Domain.Model.Commands;

public record CreateCommentCommand(
    string AuthorProfileId,
    string TargetProfileId,
    string Content,
    int Rating
);