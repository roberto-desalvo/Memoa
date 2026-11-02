using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Utente applicativo: <c>GET /me</c> lo registra (il frontend la chiama all'apertura della homepage, le
/// altre funzioni rispondono 403 finché l'utente non esiste, vedi <see cref="UserRegistrationMiddleware"/>)
/// e dice se proporre la guida; <c>PUT /me/guide</c> registra che la guida è stata completata o rifiutata.
/// </summary>
public sealed class MeFunctions(
    MemoaDbContext db,
    CurrentUser currentUser,
    ILogger<MeFunctions> logger)
{
    // Violazione di chiave primaria o di indice univoco.
    private static readonly int[] DuplicateKeyErrors = [2627, 2601];

    // Lunghezza della colonna Users.Email.
    private const int MaxEmailLength = 320;

    [Function("GetMe")]
    [AllowUnregisteredUser]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var user = await EnsureUserAsync(req, cancellationToken);
        return new OkObjectResult(new Me(ShowGuide: user.GuideDismissedAt is null));
    }

    [Function("DismissGuide")]
    public async Task<IActionResult> DismissGuide(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "me/guide")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<GuideState>(cancellationToken);
        if (body is not { Dismissed: true })
        {
            return new BadRequestObjectResult(new { error = "invalid_guide_state" });
        }

        var entraObjectId = currentUser.RequiredEntraObjectId;

        // Garantito da UserRegistrationMiddleware. Resta la prima data: rivedere la guida non la cambia.
        var updated = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId && u.GuideDismissedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.GuideDismissedAt, DateTime.UtcNow), cancellationToken);

        if (updated > 0)
        {
            logger.LogInformation("Guida chiusa dall'utente {EntraObjectId}", entraObjectId);
        }

        return new NoContentResult();
    }

    private async Task<User> EnsureUserAsync(HttpRequest req, CancellationToken cancellationToken)
    {
        var entraObjectId = currentUser.RequiredEntraObjectId;

        // Claim opzionale del token di accesso (registrazione dell'API in infra/identity/main.bicep),
        // assente se l'account non ha un'email.
        var email = req.HttpContext.User.FindFirst("email")?.Value ?? string.Empty;
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

            return existing;
        }

        // La lingua scelta alla registrazione non è nel token: si parte dall'italiano.
        var user = new User
        {
            EntraObjectId = entraObjectId,
            Email = email,
            PreferredLanguage = Languages.Italian,
        };
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Utente {UserId} creato", user.Id);
            return user;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && DuplicateKeyErrors.Contains(sql.Number))
        {
            // Prima richiesta in parallelo: l'utente è stato creato da un'altra invocazione.
            db.Entry(user).State = EntityState.Detached;
            return await db.Users.SingleAsync(u => u.EntraObjectId == entraObjectId, cancellationToken);
        }
    }
}

/// <param name="ShowGuide">La guida va proposta: l'utente non l'ha ancora completata né rifiutata.</param>
public sealed record Me(bool ShowGuide);

public sealed record GuideState(bool Dismissed);
