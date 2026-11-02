using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Memoa.Functions.Authentication;

/// <summary>
/// Richiede un bearer token valido su tutte le funzioni HTTP, tranne quelle marcate con
/// <see cref="AllowAnonymousAttribute"/>. L'utente autenticato è in <c>HttpContext.User</c>.
/// </summary>
public sealed class AuthenticationMiddleware(TokenValidator validator) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        // Trigger non HTTP (code, Event Grid, orchestrazioni) o funzioni pubbliche.
        if (httpContext is null || IsAnonymous(context.FunctionDefinition))
        {
            await next(context);
            return;
        }

        var token = ReadBearerToken(httpContext.Request);
        var principal = token is null
            ? null
            : await validator.ValidateAsync(token, context.CancellationToken);

        if (principal is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }

        httpContext.User = principal;
        context.InstanceServices.GetRequiredService<CurrentUser>().EntraObjectId =
            Guid.Parse(principal.GetUserId());
        await next(context);
    }

    private static string? ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers[HeaderNames.Authorization].ToString();
        const string prefix = "Bearer ";

        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && header.Length > prefix.Length
            ? header[prefix.Length..].Trim()
            : null;
    }

    private static bool IsAnonymous(FunctionDefinition definition) =>
        FunctionAttributes.Has<AllowAnonymousAttribute>(definition);
}
