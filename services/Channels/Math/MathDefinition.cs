using System.ComponentModel.DataAnnotations;

namespace Channels.Math;

public class MathDefinition
{
    public Guid Id { get; set; }
    [MaxLength(20)]
    public required string Name { get; set; }
    public MathType Type { get; set; }
    public decimal A { get; set; }
    public decimal B { get; set; }
    public Guid Channel1Id { get; set; }
    /// <summary>
    /// Second input channel. Use <see cref="Guid.Empty"/> to use constant <see cref="A"/> instead.
    /// </summary>
    public Guid? Channel2Id { get; set; }
    public Guid OutputChannelId { get; set; }
    /// <summary>
    /// This is used when Type is SimpleOperation. It is otherwise ignored.
    /// </summary>
    public SimpleOperationType SimpleOperationType { get; set; }
}
