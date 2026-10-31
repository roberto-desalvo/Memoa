namespace Memoa.Functions.Authentication;

/// <summary>
/// Utente autenticato dell'invocazione corrente (scoped). Valorizzato da
/// <see cref="AuthenticationMiddleware"/> solo per le funzioni HTTP autenticate: per i trigger di
/// code ed Event Grid resta vuoto.
/// </summary>
public sealed class CurrentUser
{
    /// <summary>Claim "oid" del token, oppure null se l'invocazione non ha un utente.</summary>
    public Guid? EntraObjectId { get; internal set; }

    /// <summary>Claim "oid" per le funzioni HTTP autenticate, dove l'utente c'è sempre.</summary>
    public Guid RequiredEntraObjectId =>
        EntraObjectId ?? throw new InvalidOperationException("Utente non autenticato.");
}
