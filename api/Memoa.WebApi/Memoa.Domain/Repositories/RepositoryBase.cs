using Memoa.Domain.Data;

namespace Memoa.Domain.Repositories
{
    public abstract class RepositoryBase
    {
        protected readonly MemoaDbContext DbContext;

        protected RepositoryBase(MemoaDbContext dbContext)
        {
            DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        protected virtual async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
