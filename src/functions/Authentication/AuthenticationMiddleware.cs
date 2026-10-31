using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Net.Http.Headers;

namespace Memoa.Functions.Authentication;

/// <summary>
/// Richiede un bearer token valido su tutte le funzioni HTTP, tranne quelle marcate con
/// <see cref="AllowAnonymousAttribute"/>. L'utente autenticato è in <c>HttpContext.User</c>.
/// </summary>
public sealed class AuthenticationMiddleware(TokenValidator validator) : IFunctionsWorkerMiddleware
{
    private static readonly ConcurrentDictionary<string, bool> AnonymousFunctions = new();

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
        AnonymousFunctions.GetOrAdd(definition.Id, _ =>
        {
            // EntryPoint ha la forma "Namespace.Classe.Metodo"; le funzioni stanno in questo assembly.
            var separator = definition.EntryPoint.LastIndexOf('.');
            var type = typeof(AuthenticationMiddleware).Assembly
                .GetType(definition.EntryPoint[..separator]);
            var method = type?.GetMethod(definition.EntryPoint[(separator + 1)..]);

            return method?.GetCustomAttribute<AllowAnonymousAttribute>() is not null
                || type?.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
        });
}
