namespace Racecar.Pipeline;

/// <summary>
/// Filters channel values, dropping those that haven't changed beyond their
/// per-channel deadband. Sits twice in the pipeline: once after Decode (so the
/// derive stage can skip work) and once after Derive (so consumers only see
/// real changes). Both passes share the same backing state, also exposed as
/// <see cref="ChannelStatusState"/>.
/// </summary>
/// <remarks>
/// Single-threaded: only the pipeline worker calls <see cref="Filter"/>.
/// Reads of <see cref="ChannelStatusState"/> are safe from any thread.
/// </remarks>
public sealed class ChangeFilter(ChannelStatusState state)
{
    /// <summary>
    /// In-place filter: returns the count of values that passed the filter.
    /// Surviving values are packed at the start of <paramref name="buffer"/>.
    /// </summary>
    public int Filter(ActiveConfiguration config, Span<InternalChannelValue> buffer)
    {
        var write = 0;
        for (var read = 0; read < buffer.Length; read++)
        {
            var value = buffer[read];
            // Liveness: a sample arrived for this channel now, whether or not it changed the
            // value. The timeout monitor keys off this so a constant-but-live channel that the
            // deadband drops below is not mistaken for a silent source and reset to default.
            state.MarkSeen(value.ChannelId, value.WallTime);
            if (PassesFilter(config, in value))
            {
                state.Set(in value);
                buffer[write++] = value;
            }
        }
        return write;
    }

    /// <summary>Single-value variant for derived results.</summary>
    public bool TryAccept(ActiveConfiguration config, in InternalChannelValue value)
    {
        state.MarkSeen(value.ChannelId, value.WallTime);
        if (!PassesFilter(config, in value))
        {
            return false;
        }
        state.Set(in value);
        return true;
    }

    private bool PassesFilter(ActiveConfiguration config, in InternalChannelValue value)
    {
        if (!state.TryGet(value.ChannelId, out var previous))
        {
            return true;
        }

        var deadband = config.GetDeadband(value.ChannelId);
        if (deadband <= 0d)
        {
            return previous.Value != value.Value;
        }
        return Math.Abs(value.Value - previous.Value) > deadband;
    }
}
