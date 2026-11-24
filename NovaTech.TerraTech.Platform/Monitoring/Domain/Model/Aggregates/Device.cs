using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.ValueObjects;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Commands;

namespace NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;
public partial class Device
{
    private static readonly System.Text.RegularExpressions.Regex SensorCodeRegex = new("^TT-[0-9A-Z]{6}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    protected Device()
    {
        FieldId = null!;
        MacAddress = null!;
        Status = null!;
        LastSync = null!;
    }

    public Device(CreateDeviceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        FieldId = command.FieldId;
        MacAddress = command.MacAddress;
        Status = command.Status;
        LastSync = new LastSync(command.LastSync);
        if (command.SensorCode is not null)
        {
            if (!SensorCodeRegex.IsMatch(command.SensorCode))
                throw new ArgumentException("Sensor code must match format TT-XXXXXX.", nameof(command));
            SensorCode = command.SensorCode;
        }

        if (!string.IsNullOrWhiteSpace(command.Name))
            Name = command.Name.Trim();
        if (command.BatteryLevelPercent is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(command), "Battery level percent must be between 0 and 100.");
        BatteryLevelPercent = command.BatteryLevelPercent;
    }

    public string? SensorCode { get; private set; }
    public string? Name { get; private set; }
    public int? BatteryLevelPercent { get; private set; }

    public void Identify(string code, string name)
    {
        if (!SensorCodeRegex.IsMatch(code))
            throw new ArgumentException("Sensor code must match format TT-XXXXXX.", nameof(code));
        SensorCode = code;
        Name = name.Trim();
    }

    public int Id { get; private set; }
    public FieldId FieldId { get; private set; }
    public MacAddress MacAddress { get; private set; }
    public DeviceStatus Status { get; private set; }
    public LastSync LastSync { get; private set; }

    public void Update(UpdateDeviceCommand command)
    {
        MacAddress = command.MacAddress;
        Status = command.Status;
        LastSync = new LastSync(command.LastSync);
    }
}
