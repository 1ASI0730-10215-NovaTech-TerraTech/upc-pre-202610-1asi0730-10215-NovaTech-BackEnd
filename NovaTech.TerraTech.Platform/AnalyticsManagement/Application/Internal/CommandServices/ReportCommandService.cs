using Microsoft.EntityFrameworkCore;
using NovaTech.TerraTech.Platform.AnalyticsManagement.Application.Errors;
using NovaTech.TerraTech.Platform.AnalyticsManagement.Application.Services;
using NovaTech.TerraTech.Platform.AnalyticsManagement.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.AnalyticsManagement.Domain.Model.Commands;
using NovaTech.TerraTech.Platform.AnalyticsManagement.Domain.Model.ValueObjects;
using NovaTech.TerraTech.Platform.AnalyticsManagement.Domain.Repositories;
using NovaTech.TerraTech.Platform.Shared.Application.Model;
using NovaTech.TerraTech.Platform.Shared.Domain.Repositories;

namespace NovaTech.TerraTech.Platform.AnalyticsManagement.Application.Internal.CommandServices;

public class ReportCommandService(
    IReportRepository reportRepository,
    IUnitOfWork unitOfWork,
    ILogger<ReportCommandService> logger)
    : IReportCommandService
{
    public async Task<Result<Report>> Handle(CreateReportCommand command, CancellationToken cancellationToken = default)
    {
        var deviceId = command.DeviceId;
        var generatedAt = command.GeneratedAt;
        var existing = await reportRepository.FindByDeviceIdAndGeneratedAtAsync(deviceId, generatedAt, cancellationToken);
        if (existing != null)
        {
            logger.LogWarning("Duplicate report for DeviceId {DeviceId} on {GeneratedAt}", command.DeviceId, command.GeneratedAt);
            return Result<Report>.Failure(CreateReportError.DuplicateReport,
                "A report for this device on the same date already exists.");
        }

        try
        {
            var report = new Report(command);
            await reportRepository.AddAsync(report, cancellationToken);
            await unitOfWork.CompleteAsync(cancellationToken);
            return Result<Report>.Success(report);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid arguments while creating report for DeviceId {DeviceId}", command.DeviceId);
            return Result<Report>.Failure(CreateReportError.UnexpectedError, ex.Message);
        }
        catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
        {
            logger.LogWarning(ex, "Duplicate key violation creating report for DeviceId {DeviceId}", command.DeviceId);
            return Result<Report>.Failure(CreateReportError.DuplicateReport, "Database duplicate key violation occurred.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database update failed creating report for DeviceId {DeviceId}", command.DeviceId);
            return Result<Report>.Failure(CreateReportError.UnexpectedError, "Database update failed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error creating report for DeviceId {DeviceId}", command.DeviceId);
            return Result<Report>.Failure(CreateReportError.UnexpectedError, ex.Message);
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