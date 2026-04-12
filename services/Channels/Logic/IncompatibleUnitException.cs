namespace Channels.Logic;

/// <summary>
/// Thrown when a comparison is attempted between channels with incompatible unit types
/// (e.g., comparing a temperature channel against a speed channel).
/// </summary>
public class IncompatibleUnitException : Exception
{
    public string SourceUnit { get; }
    public string TargetUnit { get; }

    public IncompatibleUnitException(string sourceUnit, string targetUnit, string message)
        : base(message)
    {
        SourceUnit = sourceUnit;
        TargetUnit = targetUnit;
    }
}
