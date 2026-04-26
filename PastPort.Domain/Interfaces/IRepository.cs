using System.Linq.Expressions;

namespace PastPort.Domain.Interfaces;

/// <summary>
/// Generic repository contract that provides async CRUD operations
/// for any domain entity. Implementations are registered as scoped
/// services in the DI container and backed by Entity Framework Core.
/// </summary>
/// <typeparam name="T">The entity type this repository manages.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>
    /// Retrieves a single entity by its primary key.
    /// </summary>
    /// <param name="id">The unique identifier of the entity.</param>
    /// <returns>The entity if found; otherwise <c>null</c>.</returns>
    Task<T?> GetByIdAsync(Guid id);

    /// <summary>
    /// Retrieves all entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <returns>An enumerable of all entities.</returns>
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>
    /// Retrieves entities matching a predicate expression.
    /// The predicate is translated to a SQL <c>WHERE</c> clause by EF Core.
    /// </summary>
    /// <param name="predicate">A LINQ expression to filter entities.</param>
    /// <returns>An enumerable of matching entities.</returns>
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>
    /// Adds a new entity to the database.
    /// </summary>
    /// <param name="entity">The entity to persist.</param>
    /// <returns>The persisted entity (with generated ID).</returns>
    Task<T> AddAsync(T entity);

    /// <summary>
    /// Updates an existing entity in the database.
    /// </summary>
    /// <param name="entity">The entity with updated values.</param>
    Task UpdateAsync(T entity);

    /// <summary>
    /// Deletes an entity from the database.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    Task DeleteAsync(T entity);

    /// <summary>
    /// Checks whether an entity with the specified ID exists in the database.
    /// </summary>
    /// <param name="id">The unique identifier to check.</param>
    /// <returns><c>true</c> if the entity exists; otherwise <c>false</c>.</returns>
    Task<bool> ExistsAsync(Guid id);
}