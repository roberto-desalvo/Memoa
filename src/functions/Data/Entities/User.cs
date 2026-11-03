namespace Memoa.Functions.Data.Entities;

/// <summary>Utente applicativo, collegato all'account di Entra External ID.</summary>
public sealed class User
{
    public int Id { get; set; }

    /// <summary>Claim "oid" del token, univoco nel tenant dell'ambiente.</summary>
    public Guid EntraObjectId { get; set; }

    /// <summary>Dato personale: mai nei log, incluso in esportazione e cancellazione.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>"it" o "en" (<see cref="Languages"/>).</summary>
    public string PreferredLanguage { get; set; } = Languages.Italian;

    /// <summary>
    /// Quando l'utente ha completato o rifiutato la guida dell'app (UTC); null finché va proposta.
    /// </summary>
    public DateTime? GuideDismissedAt { get; set; }

    /// <summary>
    /// Può modificare i parametri di sistema (<see cref="SystemSettings"/>). Si imposta solo a mano sul
    /// database: nessuna funzione lo modifica.
    /// </summary>
    public bool IsAdmin { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Record> Records { get; set; } = [];

    public List<Tag> Tags { get; set; } = [];
}
