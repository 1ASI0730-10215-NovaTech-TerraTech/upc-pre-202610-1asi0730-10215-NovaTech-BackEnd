using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace NovaTech.TerraTech.Platform.Monitoring.Interfaces.REST.Filters;

public class WebhookApiKeyFilter(IConfiguration configuration) : IAsyncResourceFilter
{
    public const string HeaderName = "X-Api-Key";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var expectedKey = configuration["Webhooks:ApiKey"];
        if (string.IsNullOrWhiteSpace(expectedKey) ||
            !context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            context.Result = UnauthorizedProblem();
            return;
        }

        var providedKey = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(providedKey))
        {
            context.Result = UnauthorizedProblem();
            return;
        }

        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey));
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, providedHash))
        {
            context.Result = UnauthorizedProblem();
            return;
        }

        await next();
    }

    private static ObjectResult UnauthorizedProblem() =>
        new(new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
            Status = StatusCodes.Status401Unauthorized,
            Title = "Valid X-Api-Key header is required.",
            Extensions = { ["code"] = "INVALID_API_KEY" }
        })
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentTypes = { "application/problem+json" }
        };
}
