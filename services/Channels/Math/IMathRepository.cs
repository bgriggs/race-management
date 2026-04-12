namespace Channels.Math;

public interface IMathRepository
{
    public Task<IEnumerable<MathDefinition>> GetDefinitionsAsync();
}
