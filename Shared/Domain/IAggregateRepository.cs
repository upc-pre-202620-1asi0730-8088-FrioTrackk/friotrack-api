namespace FrioTrack.Api.Shared.Domain;

// Application use cases coordinate one unit of work; repositories expose aggregate roots.
public interface IAggregateRepository<T> where T : class {
    Task<T?> FindAsync(string id, CancellationToken cancellationToken = default);
    void Add(T aggregate);
    void Remove(T aggregate);
}
