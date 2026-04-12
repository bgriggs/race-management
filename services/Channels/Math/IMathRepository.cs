using Channels.Repositories;

namespace Channels.Math;

public interface IMathRepository : IDefinitionSetRepository<MathDefinition>
{
    public Task<IEnumerable<MathDefinition>> GetDefinitionsAsync();
}
