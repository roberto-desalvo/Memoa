namespace Memoa.Functions.Authentication;

/// <summary>
/// Account di accesso dell'utente nel tenant Entra External ID dell'ambiente, distinto dall'utente
/// applicativo nel database.
/// </summary>
public interface IIdentityAccountService
{
    /// <summary>Elimina l'account di accesso. Idempotente: un account già eliminato non è un errore.</summary>
    Task DeleteAsync(Guid entraObjectId, CancellationToken cancellationToken);
}

/// <summary>
/// Segnaposto finché non si decide come le Functions si autenticano su Microsoft Graph nel tenant
/// esterno: l'account di accesso resta, e al login successivo l'utente trova un account vuoto.
/// </summary>
public sealed class RetainedIdentityAccountService : IIdentityAccountService
{
    public Task DeleteAsync(Guid entraObjectId, CancellationToken cancellationToken) => Task.CompletedTask;
}
