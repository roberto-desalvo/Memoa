namespace Memoa.Application.Services
{
    /// <summary>
    /// Servizio per accedere all'utente corrente autenticato
    /// </summary>
    public interface ICurrentUserService
    {
        /// <summary>
        /// Ottiene l'ID dell'utente corrente dalla richiesta HTTP
        /// </summary>
        /// <returns>L'ID utente se autenticato, altrimenti null</returns>
        string? GetCurrentUserId();

        /// <summary>
        /// Verifica se un utente è autenticato
        /// </summary>
        bool IsAuthenticated();
    }
}
