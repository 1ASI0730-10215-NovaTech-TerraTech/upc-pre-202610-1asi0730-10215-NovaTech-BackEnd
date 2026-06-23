namespace NovaTech.TerraTech.Platform.NotificationManagement.Domain.Model.Commands;

public record CreateNotificationCommand(
    string ProfileId,
    string Title,
    string Message,
    bool IsAlert = false);