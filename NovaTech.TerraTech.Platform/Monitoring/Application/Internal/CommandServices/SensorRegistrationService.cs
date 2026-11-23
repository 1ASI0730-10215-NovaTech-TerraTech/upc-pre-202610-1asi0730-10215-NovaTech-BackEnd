using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Repositories;
using NovaTech.TerraTech.Platform.Shared.Domain.Repositories;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Commands;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;
using NovaTech.TerraTech.Platform.Shared.Tb1;

namespace NovaTech.TerraTech.Platform.Monitoring.Application.Internal.CommandServices;
public class SensorRegistrationService(ISensorDataRepository sensors, IFieldRepository fields, IDeviceRepository devices, IUnitOfWork unitOfWork)
{
    private static readonly Regex SensorCodeRegex = new("^TT-[0-9A-Z]{6}$", RegexOptions.Compiled);
    private const string Base36Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public async Task<Device> Register(string? code, string? mac, int fieldId, string name, string status = "OFFLINE", DateTimeOffset? lastSync = null, CancellationToken ct = default, int? batteryLevelPercent = null)
    {
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        if (normalizedCode is not null && !SensorCodeRegex.IsMatch(normalizedCode))
            throw new ApiFailure(400, "INVALID_SENSOR_CODE", "Sensor code must match format TT-XXXXXX.");

        MacAddress? macValue = null;
        if (!string.IsNullOrWhiteSpace(mac))
        {
            try
            {
                macValue = new MacAddress(mac.Trim()).Normalize();
            }
            catch (ArgumentException ex)
            {
                throw new ApiFailure(400, "INVALID_MAC_ADDRESS", ex.Message);
            }
        }

        var normalizedMac = macValue?.Value;
        if (normalizedCode is null && normalizedMac is null)
            throw new ApiFailure(400, "INVALID_INPUT", "Either sensor code or MAC address is required.");

        var normalizedName = string.IsNullOrWhiteSpace(name) ? "Sensor" : name.Trim();
        if (normalizedName.Length > 100)
            throw new ApiFailure(400, "INVALID_NAME", "Device name cannot exceed 100 characters.");

        DeviceStatus deviceStatus;
        try
        {
            deviceStatus = DeviceStatus.Create(status);
        }
        catch (ArgumentException ex)
        {
            throw new ApiFailure(400, "INVALID_DEVICE_STATUS", ex.Message);
        }

        if (fieldId <= 0 || await fields.FindByIdAsync(fieldId, ct) is null)
            throw new ApiFailure(404, "FIELD_NOT_FOUND", "Field was not found.");

        var sensorByCode = normalizedCode is not null ? await sensors.FindSensor(normalizedCode, null, ct) : null;
        var sensorByMac = normalizedMac is not null ? await sensors.FindSensor(null, normalizedMac, ct) : null;

        if (normalizedMac is null)
        {
            if (sensorByCode is null)
                throw new ApiFailure(400, "UNKNOWN_SENSOR", "Sensor must exist in the provisioned catalog.");

            var catalogMac = new MacAddress(sensorByCode.MacAddress).Normalize();
            if (await sensors.IsAssociated(sensorByCode.SensorCode, ct) || await devices.ExistsByMacAddressAsync(catalogMac, ct))
                throw new ApiFailure(409, "SENSOR_OCCUPIED", "Sensor is already associated.");

            var legacyDevice = new Device(new CreateDeviceCommand(new FieldId(fieldId), new MacAddress(sensorByCode.MacAddress), deviceStatus, lastSync ?? DateTimeOffset.UtcNow, sensorByCode.SensorCode, normalizedName, batteryLevelPercent));
            legacyDevice.Identify(sensorByCode.SensorCode, normalizedName);
            await devices.AddAsync(legacyDevice, ct);
            try
            {
                await unitOfWork.CompleteAsync(ct);
            }
            catch (DbUpdateException e) when (DatabaseErrors.IsUniqueViolation(e))
            {
                throw new ApiFailure(409, "SENSOR_OCCUPIED", "Sensor is already associated.");
            }

            return legacyDevice;
        }

        if (sensorByCode is not null)
        {
            var existingCodeMac = new MacAddress(sensorByCode.MacAddress).Normalize().Value;
            if (!string.Equals(existingCodeMac, normalizedMac, StringComparison.Ordinal))
                throw new ApiFailure(409, "SENSOR_CODE_CONFLICT", "Sensor code is already associated with another MAC address.");
        }

        if (sensorByMac is not null && normalizedCode is not null && !string.Equals(sensorByMac.SensorCode, normalizedCode, StringComparison.Ordinal))
            throw new ApiFailure(409, "MAC_ADDRESS_CONFLICT", "MAC address is already associated with another sensor code.");

        if (await devices.ExistsByMacAddressAsync(macValue!, ct))
            throw new ApiFailure(409, "DEVICE_ALREADY_REGISTERED", "A device with this MAC address is already registered.");

        var catalogItem = sensorByMac ?? sensorByCode;
        if (catalogItem is not null && await sensors.IsAssociated(catalogItem.SensorCode, ct))
            throw new ApiFailure(409, "SENSOR_OCCUPIED", "Sensor is already associated.");

        if (normalizedCode is not null && await sensors.IsAssociated(normalizedCode, ct))
            throw new ApiFailure(409, "SENSOR_OCCUPIED", "Sensor is already associated.");

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        SensorCatalogItem? createdCatalogItem = null;
        Device? device = null;
        try
        {
            if (catalogItem is null)
            {
                var resolvedCode = await ResolveNewSensorCodeAsync(normalizedCode, normalizedMac, ct);
                createdCatalogItem = new SensorCatalogItem
                {
                    SensorCode = resolvedCode,
                    MacAddress = normalizedMac,
                    IsDemo = false
                };
                await sensors.AddSensorAsync(createdCatalogItem, ct);
                catalogItem = createdCatalogItem;
            }

            device = new Device(new CreateDeviceCommand(new FieldId(fieldId), macValue!, deviceStatus, lastSync ?? DateTimeOffset.UtcNow, catalogItem.SensorCode, normalizedName, batteryLevelPercent));
            device.Identify(catalogItem.SensorCode, normalizedName);
            await devices.AddAsync(device, ct);
            await unitOfWork.CompleteAsync(ct);
            await transaction.CommitAsync(ct);
            return device;
        }
        catch (DbUpdateException e) when (DatabaseErrors.IsUniqueViolation(e))
        {
            if (createdCatalogItem is not null)
                sensors.DetachSensor(createdCatalogItem);
            if (device is not null)
                devices.Detach(device);
            throw new ApiFailure(409, "SENSOR_OCCUPIED", "Sensor is already associated.");
        }
        catch
        {
            if (createdCatalogItem is not null)
                sensors.DetachSensor(createdCatalogItem);
            if (device is not null)
                devices.Detach(device);
            throw;
        }
    }

