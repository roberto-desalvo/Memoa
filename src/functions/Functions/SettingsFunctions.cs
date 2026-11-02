using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Memoa.Functions.Functions;

/// <summary>
/// Impostazioni dell'account salvate nel database: per ora la lingua preferita, che fa da ripiego per la
/// trascrizione. Tema chiaro / scuro è solo una preferenza del dispositivo, gestita dal client.
/// </summary>
public sealed class SettingsFunctions(
    MemoaDbContext db,
    CurrentUser currentUser,
    ILogger<SettingsFunctions> logger)
{
    private static readonly string[] SupportedLanguages = [Languages.Italian, Languages.English];

    [Function("GetSettings")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me/settings")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var entraObjectId = currentUser.RequiredEntraObjectId;

        // Garantito da UserRegistrationMiddleware prima di questa funzione.
        var settings = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => new Settings(u.PreferredLanguage))
            .SingleAsync(cancellationToken);

        return new OkObjectResult(settings);
    }

    [Function("UpdateSettings")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "me/settings")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        var body = await req.ReadJsonOrDefaultAsync<Settings>(cancellationToken);
        if (body is null || !SupportedLanguages.Contains(body.PreferredLanguage))
        {
            return new BadRequestObjectResult(new { error = "invalid_language", supported = SupportedLanguages });
        }

        var entraObjectId = currentUser.RequiredEntraObjectId;
        var user = await db.Users.SingleAsync(u => u.EntraObjectId == entraObjectId, cancellationToken);
        user.PreferredLanguage = body.PreferredLanguage;
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Impostazioni dell'utente {UserId} aggiornate", user.Id);
        return new OkObjectResult(new Settings(user.PreferredLanguage));
    }
}

public sealed record Settings(string PreferredLanguage);
