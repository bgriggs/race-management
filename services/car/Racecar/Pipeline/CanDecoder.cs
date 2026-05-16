using Racecar.CanBus;

namespace Racecar.Pipeline;

/// <summary>
/// Decodes a <see cref="CanMessage"/> into a set of <see cref="InternalChannelValue"/>s
/// per the supplied <see cref="ActiveConfiguration"/>. Cheap, allocation-light,
/// runs synchronously on the pipeline worker.
/// </summary>
public sealed class CanDecoder
{
    /// <summary>
    /// Decodes the frame. Returns the count of decoded values written to <paramref name="output"/>.
    /// Returns 0 if the (bus, can-id) pair is not mapped.
    /// </summary>
    public int Decode(
        ActiveConfiguration config,
        in TimestampedFrame frame,
        Span<InternalChannelValue> output)
    {
        var key = (frame.BusIndex, frame.Frame.CanId);
        if (!config.Messages.TryGetValue(key, out var msg))
        {
            return 0;
        }

        var data = frame.Frame.Data;
        var dataLen = Math.Min(frame.Frame.DataLength, data.Length);
        var written = 0;

        for (var i = 0; i < msg.Channels.Count; i++)
        {
            var ch = msg.Channels[i];
            if (!TryExtract(data.AsSpan(0, dataLen), msg.IsBigEndian, ch, out var raw))
            {
                continue;
            }

            var baseValue = ApplyFormula(raw, ch);
            var outputValue = config.UnitConverters.TryGetValue(ch.ChannelId, out var converter)
                ? converter.Convert(baseValue)
                : baseValue;
            output[written++] = new InternalChannelValue(
                ch.ChannelId, outputValue, frame.MonotonicTicks, frame.WallTime);

            if (written >= output.Length) break;
        }

        return written;
    }

    private static double ApplyFormula(long raw, CanChannelMapping ch)
    {
        var divisor = ch.FormulaDivider == 0 ? 1d : ch.FormulaDivider;
        return raw * ch.FormulaMultiplier / divisor + ch.FormulaConst;
    }

    /// <summary>
    /// Extracts a contiguous-byte range from <paramref name="data"/> using the
    /// channel's offset/length, applies the mask, and sign-extends if signed.
    /// </summary>
    private static bool TryExtract(
        ReadOnlySpan<byte> data,
        bool isBigEndian,
        CanChannelMapping ch,
        out long value)
    {
        value = 0;
        if (ch.Length <= 0 || ch.Length > 8) return false;
        if (ch.Offset < 0 || ch.Offset + ch.Length > data.Length) return false;

        ulong raw = 0;
        if (isBigEndian)
        {
            for (var i = 0; i < ch.Length; i++)
            {
                raw = (raw << 8) | data[ch.Offset + i];
            }
        }
        else
        {
            for (var i = ch.Length - 1; i >= 0; i--)
            {
                raw = (raw << 8) | data[ch.Offset + i];
            }
        }

        if (ch.Mask != 0) raw &= ch.Mask;

        if (ch.IsSigned)
        {
            // Sign-extend from the top bit of the masked range. If a mask is set,
            // use the highest set bit of the mask; else fall back to byte length.
            var bits = ch.Mask != 0 ? HighestSetBitIndex(ch.Mask) + 1 : ch.Length * 8;
            if (bits is > 0 and < 64)
            {
                ulong signBit = 1UL << (bits - 1);
                if ((raw & signBit) != 0)
                {
                    raw |= ~((1UL << bits) - 1);
                }
            }
            value = unchecked((long)raw);
        }
        else
        {
            value = unchecked((long)raw);
        }
        return true;
    }

    private static int HighestSetBitIndex(ulong v)
    {
        var i = -1;
        while (v != 0) { v >>= 1; i++; }
        return i;
    }
}
