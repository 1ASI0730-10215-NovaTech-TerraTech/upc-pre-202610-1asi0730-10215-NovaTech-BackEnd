using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using NovaTech.TerraTech.Platform.Monitoring.Application.Errors;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Shared.Application.Model;
using NovaTech.TerraTech.Platform.Shared.Resources;
using NovaTech.TerraTech.Platform.Shared.Tb1;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Transform;
public static class ActionResultFromCreateDeviceResultAssembler
{
    public static ActionResult ToActionResultFromCreateDeviceResult(Result<Device> result, ControllerBase controller, IStringLocalizer<CommonMessages> localizer, string getDeviceByIdActionName) => result switch
    {
        var success when success.IsSuccess => controller.CreatedAtAction(getDeviceByIdActionName, new { id = success.Value!.Id }, DeviceResourceFromEntityAssembler.ToResourceFromEntity(success.Value!)),
        var failure when failure.IsFailure => failure.Error switch
        {
            CreateDeviceError.DuplicateDevice => ApiFailure.Result(409, "DEVICE_ALREADY_REGISTERED", failure.Message ?? localizer["DeviceAlreadyExists"].Value),
            CreateDeviceError.FieldNotFound => ApiFailure.Result(404, "FIELD_NOT_FOUND", failure.Message ?? localizer["FieldNotFound"].Value),
            CreateDeviceError.InvalidMacAddress => ApiFailure.Result(400, "INVALID_MAC_ADDRESS", failure.Message ?? "MAC address must be in format XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX."),
            CreateDeviceError.InvalidData or CreateDeviceError.InvalidStatus or CreateDeviceError.InvalidLastSync => ApiFailure.Result(400, "INVALID_INPUT", failure.Message ?? "Invalid device data."),
            _ => controller.Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorCreatingDevice"].Value, statusCode: 500)},
        _ => controller.Problem(title: localizer["UnexpectedServerError"].Value, detail: localizer["UnexpectedErrorProcessingRequest"].Value, statusCode: 500)};
}
