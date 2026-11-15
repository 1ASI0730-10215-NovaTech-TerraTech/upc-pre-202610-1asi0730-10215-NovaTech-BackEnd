using Microsoft.Extensions.DependencyInjection;
using NovaTech.TerraTech.Platform.Iam.Interface.Acl;
using Xunit;

public partial class JourneyTests
{
    [Fact]
    public async Task LegacyRegistrationAndLoginCannotBypassEmailVerification()
    {
        var account = await Account();
        var me = await Json(await account.client.GetAsync("/api/v1/users/me"));
        var email = me.GetProperty("emailAddress").GetString()!;
        var assembly = typeof(Program).Assembly;
        Assert.Null(assembly.GetType("NovaTech.TerraTech.Platform.Iam.Application.CommandServices.IUserCommandService"));
        Assert.Null(assembly.GetType("NovaTech.TerraTech.Platform.Iam.Application.Internal.CommandServices.UserCommandService"));
        Assert.Null(assembly.GetType("NovaTech.TerraTech.Platform.Iam.Domain.Model.Commands.SignUpCommand"));
        Assert.Null(assembly.GetType("NovaTech.TerraTech.Platform.Iam.Domain.Model.Commands.SignInCommand"));
        Assert.Null(typeof(IIamContextFacade).GetMethod("CreateUser"));
        using var scope = server.Services.CreateScope();
        var facade = scope.ServiceProvider.GetRequiredService<IIamContextFacade>();
        Assert.Equal(account.userId, await facade.FetchUserByEmail(email, CancellationToken.None));
        Assert.Equal(email, await facade.FetchEmailByUserId(account.userId, CancellationToken.None));
        Assert.Equal("Demo Farmer", await facade.FetchFullNameByUserId(account.userId, CancellationToken.None));
        var names = await facade.FetchFullNamesByUserIds([account.userId], CancellationToken.None);
        Assert.Equal("Demo Farmer", names[account.userId]);
    }
}
