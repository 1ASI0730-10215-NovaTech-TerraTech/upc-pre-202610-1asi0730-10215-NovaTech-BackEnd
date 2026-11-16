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
public class DeviceCommandService(IDeviceRepository deviceRepository, IFieldRepository fieldRepository, IUnitOfWork unitOfWork, ILogger<DeviceCommandService> logger) : IDeviceCommandService
{
    private static readonly System.Text.RegularExpressions.Regex SensorCodeRegex = new("^TT-[0-9A-Z]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<Result<Device>> Handle(CreateDeviceCommand command, CancellationToken cancellationToken = default)
    {
        var normalizedMac = command.MacAddress.Normalize();
        if (command.SensorCode is not null && !SensorCodeRegex.IsMatch(command.SensorCode))
            throw new ApiFailure(400, "INVALID_SENSOR_CODE", "Sensor code must match format TT-XXXXXX.");
        if (command.Name is not null && (string.IsNullOrWhiteSpace(command.Name) || command.Name.Trim().Length > 100))
            throw new ApiFailure(400, "INVALID_NAME", "Device name must be between 1 and 100 characters.");
        if (await fieldRepository.FindByIdAsync(command.FieldId.Value, cancellationToken)is null)
            throw new ApiFailure(404, "FIELD_NOT_FOUND", "Field was not found.");
        if (await deviceRepository.ExistsByMacAddressAsync(normalizedMac, cancellationToken))
            throw new ApiFailure(409, "DEVICE_ALREADY_REGISTERED", "A device with this MAC address is already registered.");
        if (command.SensorCode is not null && await deviceRepository.ExistsBySensorCodeAsync(command.SensorCode, cancellationToken))
            throw new ApiFailure(409, "SENSOR_OCCUPIED", "Sensor is already associated.");

        try
        {
            var device = new Device(command with { MacAddress = normalizedMac });
            await deviceRepository.AddAsync(device, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<Device>.Success(device);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid arguments while creating device with MAC {MacAddress}", normalizedMac);
            return Result<Device>.Failure(CreateDeviceError.InvalidData, ex.Message);
        }
        catch (DbUpdateException ex)when (DatabaseErrors.IsUniqueViolation(ex))
        {
            var codeOccupied = command.SensorCode is not null && await deviceRepository.ExistsBySensorCodeAsync(command.SensorCode, cancellationToken);
            throw new ApiFailure(409, codeOccupied ? "SENSOR_OCCUPIED" : "DEVICE_ALREADY_REGISTERED", codeOccupied ? "Sensor is already associated." : "A device with this MAC address is already registered.");
        }
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
}