    private async Task<string> ResolveNewSensorCodeAsync(string? requestedCode, string normalizedMac, CancellationToken ct)
    {
        if (requestedCode is not null)
            return requestedCode;

        var deterministic = DeterministicCodeFromMac(normalizedMac);
        if (await sensors.FindSensor(deterministic, null, ct) is null && !await sensors.IsAssociated(deterministic, ct))
            return deterministic;

        for (var attempt = 0; attempt < 16; attempt++)
        {
            var candidate = RandomCode();
            if (await sensors.FindSensor(candidate, null, ct) is null && !await sensors.IsAssociated(candidate, ct))
                return candidate;
        }

        throw new ApiFailure(409, "SENSOR_CODE_CONFLICT", "Unable to allocate a unique sensor code.");
    }

    private static string DeterministicCodeFromMac(string normalizedMac)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedMac));
        var value = BinaryPrimitives.ReadUInt64BigEndian(hash);
        Span<char> chars = stackalloc char[9];
        chars[0] = 'T';
        chars[1] = 'T';
        chars[2] = '-';
        for (var i = 8; i >= 3; i--)
        {
            chars[i] = Base36Alphabet[(int)(value % 36)];
            value /= 36;
        }

        return new string(chars);
    }

    private static string RandomCode()
    {
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        Span<char> chars = stackalloc char[9];
        chars[0] = 'T';
        chars[1] = 'T';
        chars[2] = '-';
        for (var i = 0; i < 6; i++)
            chars[i + 3] = Base36Alphabet[bytes[i] % 36];
        return new string(chars);
    }
}
