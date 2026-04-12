namespace Channels.Math;

public class MathMemoryRepository : IMathRepository
{
    private readonly List<MathDefinition> definitions = [];

    public void Add(MathDefinition definition) => definitions.Add(definition);

    public Task<IEnumerable<MathDefinition>> GetDefinitionsAsync() =>
        Task.FromResult(definitions.AsEnumerable());
}
