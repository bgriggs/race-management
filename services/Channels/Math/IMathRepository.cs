namespace Channels.Math;

public interface IMathRepository
{
    public Task<IEnumerable<MathParameters>> GetParameters();
}
