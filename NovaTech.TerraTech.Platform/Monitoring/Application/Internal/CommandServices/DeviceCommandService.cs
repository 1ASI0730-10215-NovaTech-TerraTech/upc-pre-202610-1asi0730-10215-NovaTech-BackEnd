using Microsoft.EntityFrameworkCore;
using NovaTech.TerraTech.Platform.Monitoring.Application.Errors;
using NovaTech.TerraTech.Platform.Monitoring.Application.Services;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Commands;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Repositories;
using NovaTech.TerraTech.Platform.Shared.Application.Model;
using NovaTech.TerraTech.Platform.Shared.Domain.Repositories;

namespace NovaTech.TerraTech.Platform.Monitoring.Application.Internal.CommandServices;
public class DeviceCommandService(IDeviceRepository deviceRepository, IUnitOfWork unitOfWork, ILogger<DeviceCommandService> logger, SensorRegistrationService registration) : IDeviceCommandService
{
    public async Task<Result<Device>> Handle(CreateDeviceCommand command, CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(command.Name) ? "Sensor" : command.Name.Trim();
        var device = await registration.Register(command.SensorCode, command.MacAddress.Value, command.FieldId.Value, name, command.Status.Value, command.LastSync, cancellationToken, command.BatteryLevelPercent);
        return Result<Device>.Success(device);
    }

    public async Task<Result<Device>> Handle(UpdateDeviceCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var device = await deviceRepository.FindByIdAsync(command.Id, cancellationToken);
            if (device is null)
            {
                logger.LogWarning("Device with id {Id} not found for update", command.Id);
                return Result<Device>.Failure(CreateDeviceError.DeviceNotFound, $"Device with id {command.Id} not found.");
            }

            var normalizedMac = command.MacAddress.Normalize();
            var existingDevice = await deviceRepository.FindByMacAddressAsync(normalizedMac, cancellationToken);
            if (existingDevice is not null && existingDevice.Id != command.Id)
            {
                logger.LogWarning("MAC {MacAddress} already used by another device", normalizedMac);
                return Result<Device>.Failure(CreateDeviceError.DuplicateDevice, $"MAC address {normalizedMac} is already in use.");
            }

            if (device.MacAddress.Value.Replace('-', ':').ToUpperInvariant() != normalizedMac.Value)
                return Result<Device>.Failure(CreateDeviceError.InvalidData, "Registered MAC cannot be changed.");
            device.Update(command with { MacAddress = normalizedMac });
            deviceRepository.Update(device);
            await unitOfWork.CompleteAsync(cancellationToken);
            logger.LogInformation("Device {Id} updated successfully", command.Id);
            return Result<Device>.Success(device);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid arguments while updating device {Id}", command.Id);
            return Result<Device>.Failure(CreateDeviceError.InvalidData, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error updating device {Id}", command.Id);
            return Result<Device>.Failure(CreateDeviceError.UnexpectedError, ex.Message);
        }
    }

    public async Task<Result<Device>> Handle(DeleteDeviceCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var device = await deviceRepository.FindByIdAsync(command.Id, cancellationToken);
            if (device is null)
            {
                logger.LogWarning("Device with id {Id} not found for deletion", command.Id);
                return Result<Device>.Failure(CreateDeviceError.DeviceNotFound, $"Device with id {command.Id} not found.");
            }

            deviceRepository.Remove(device);
            await unitOfWork.CompleteAsync(cancellationToken);
            logger.LogInformation("Device {Id} deleted successfully", command.Id);
            return Result<Device>.Success(device);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error deleting device {Id}", command.Id);
            return Result<Device>.Failure(CreateDeviceError.UnexpectedError, ex.Message);
        }
    }

    private static bool IsDuplicateKeyViolation(DbUpdateException exception)
    {
        return DatabaseErrors.IsUniqueViolation(exception);
    }
}
