using Memoa.Functions.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Authentication;

/// <summary>
/// Rifiuta le richieste autenticate di un utente non ancora registrato con 403 e il codice
/// <see cref="NotRegisteredError"/>: l'utente applicativo lo crea solo <c>GET /me</c>, che il frontend
/// chiama all'apertura della homepage. Va registrato dopo <see cref="AuthenticationMiddleware"/>: agisce
/// solo se il token è stato validato, quindi mai per funzioni anonime o trigger non HTTP. Le funzioni
/// marcate con <see cref="AllowUnregisteredUserAttribute"/> passano comunque.
/// </summary>
public sealed class UserRegistrationMiddleware(ILogger<UserRegistrationMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public const string NotRegisteredError = "user_not_registered";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var entraObjectId = context.InstanceServices.GetRequiredService<CurrentUser>().EntraObjectId;

        if (entraObjectId is { } oid
            && !FunctionAttributes.Has<AllowUnregisteredUserAttribute>(context.FunctionDefinition))
        {
            var db = context.InstanceServices.GetRequiredService<MemoaDbContext>();
            if (!await db.Users.AnyAsync(u => u.EntraObjectId == oid, context.CancellationToken))
            {
                logger.LogInformation("Richiesta a {Function} di un utente non registrato", context.FunctionDefinition.Name);

                var response = context.GetHttpContext()!.Response;
                response.StatusCode = StatusCodes.Status403Forbidden;
                await response.WriteAsJsonAsync(new { error = NotRegisteredError }, context.CancellationToken);
                return;
            }
        }

        await next(context);
    }
}
