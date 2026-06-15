using Swashbuckle.AspNetCore.Annotations;

namespace NovaTech.TerraTech.Platform.CommunityManagement.Interfaces.REST.Resources;

[SwaggerSchema(Description = "Comment resource")]
public record CommentResource(
    [SwaggerParameter(Description = "Comment database identifier")] int Id,
    [SwaggerParameter(Description = "Author profile identifier")] string AuthorProfileId,
    [SwaggerParameter(Description = "Target profile identifier")] string TargetProfileId,
    [SwaggerParameter(Description = "Content of the comment")] string Content,
    [SwaggerParameter(Description = "Rating score (0-5)")] int Rating,
    [SwaggerParameter(Description = "Creation timestamp")] DateTimeOffset? CreatedAt
);