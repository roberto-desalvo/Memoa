using Memoa.Infrastructure.Data;
using Memoa.Domain.Entities;
using Memoa.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Infrastructure.Repositories
{
    public class EntryRepository : IEntryRepository
    {
        private readonly MemoaDbContext _dbContext;

        public EntryRepository(MemoaDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task AddAsync(Entry entry, CancellationToken cancellationToken = default)
        {
            await _dbContext.Entries.AddAsync(entry, cancellationToken);
        }

        public async Task<List<Entry>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Entries
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Entry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Entries
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        public Task UpdateAsync(Entry entry, CancellationToken cancellationToken = default)
        {
            _dbContext.Entries.Update(entry);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await GetByIdAsync(id, cancellationToken);
            if (entry != null)
            {
                _dbContext.Entries.Remove(entry);
            }
        }
    }
}
