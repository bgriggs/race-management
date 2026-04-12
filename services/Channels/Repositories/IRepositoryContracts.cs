namespace Channels.Repositories;

/// <summary>
/// Read-only access for a collection of saved definitions.
/// </summary>
public interface IDefinitionSetRepository<TDefinition>
{
    Task<IEnumerable<TDefinition>> GetDefinitionsAsync();
}

/// <summary>
/// Read/write access for a collection of saved definitions.
/// </summary>
public interface IMutableDefinitionSetRepository<TDefinition> : IDefinitionSetRepository<TDefinition>
{
    Task SaveDefinitionsAsync(IEnumerable<TDefinition> definitions);
}

/// <summary>
/// Read/write access for runtime state records keyed by ID.
/// </summary>
public interface IStateRepository<TId, TState>
{
    Task<TState> GetStateAsync(TId id);
    Task SetStateAsync(TState state);
}

/// <summary>
/// Read-only access for a single saved definition keyed by ID.
/// </summary>
public interface IDefinitionReader<TId, TDefinition>
{
    Task<TDefinition> GetDefinitionAsync(TId id);
}

/// <summary>
/// Read/write access for a single saved definition keyed by ID.
/// </summary>
public interface IDefinitionRepository<TId, TDefinition> : IDefinitionReader<TId, TDefinition>
{
    Task SetDefinitionAsync(TDefinition definition);
}
