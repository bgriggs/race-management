namespace Channels.Logic;

/// <summary>
/// Thrown when a comparison is attempted between channels with incompatible unit types
/// (e.g., comparing a temperature channel against a speed channel).
/// </summary>
public class IncompatibleUnitException(string sourceUnit, string targetUnit, string message) : Exception(message)
{
    public string SourceUnit { get; } = sourceUnit;
    public string TargetUnit { get; } = targetUnit;
}
