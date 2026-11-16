using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NovaTech.TerraTech.Platform.Monitoring.Application.Internal.CommandServices;
using NovaTech.TerraTech.Platform.Monitoring.Application.Services;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Commands;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Resources;
using NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Transform;
using NovaTech.TerraTech.Platform.Shared.Resources;
using NovaTech.TerraTech.Platform.Shared.Tb1;
using Swashbuckle.AspNetCore.Annotations;
using NovaTech.TerraTech.Platform.Monitoring.Application.Errors;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST;
[ApiController]
[Route("api/v1/[controller]")]
[Produces(MediaTypeNames.Application.Json)]
[Tags("Monitoring")]
public class DevicesController(IDeviceCommandService deviceCommandService, IDeviceQueryService deviceQueryService, SensorRegistrationService registration, IStringLocalizer<CommonMessages> localizer, ILogger<DevicesController> logger) : ControllerBase
{
    [HttpPost]
    [SwaggerOperation(Summary = "Creates a Device", Description = "Registers a new IoT device associated with an owned field. Does not require prior provisioning in SensorCatalogItem. Supports an optional TT-XXXXXX sensor code and display name.", OperationId = "CreateDevice")]
    [SwaggerResponse(201, "The Device was created", typeof(DeviceResource))]
    [SwaggerResponse(400, "The request payload is invalid", typeof(ProblemDetails))]
    [SwaggerResponse(404, "The specified field was not found", typeof(ProblemDetails))]
    [SwaggerResponse(409, "A device with this MAC address or sensor code already exists", typeof(ProblemDetails))]
    [SwaggerResponse(500, "Unexpected server error", typeof(ProblemDetails))]
    public async Task<ActionResult> CreateDevice([FromBody] CreateDeviceResource resource, CancellationToken cancellationToken)
    {
        try
        {
            var command = CreateDeviceCommandFromResourceAssembler.ToCommandFromResource(resource);
            var result = await deviceCommandService.Handle(command, cancellationToken);
            return ActionResultFromCreateDeviceResultAssembler.ToActionResultFromCreateDeviceResult(result, this, localizer, nameof(GetDeviceById));
        }
        catch (ApiFailure)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Validation failed while creating device with MAC {MacAddress}", resource.MacAddress);
            var code = ex.Message.Contains("Sensor code", StringComparison.OrdinalIgnoreCase)
                ? "INVALID_SENSOR_CODE"
                : ex.Message.Contains("MAC", StringComparison.OrdinalIgnoreCase)
                    ? "INVALID_MAC_ADDRESS"
                    : "INVALID_INPUT";
            return ApiFailure.Result(400, code, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while creating device with MAC {MacAddress}", resource.MacAddress);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorCreatingDevice"].Value, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [Obsolete("Legacy compatibility endpoint for Android clients. Prefer POST /api/v1/devices.")]
    [HttpPost("register")]
    [ProducesResponseType<DeviceResource>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(Summary = "Associate a provisioned sensor (Legacy / Deprecated)", Description = "Deprecated compatibility endpoint retained for legacy Android clients. TT-XXXXXX must already exist in the provisioned catalog and be available to resolve its hardware MAC address. New integrations should use POST /api/v1/devices.", OperationId = "RegisterProvisionedDevice")]
    public async Task<ActionResult<DeviceResource>> Register(RegisterSensorResource r, CancellationToken ct)
    {
        var device = await registration.Register(r.SensorCode, null, r.FieldId, r.Name, ct: ct);
        return Created($"/api/v1/devices/{device.Id}", DeviceResourceFromEntityAssembler.ToResourceFromEntity(device));
    }

    [HttpGet]
    [SwaggerOperation(Summary = "Gets all devices", Description = "Retrieves all available devices", OperationId = "GetAllDevices")]
    [SwaggerResponse(200, "List of all devices", typeof(IEnumerable<DeviceResource>))]
    public async Task<ActionResult<IEnumerable<DeviceResource>>> GetAllDevices(CancellationToken cancellationToken)
    {
        try
        {
            var resources = await deviceQueryService.GetAllDeviceResourcesAsync(cancellationToken);
            return Ok(resources);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while retrieving all devices");
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorProcessingRequest"].Value, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Gets a device by id", Description = "Retrieves a specific device by its identifier", OperationId = "GetDeviceById")]
    [SwaggerResponse(200, "The device was found", typeof(DeviceResource))]
    [SwaggerResponse(404, "The device was not found", typeof(ProblemDetails))]
    public async Task<ActionResult> GetDeviceById(int id, CancellationToken cancellationToken)
    {
        try
        {
            var device = await deviceQueryService.GetDeviceByIdAsync(id, cancellationToken);
            if (device is null)
                return ApiFailure.Result(404, "DEVICE_NOT_FOUND", "Device was not found.");
            var resource = DeviceResourceFromEntityAssembler.ToResourceFromEntity(device);
            return Ok(resource);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while retrieving device with id {DeviceId}", id);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorProcessingRequest"].Value, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("~/api/v1/fields/{fieldId:int}/devices")]
    [SwaggerOperation(Summary = "Gets devices by field id", Description = "Retrieves all devices belonging to a specific field", OperationId = "GetDevicesByFieldId")]
    [SwaggerResponse(200, "List of devices for the field", typeof(IEnumerable<DeviceResource>))]
    [SwaggerResponse(404, "Field not found", typeof(ProblemDetails))]
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
            return ApiFailure.Result(404, "FIELD_NOT_FOUND", localizer["FieldNotFound"].Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while retrieving devices for field {FieldId}", fieldId);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorProcessingRequest"].Value, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpGet("~/api/v1/status/{status}/devices")]
    [SwaggerOperation(Summary = "Gets devices by status", Description = "Retrieves all devices with a specific status (ONLINE, OFFLINE, LOW_BATTERY)", OperationId = "GetDevicesByStatus")]
    [SwaggerResponse(200, "List of devices with the specified status", typeof(IEnumerable<DeviceResource>))]
    [SwaggerResponse(400, "Invalid status value", typeof(ProblemDetails))]
    public async Task<ActionResult<IEnumerable<DeviceResource>>> GetDevicesByStatus([FromRoute] string status, CancellationToken cancellationToken)
    {
        try
        {
            var deviceStatus = DeviceStatus.Create(status);
            var devices = await deviceQueryService.GetDevicesByStatusAsync(deviceStatus, cancellationToken);
            var resources = devices.Select(DeviceResourceFromEntityAssembler.ToResourceFromEntity);
            return Ok(resources);
        }
        catch (ApiFailure)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid status value: {Status}", status);
            return ApiFailure.Result(400, "INVALID_DEVICE_STATUS", localizer["InvalidDeviceStatus"].Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while retrieving devices with status {Status}", status);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorProcessingRequest"].Value, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Updates a device", Description = "Updates an existing device's MAC, status, and last sync time", OperationId = "UpdateDevice")]
    [SwaggerResponse(200, "The device was updated", typeof(DeviceResource))]
    [SwaggerResponse(400, "Invalid request data", typeof(ProblemDetails))]
    [SwaggerResponse(404, "Device not found", typeof(ProblemDetails))]
    [SwaggerResponse(409, "MAC address already in use", typeof(ProblemDetails))]
    [SwaggerResponse(500, "Unexpected server error", typeof(ProblemDetails))]
    public async Task<ActionResult> UpdateDevice([FromRoute] int id, [FromBody] UpdateDeviceResource resource, CancellationToken cancellationToken)
    {
        try
        {
            var command = UpdateDeviceCommandFromResourceAssembler.ToCommandFromResource(id, resource);
            var result = await deviceCommandService.Handle(command, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error switch
                {
                    CreateDeviceError.DeviceNotFound => ApiFailure.Result(404, "DEVICE_NOT_FOUND", result.Message ?? "Device was not found."),
                    CreateDeviceError.InvalidMacAddress => ApiFailure.Result(400, "INVALID_MAC_ADDRESS", result.Message ?? "Invalid MAC address."),
                    CreateDeviceError.InvalidData => ApiFailure.Result(400, "INVALID_INPUT", result.Message ?? "Invalid device data."),
                    CreateDeviceError.DuplicateDevice => ApiFailure.Result(409, "DEVICE_ALREADY_REGISTERED", result.Message ?? "A device with this MAC address is already registered."),
                    _ => Problem(title: localizer["UnexpectedServerError"].Value, detail: result.Message, statusCode: StatusCodes.Status500InternalServerError)};
            }

            var resourceResponse = DeviceResourceFromEntityAssembler.ToResourceFromEntity(result.Value);
            return Ok(resourceResponse);
        }
        catch (ApiFailure)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Validation failed while updating device {DeviceId}", id);
            return ApiFailure.Result(400, "INVALID_INPUT", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while updating device {DeviceId}", id);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorUpdatingDevice"].Value, statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    [HttpDelete("{id:int}")]
    [SwaggerOperation(Summary = "Deletes a device", Description = "Deletes an existing device by its identifier", OperationId = "DeleteDevice")]
    [SwaggerResponse(204, "The device was deleted")]
    [SwaggerResponse(404, "Device not found", typeof(ProblemDetails))]
    [SwaggerResponse(409, "Device has dependent resources", typeof(ProblemDetails))]
    [SwaggerResponse(500, "Unexpected server error", typeof(ProblemDetails))]
    public async Task<ActionResult> DeleteDevice([FromRoute] int id, CancellationToken cancellationToken)
    {
        try
        {
            var command = new DeleteDeviceCommand(id);
            var result = await deviceCommandService.Handle(command, cancellationToken);
            if (result.IsFailure)
            {
                return result.Error switch
                {
                    CreateDeviceError.DeviceNotFound => ApiFailure.Result(404, "DEVICE_NOT_FOUND", localizer["DeviceNotFound"].Value),
                    _ => Problem(title: localizer["UnexpectedServerError"].Value, detail: result.Message, statusCode: StatusCodes.Status500InternalServerError)};
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error deleting device with id {DeviceId}", id);
            return Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorDeletingDevice"].Value, statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
