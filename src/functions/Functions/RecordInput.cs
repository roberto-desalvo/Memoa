using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Memoa.Functions.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Memoa.Functions.Functions;

/// <summary>Controlli comuni sui dati dei record inviati dal client.</summary>
internal static class RecordInput
{
    /// <summary>Limite sul testo di un record (scritto, caricato o trascrizione modificata).</summary>
    public const int MaxTextLength = 50_000;

    /// <summary>Corpo JSON della richiesta, oppure null se manca o non è valido.</summary>
    public static async Task<T?> ReadJsonOrDefaultAsync<T>(this HttpRequest req, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await req.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Titolo e data della nota, obbligatori alla creazione: il client manda sempre un titolo (se l'utente
    /// non lo specifica usa la data della nota). Restituisce la risposta di errore, oppure null se validi.
    /// </summary>
    public static IActionResult? ValidateNote(
        string? title, DateTimeOffset? occurredAt, out string validTitle, out DateTime occurredAtUtc)
    {
        validTitle = string.Empty;
        occurredAtUtc = default;

        if (!TryNormalizeTitle(title, out var normalized))
        {
            return InvalidTitle();
        }

        validTitle = normalized;

        if (occurredAt is not { } value)
        {
            return InvalidOccurredAt();
        }

        occurredAtUtc = value.UtcDateTime;
        return null;
    }

    public static bool TryNormalizeTitle(string? title, [NotNullWhen(true)] out string? normalized)
    {
        normalized = title?.Trim();
        return !string.IsNullOrEmpty(normalized) && normalized.Length <= Record.MaxTitleLength;
    }

    /// <summary>Testo non vuoto ed entro il limite; mai nei log.</summary>
    public static bool IsValidText([NotNullWhen(true)] string? text) =>
        !string.IsNullOrWhiteSpace(text) && text.Length <= MaxTextLength;

    public static IActionResult InvalidTitle() =>
        new BadRequestObjectResult(new { error = "invalid_title", maxLength = Record.MaxTitleLength });

    public static IActionResult InvalidOccurredAt() =>
        new BadRequestObjectResult(new { error = "invalid_occurred_at" });

    public static IActionResult InvalidText() =>
        new BadRequestObjectResult(new { error = "invalid_text", maxLength = MaxTextLength });
}
