using Memoa.Domain.Entities;

namespace Memoa.Domain.Repositories
{
    public interface IEntryRepository
    {
        Task AddAsync(Entry entry, CancellationToken cancellationToken = default);
        Task<List<Entry>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
        Task<Entry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task UpdateAsync(Entry entry, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
