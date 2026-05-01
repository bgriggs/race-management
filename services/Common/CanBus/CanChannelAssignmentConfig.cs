namespace Common.CanBus;

/// <summary>
/// Associates a Channel with a specific offset and length in a CAN message.
/// </summary>
public class CanChannelAssignmentConfig
{
    public Guid Id { get; set; }
    public int Offset { get; set; }
    public int Length { get; set; } = 1;
    public ulong Mask { get; set; } = 0xFF;
    public bool IsSigned { get; set; }
    public double FormulaMultiplier { get; set; } = 1;
    public double FormulaDivider { get; set; } = 1;
    public double FormulaConst { get; set; } = 0;
}
