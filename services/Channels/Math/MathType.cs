namespace Channels.Math;

/// <summary>
/// Types of supported mathematical calculations.
/// </summary>
public enum MathType
{
    /// <summary>
    /// Output = CH1 / (CH1 + CH2)
    /// </summary>
    Bias,
    /// <summary>
    /// Output = (A * CH1) + B
    /// </summary>
    LinearCorrector,
    /// <summary>
    /// Output = CH1 + CH2
    /// </summary>
    SimpleOperation,
    /// <summary>
    /// Output = (int)(CH1 / A)
    /// </summary>
    DivisionInteger,
    /// <summary>
    /// Output = CH1 % A
    /// </summary>
    DivisionModulo
}
