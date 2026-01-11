using Memoa.Domain.Data;
using Memoa.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Memoa.Domain.Repositories
{
    public class EntryRepository : RepositoryBase, IEntryRepository
    {
        public EntryRepository(MemoaDbContext dbContext)
            : base(dbContext)
        {
        }

        public async Task AddAsync(Entry entry, CancellationToken cancellationToken = default)
        {
            DbContext.Entries.Add(entry);
        }

        public async Task<List<Entry>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            return await DbContext.Entries
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<Entry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await DbContext.Entries
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        }

        public Task UpdateAsync(Entry entry, CancellationToken cancellationToken = default)
        {
            DbContext.Entries.Update(entry);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entry = await GetByIdAsync(id, cancellationToken);
            if (entry != null)
            {
                DbContext.Entries.Remove(entry);
            }
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await base.SaveChangesAsync(cancellationToken);
        }
    }
}
