using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Authentication;

/// <summary>
/// Crea l'utente applicativo alla prima richiesta autenticata (just in time). Va registrato dopo
/// <see cref="AuthenticationMiddleware"/>: agisce solo se il token è stato validato, quindi mai per
/// funzioni anonime o trigger non HTTP.
/// </summary>
public sealed class UserProvisioningMiddleware(ILogger<UserProvisioningMiddleware> logger) : IFunctionsWorkerMiddleware
{
    // Violazione di chiave primaria o di indice univoco.
    private static readonly int[] DuplicateKeyErrors = [2627, 2601];

    // Lunghezza della colonna Users.Email.
    private const int MaxEmailLength = 320;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var entraObjectId = context.InstanceServices.GetRequiredService<CurrentUser>().EntraObjectId;

        if (entraObjectId is { } oid)
        {
            await EnsureUserAsync(context, oid);
        }

        await next(context);
    }

    private async Task EnsureUserAsync(FunctionContext context, Guid entraObjectId)
    {
        var db = context.InstanceServices.GetRequiredService<MemoaDbContext>();
        var cancellationToken = context.CancellationToken;

        // Claim opzionale del token di accesso (registrazione dell'API in infra/identity/main.bicep),
        // assente se l'account non ha un'email.
        var email = context.GetHttpContext()?.User.FindFirst("email")?.Value ?? string.Empty;
        if (email.Length > MaxEmailLength)
        {
            email = email[..MaxEmailLength];
        }

        var existing = await db.Users.SingleOrDefaultAsync(u => u.EntraObjectId == entraObjectId, cancellationToken);
        if (existing is not null)
        {
            // L'email può cambiare in Entra: si allinea la copia, ma non la si cancella se il claim manca.
            if (email.Length > 0 && existing.Email != email)
            {
                existing.Email = email;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Email dell'utente {UserId} aggiornata", existing.Id);
            }

            return;
        }

        // La lingua scelta alla registrazione non è nel token: si parte dall'italiano.
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            EntraObjectId = entraObjectId,
            Email = email,
            PreferredLanguage = Languages.Italian,
        };
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Utente {UserId} creato", user.Id);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && DuplicateKeyErrors.Contains(sql.Number))
        {
            // Prima richiesta in parallelo: l'utente è stato creato da un'altra invocazione.
            db.Entry(user).State = EntityState.Detached;
        }
    }
}
