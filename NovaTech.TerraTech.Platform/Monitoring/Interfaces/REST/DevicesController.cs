using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NovaTech.TerraTech.Platform.Monitoring.Application.Errors;
using NovaTech.TerraTech.Platform.Monitoring.Application.Services;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Transform;
using NovaTech.TerraTech.Platform.Shared.Resources;
using Swashbuckle.AspNetCore.Annotations;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST;

[ApiController]
[Route("api/v1/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
[Tags("Monitoring")]
public class DevicesController(
    IDeviceCommandService deviceCommandService,
    IDeviceQueryService deviceQueryService,
    IStringLocalizer<CommonMessages> localizer,
    ILogger<DevicesController> logger)
    : ControllerBase
{
    [HttpPost]
    [SwaggerOperation(Summary = "Creates a Device", OperationId = "CreateDevice")]
    [SwaggerResponse(201, "The Device was created", typeof(DeviceResource))]
    [SwaggerResponse(400, "The request payload is invalid", typeof(string))]
    [SwaggerResponse(409, "A device with this MAC address already exists", typeof(string))]
    [SwaggerResponse(500, "Unexpected server error", typeof(ProblemDetails))]
    public async Task<ActionResult> CreateDevice([FromBody] CreateDeviceResource resource, CancellationToken cancellationToken)
    {
        try
        {
            var command = CreateDeviceCommandFromResourceAssembler.ToCommandFromResource(resource);
            var result = await deviceCommandService.Handle(command, cancellationToken);
            return ActionResultFromCreateDeviceResultAssembler.ToActionResultFromCreateDeviceResult(
                result, this, localizer, nameof(GetDeviceById));
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Validation failed while creating device with MAC {MacAddress}", resource.MacAddress);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while creating device with MAC {MacAddress}", resource.MacAddress);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorCreatingDevice"].Value, statusCode: 500);
        }
    }

    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Updates a device", OperationId = "UpdateDevice")]
    [SwaggerResponse(200, "The device was updated", typeof(DeviceResource))]
    [SwaggerResponse(400, "Invalid request data", typeof(string))]
    [SwaggerResponse(404, "Device not found", typeof(string))]
    [SwaggerResponse(409, "MAC address already in use", typeof(string))]
    [SwaggerResponse(500, "Unexpected server error", typeof(ProblemDetails))]
    public async Task<ActionResult> UpdateDevice(
        [FromRoute] int id,
        [FromBody] UpdateDeviceResource resource,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = UpdateDeviceCommandFromResourceAssembler.ToCommandFromResource(id, resource);
            var result = await deviceCommandService.Handle(command, cancellationToken);
            
            if (result.IsFailure)
            {
                return result.Error switch
                {
                    CreateDeviceError.InvalidData => NotFound(new { error = result.Message }),
                    CreateDeviceError.DuplicateDevice => Conflict(new { error = result.Message }),
                    _ => Problem(title: localizer["UnexpectedServerError"].Value, detail: result.Message, statusCode: 500)
                };
            }
            
            var resourceResponse = DeviceResourceFromEntityAssembler.ToResourceFromEntity(result.Value);
            return Ok(resourceResponse);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Validation failed while updating device {DeviceId}", id);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while updating device {DeviceId}", id);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorUpdatingDevice"].Value, statusCode: 500);
        }
    }

    [HttpGet]
    [SwaggerOperation(Summary = "Gets all devices", OperationId = "GetAllDevices")]
    [SwaggerResponse(200, "List of all devices", typeof(IEnumerable<DeviceResource>))]
    public async Task<ActionResult<IEnumerable<DeviceResource>>> GetAllDevices(CancellationToken cancellationToken)
    {
        try
        {
            var devices = await deviceQueryService.GetAllDevicesAsync(cancellationToken);
            var resources = devices.Select(DeviceResourceFromEntityAssembler.ToResourceFromEntity);
            return Ok(resources);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while retrieving all devices");
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorProcessingRequest"].Value, statusCode: 500);
        }
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Gets a device by id", OperationId = "GetDeviceById")]
    [SwaggerResponse(200, "The device was found", typeof(DeviceResource))]
    [SwaggerResponse(404, "The device was not found")]
    public async Task<ActionResult> GetDeviceById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var device = await deviceQueryService.GetDeviceByIdAsync(id, cancellationToken);
            if (device is null) return NotFound();
            var resource = DeviceResourceFromEntityAssembler.ToResourceFromEntity(device);
            return Ok(resource);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while retrieving device with id {DeviceId}", id);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorProcessingRequest"].Value, statusCode: 500);
        }
    }

    [HttpGet("field/{fieldId:int}")]
    [SwaggerOperation(Summary = "Gets devices by field id", OperationId = "GetDevicesByFieldId")]
    [SwaggerResponse(200, "List of devices for the field", typeof(IEnumerable<DeviceResource>))]
    [SwaggerResponse(404, "Field not found")]
    public async Task<ActionResult<IEnumerable<DeviceResource>>> GetDevicesByFieldId(int fieldId, CancellationToken cancellationToken)
    {
        try
        {
            var devices = await deviceQueryService.GetDevicesByFieldIdAsync(fieldId, cancellationToken);
            var resources = devices.Select(DeviceResourceFromEntityAssembler.ToResourceFromEntity);
            return Ok(resources);
        }
        catch (ArgumentException)
        {
            return NotFound(new { error = localizer["FieldNotFound"].Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while retrieving devices for field {FieldId}", fieldId);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorProcessingRequest"].Value, statusCode: 500);
        }
    }
}