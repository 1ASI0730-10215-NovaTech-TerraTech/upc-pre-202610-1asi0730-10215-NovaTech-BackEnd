namespace NovaTech.TerraTech.Platform.Iam.Application.Internal.OutboundServices;

public interface IAccountEmailSender
{
    void EnsureConfigured();
    Task SendPasswordReset(string email, string token, CancellationToken ct);
}
