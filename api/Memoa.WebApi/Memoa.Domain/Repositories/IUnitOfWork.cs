namespace Memoa.Domain.Repositories
{
    /// <summary>
    /// Unit of Work pattern per gestire transazioni e persistenza
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Salva tutte le modifiche nel contesto corrente
        /// </summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Inizia una nuova transazione
        /// </summary>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Conferma la transazione corrente
        /// </summary>
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Annulla la transazione corrente
        /// </summary>
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
    }
}
