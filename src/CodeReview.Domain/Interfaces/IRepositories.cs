using CodeReview.Domain.Entities;
using CodeReview.Domain.Enums;

namespace CodeReview.Domain.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackTransactionAsync(CancellationToken ct = default);
}

public interface IAnalysisRepository : IRepository<CodeAnalysis>
{
    Task<CodeAnalysis?> GetWithIssuesAsync(Guid id, CancellationToken ct = default);
    Task<(IReadOnlyList<CodeAnalysis> Items, int TotalCount)> GetPagedByUserAsync(
        Guid userId,
        int page,
        int pageSize,
        AnalysisStatus? status = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<CodeAnalysis>> GetRecentByUserAsync(Guid userId, int count, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, Guid userId, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken ct = default);
}

public interface ISettingsRepository
{
    Task<string?> GetValueAsync(string key, CancellationToken ct = default);
    Task SetValueAsync(string key, string value, CancellationToken ct = default);
}
