using Memoa.Functions.Data;
using Memoa.Functions.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Functions.Tagging;

/// <summary>
/// Stato di assegnazione dei tag, calcolato a ogni lettura invece che salvato: un tag è "in assegnazione"
/// se ha un ricalcolo in attesa (<see cref="Tag.AssignmentPendingSince"/>) oppure se è attivo e almeno un
/// record dell'utente attende il confronto con i tag (<see cref="Record.TagsPendingSince"/>, record non in
/// errore). Ogni passaggio azzera solo la propria attesa, quindi più record elaborati insieme non si
/// scavalcano. Le attese più vecchie del timeout non contano: un processo fallito senza ripulire non
/// lascia il tag bloccato.
/// </summary>
internal static class TagStatus
{
    public const string Assigning = "Assigning";

    public const string Ready = "Ready";

    public static string Of(bool isActive, DateTime? assignmentPendingSince, bool recordsPending, DateTime pendingAfter) =>
        assignmentPendingSince > pendingAfter || (isActive && recordsPending) ? Assigning : Ready;

    /// <summary>Inizio della finestra in cui un'attesa conta ancora (UTC).</summary>
    public static DateTime PendingAfter(TimeSpan timeout) => DateTime.UtcNow - timeout;

    /// <summary>
    /// True se almeno un record dell'utente autenticato (filtro globale) attende il confronto con i tag. Un
    /// record in errore non arriverà al confronto: non conta.
    /// </summary>
    public static Task<bool> AnyRecordPendingAsync(MemoaDbContext db, DateTime pendingAfter, CancellationToken cancellationToken) =>
        db.Records.AnyAsync(
            r => r.TagsPendingSince > pendingAfter && r.Status != RecordStatus.Error,
            cancellationToken);
}
