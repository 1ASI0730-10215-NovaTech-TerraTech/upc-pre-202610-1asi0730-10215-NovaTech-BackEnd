using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NovaTech.TerraTech.Platform.Iam.Application.CommandServices;
using NovaTech.TerraTech.Platform.Iam.Domain.Model.Aggregates;
using NovaTech.TerraTech.Platform.Iam.Domain.Model.Commands;
using Xunit;

public partial class JourneyTests
{
    [Fact]
    public async Task LegacyRegistrationAndLoginAllowImmediateAuthenticationWithoutEmailVerification()
    {
        var email = Guid.NewGuid() + "@test.example";
        using (var scope = server.Services.CreateScope())
        {
            var commands = scope.ServiceProvider.GetRequiredService<IUserCommandService>();
            Assert.True((await commands.Handle(new SignUpCommand(email, "test-password"), CancellationToken.None)).IsSuccess);
            var signInResult = await commands.Handle(new SignInCommand(email, "test-password"), CancellationToken.None);
            Assert.True(signInResult.IsSuccess);
            Assert.False(string.IsNullOrWhiteSpace(signInResult.Value.token));
        }

        await server.WithDb(async db =>
        {
            var user = await db.Set<User>().SingleAsync(u => u.EmailAddress.Value == email);
            Assert.False(user.EmailVerificationExempt);
            Assert.Null(user.EmailVerifiedAtUtc);
        });

        var client = server.CreateClient();
        var login = await client.PostAsJsonAsync("/api/v1/authentication/sign-in", new { emailAddress = email, password = "test-password" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await Json(login);
        var token = auth.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/users/me")).StatusCode);

        await server.WithDb(async db =>
        {
            var user = await db.Set<User>().SingleAsync(u => u.EmailAddress.Value == email);
            db.Set<User>().Remove(user);
            await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me")).StatusCode);
    }
}
