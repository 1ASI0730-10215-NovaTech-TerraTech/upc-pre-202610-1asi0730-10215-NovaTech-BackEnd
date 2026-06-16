using System.Net.Mime;
// using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NovaTech.TerraTech.Platform.Iam.Application.QueryServices;
using NovaTech.TerraTech.Platform.Iam.Domain.Model.Queries;
using NovaTech.TerraTech.Platform.Iam.Interface.Rest.Resources;
using NovaTech.TerraTech.Platform.Iam.Interface.Rest.Transform;
using NovaTech.TerraTech.Platform.Shared.Interfaces.Rest.ProblemDetails;
using NovaTech.TerraTech.Platform.Shared.Resources.Errors;
using Swashbuckle.AspNetCore.Annotations;
using NovaTech.TerraTech.Platform.Iam.Infrastructure.Pipeline.Middleware.Attributes;

namespace NovaTech.TerraTech.Platform.Iam.Interface.Rest;

[Authorize]
[ApiController]
[Route("api/v1/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
[SwaggerTag("Available Users endpoints")]
public class UsersController(
    IUserQueryService userQueryService, IStringLocalizer<ErrorMessages> errorLocalizer,
    ProblemDetailsFactory problemDetailsFactory): ControllerBase
{
    private readonly IStringLocalizer<ErrorMessages> _errorLocalizer = errorLocalizer;
    private readonly ProblemDetailsFactory _problemDetailsFactory = problemDetailsFactory;

    [HttpGet("{id:int}")]
    [SwaggerOperation(
        Summary = "Get a user by its id",
        Description = "Get a user by its id",
        OperationId = "GetUserById")]
    [SwaggerResponse(StatusCodes.Status200OK, "The user was found", typeof(UserResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The user was not found")]
    public async Task<IActionResult> GetUserById(int id, CancellationToken cancellationToken)
    {
        var getUserByIdQuery = new GetUserByIdQuery(id);
        var user = await userQueryService.Handle(getUserByIdQuery, cancellationToken);
        
        return IamActionResultAssembler.ToActionResultFromGetUserByIdResult(
            this,
            user,
            _errorLocalizer,
            _problemDetailsFactory,
            foundUser => Ok(UserResourceFromEntityAssembler.ToResourceFromEntity(foundUser))
        );
    }

    [HttpGet("email/{emailAddress}")]
    [SwaggerOperation(
        Summary = "Get a user by email",
        Description = "Get a user by email",
        OperationId = "GetUserByEmail")]
    [SwaggerResponse(StatusCodes.Status200OK, "The user was found", typeof(UserResource))]
    [SwaggerResponse(StatusCodes.Status404NotFound, "The user was not found")]
    public async Task<IActionResult> GetUserByEmail(string emailAddress, CancellationToken cancellationToken)
    {
        var getUserByEmailQuery = new GetUserByEmailQuery(emailAddress);
        var user = await userQueryService.Handle(getUserByEmailQuery, cancellationToken);
        
        return IamActionResultAssembler.ToActionResultFromGetUserByEmailResult(
            this,
            user,
            _errorLocalizer,
            _problemDetailsFactory,
            foundUser => Ok(UserResourceFromEntityAssembler.ToResourceFromEntity(foundUser))
        );
    }
}