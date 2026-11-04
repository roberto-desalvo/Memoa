using Memoa.Functions.Authentication;
using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Memoa.Functions.Embeddings;
using Memoa.Functions.Settings;
using Memoa.Functions.Transcription;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Memoa.Functions.Functions;

/// <summary>
/// Parametri di sistema (<see cref="SystemSettings"/>), solo per gli utenti con <see cref="User.IsAdmin"/>:
/// gli altri ricevono 403 con il codice <see cref="ForbiddenError"/>. Ogni valore null vale "usa la
/// configurazione"; la risposta riporta anche i valori della configurazione, mostrati come predefiniti.
/// Percorso system/settings e non admin/...: l'host di Azure Functions riserva il prefisso admin alle proprie
/// API.
/// </summary>
public sealed class AdminFunctions(
    MemoaDbContext db,
    CurrentUser currentUser,
    SystemSettingsCache cache,
    IOptions<SpeechOptions> speechOptions,
    IOptions<SearchOptions> searchOptions,
    IOptions<TaggingOptions> taggingOptions,
    ILogger<AdminFunctions> logger)
{
    public const string ForbiddenError = "admin_required";

    [Function("GetSystemSettings")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "system/settings")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (await AdminIdAsync(cancellationToken) is null)
        {
            return Forbidden();
        }

        var stored = await db.SystemSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);

        return new OkObjectResult(ToResponse(stored));
    }

    [Function("UpdateSystemSettings")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "system/settings")] HttpRequest req,
        CancellationToken cancellationToken)
    {
        if (await AdminIdAsync(cancellationToken) is not { } userId)
        {
            return Forbidden();
        }

        var body = await req.ReadJsonOrDefaultAsync<SystemSettingsValues>(cancellationToken);
        if (body is null)
        {
            return new BadRequestObjectResult(new { error = "invalid_settings" });
        }

        if (Validate(body) is { } invalid)
        {
            return new BadRequestObjectResult(new { error = "invalid_setting", setting = invalid });
        }

        var stored = await db.SystemSettings.SingleOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken);
        if (stored is null)
        {
            stored = new SystemSettings { Id = SystemSettings.SingletonId };
            db.SystemSettings.Add(stored);
        }

        stored.SpeechEnabled = body.SpeechEnabled;
        stored.SpeechBaseTimeoutSeconds = body.SpeechBaseTimeoutSeconds;
        stored.SpeechTimeoutPerAudioMinuteSeconds = body.SpeechTimeoutPerAudioMinuteSeconds;
        stored.SearchMinSimilarity = body.SearchMinSimilarity;
        stored.SearchMaxResults = body.SearchMaxResults;
        stored.TaggingMinSimilarity = body.TaggingMinSimilarity;
        stored.TaggingPendingTimeoutMinutes = body.TaggingPendingTimeoutMinutes;
        await db.SaveChangesAsync(cancellationToken);

        // Le altre istanze leggono i nuovi valori alla scadenza della loro cache.
        cache.Invalidate();

        logger.LogInformation("Parametri di sistema aggiornati dall'utente {UserId}", userId);
        return new OkObjectResult(ToResponse(stored));
    }

    /// <summary>Id dell'utente autenticato se è amministratore, altrimenti null.</summary>
    private async Task<int?> AdminIdAsync(CancellationToken cancellationToken)
    {
        var entraObjectId = currentUser.RequiredEntraObjectId;
        var user = await db.Users
            .Where(u => u.EntraObjectId == entraObjectId)
            .Select(u => new { u.Id, u.IsAdmin })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is not { IsAdmin: true })
        {
            logger.LogWarning("Accesso ai parametri di sistema negato all'utente {UserId}", user?.Id);
            return null;
        }

        return user.Id;
    }

    private static ObjectResult Forbidden() =>
        new(new { error = ForbiddenError }) { StatusCode = StatusCodes.Status403Forbidden };

    /// <summary>Nome del primo valore fuori dall'intervallo ammesso, oppure null se sono tutti validi.</summary>
    private static string? Validate(SystemSettingsValues values) =>
        !InRange(values.SpeechBaseTimeoutSeconds, 1, 600) ? nameof(values.SpeechBaseTimeoutSeconds)
        : !InRange(values.SpeechTimeoutPerAudioMinuteSeconds, 0, 120) ? nameof(values.SpeechTimeoutPerAudioMinuteSeconds)
        : !InRange(values.SearchMinSimilarity, 0, 1) ? nameof(values.SearchMinSimilarity)
        : !InRange(values.SearchMaxResults, 1, 50) ? nameof(values.SearchMaxResults)
        : !InRange(values.TaggingMinSimilarity, 0, 1) ? nameof(values.TaggingMinSimilarity)
        : !InRange(values.TaggingPendingTimeoutMinutes, 1, 1_440) ? nameof(values.TaggingPendingTimeoutMinutes)
        : null;

    // Stessi intervalli delle opzioni di configurazione (attributi Range).
    private static bool InRange(double? value, double min, double max) =>
        value is null || (value >= min && value <= max);

    private SystemSettingsResponse ToResponse(SystemSettings? stored)
    {
        var speech = speechOptions.Value;
        var search = searchOptions.Value;
        var tagging = taggingOptions.Value;

        return new SystemSettingsResponse(
            new SystemSettingsValues(
                stored?.SpeechEnabled,
                stored?.SpeechBaseTimeoutSeconds,
                stored?.SpeechTimeoutPerAudioMinuteSeconds,
                stored?.SearchMinSimilarity,
                stored?.SearchMaxResults,
                stored?.TaggingMinSimilarity,
                stored?.TaggingPendingTimeoutMinutes),
            new SystemSettingsValues(
                speech.Enabled,
                speech.BaseTimeoutSeconds,
                speech.TimeoutPerAudioMinuteSeconds,
                search.MinSimilarity,
                search.MaxResults,
                tagging.MinSimilarity,
                tagging.PendingTimeoutMinutes),
            stored is null ? null : RecordResponses.AsUtc(stored.UpdatedAt));
    }
}

/// <summary>Parametri di sistema; null vale "usa la configurazione".</summary>
public sealed record SystemSettingsValues(
    bool? SpeechEnabled,
    int? SpeechBaseTimeoutSeconds,
    int? SpeechTimeoutPerAudioMinuteSeconds,
    double? SearchMinSimilarity,
    int? SearchMaxResults,
    double? TaggingMinSimilarity,
    int? TaggingPendingTimeoutMinutes);

/// <param name="Values">Valori salvati nel database.</param>
/// <param name="Defaults">Valori della configurazione, usati dove <paramref name="Values"/> è null.</param>
/// <param name="UpdatedAt">Ultimo salvataggio, null se i parametri non sono mai stati salvati.</param>
public sealed record SystemSettingsResponse(
    SystemSettingsValues Values,
    SystemSettingsValues Defaults,
    DateTimeOffset? UpdatedAt);
