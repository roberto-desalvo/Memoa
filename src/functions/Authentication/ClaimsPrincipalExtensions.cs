using System.Security.Claims;

namespace Memoa.Functions.Authentication;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Identificativo stabile dell'utente nel tenant (claim "oid"). È l'unico dato dell'utente
    /// ammesso nei log.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal principal) =>
        principal.FindFirst("oid")?.Value
        ?? throw new InvalidOperationException("Utente non autenticato o claim 'oid' assente.");
}
