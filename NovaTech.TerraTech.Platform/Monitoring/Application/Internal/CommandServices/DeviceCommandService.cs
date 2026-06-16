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

public class DeviceCommandService(
    IDeviceRepository deviceRepository,
    IUnitOfWork unitOfWork,
    ILogger<DeviceCommandService> logger)
    : IDeviceCommandService
{
    public async Task<Result<Device>> Handle(CreateDeviceCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await deviceRepository.ExistsByMacAddressAsync(command.MacAddress, cancellationToken);
            if (exists)
            {
                logger.LogWarning("Device with MAC {MacAddress} already exists", command.MacAddress);
                return Result<Device>.Failure(
                    CreateDeviceError.DuplicateDevice,
                    $"A device with MAC address {command.MacAddress} already exists.");
            }
            
            var device = new Device(command);
            await deviceRepository.AddAsync(device, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
            
            logger.LogInformation("Device created successfully with ID {Id}", device.Id);
            return Result<Device>.Success(device);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid arguments while creating device");
            return Result<Device>.Failure(CreateDeviceError.InvalidData, ex.Message);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
        {
            logger.LogWarning(ex, "Duplicate key violation creating device");
            return Result<Device>.Failure(CreateDeviceError.DuplicateDevice, "Database duplicate key violation occurred.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating device");
            return Result<Device>.Failure(CreateDeviceError.UnexpectedError, ex.Message);
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
                return Result<Device>.Failure(
                    CreateDeviceError.InvalidData,
                    $"Device with id {command.Id} not found.");
            }
            
            var existingDevice = await deviceRepository.FindByMacAddressAsync(command.MacAddress, cancellationToken);
            if (existingDevice is not null && existingDevice.Id != command.Id)
            {
                logger.LogWarning("MAC {MacAddress} already used by another device", command.MacAddress);
                return Result<Device>.Failure(
                    CreateDeviceError.DuplicateDevice,
                    $"MAC address {command.MacAddress} is already in use.");
            }
            
            device.Update(command);
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

    private static bool IsDuplicateKeyViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (!string.Equals(current.GetType().Name, "MySqlException", StringComparison.Ordinal)) continue;
            var numberProperty = current.GetType().GetProperty("Number");
            if (numberProperty?.PropertyType == typeof(int) &&
                numberProperty.GetValue(current) is int errorCode &&
                errorCode == 1062)
                return true;
        }
        return false;
    }
}