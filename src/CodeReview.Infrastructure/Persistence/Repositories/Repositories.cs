using CodeReview.Domain.Entities;
using CodeReview.Domain.Enums;
using CodeReview.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeReview.Infrastructure.Persistence.Repositories;

public abstract class BaseRepository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    protected BaseRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await DbSet.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        await DbSet.ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await DbSet.AddAsync(entity, ct);

    public virtual void Update(T entity) => DbSet.Update(entity);

    public virtual void Remove(T entity) => DbSet.Remove(entity);
}

public sealed class AnalysisRepository : BaseRepository<CodeAnalysis>, IAnalysisRepository
{
    public AnalysisRepository(AppDbContext context) : base(context) { }

    public async Task<CodeAnalysis?> GetWithIssuesAsync(Guid id, CancellationToken ct = default) =>
        await Context.Analyses
            .Include(a => a.Issues)
            .Include(a => a.Snapshots)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<(IReadOnlyList<CodeAnalysis> Items, int TotalCount)> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        AnalysisStatus? status = null,
        CancellationToken ct = default)
    {
        var query = Context.Analyses
            .Include(a => a.Issues)
            .Where(a => a.UserId == userId);

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<CodeAnalysis>> GetRecentByUserAsync(Guid userId, int count, CancellationToken ct = default) =>
        await Context.Analyses
            .Where(a => a.UserId == userId && a.Status == AnalysisStatus.Completed)
            .OrderByDescending(a => a.CompletedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await Context.Analyses.AnyAsync(a => a.Id == id && a.UserId == userId, ct);
}

public sealed class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await Context.Users
            .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        await Context.Users
            .FirstOrDefaultAsync(u => u.Username == username, ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        await Context.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default) =>
        await Context.Users.AnyAsync(u => u.Username == username, ct);
}

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public UnitOfWork(AppDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);

    public Task BeginTransactionAsync(CancellationToken ct = default) =>
        _context.BeginTransactionAsync(ct);

    public Task CommitTransactionAsync(CancellationToken ct = default) =>
        _context.CommitTransactionAsync(ct);

    public Task RollbackTransactionAsync(CancellationToken ct = default) =>
        _context.RollbackTransactionAsync(ct);
}
