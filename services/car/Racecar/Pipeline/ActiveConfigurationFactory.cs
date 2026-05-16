using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Channels;
using Common;
using Common.CanBus;

namespace Racecar.Pipeline;

/// <summary>
/// Converts a <see cref="CarConfiguration"/> into an <see cref="ActiveConfiguration"/>
/// ready for use by the pipeline.
/// </summary>
public sealed class ActiveConfigurationFactory
{
    private readonly TimeProvider _time;

    public ActiveConfigurationFactory(TimeProvider time)
    {
        _time = time;
    }

    public ActiveConfiguration Build(CarConfiguration carConfig)
    {
        // 1. Assign compact int channel IDs (session indices) in definition list order.
        var idToGuid = new Dictionary<int, Guid>();
        var guidToId = new Dictionary<Guid, int>();
        var channels = new Dictionary<int, ChannelDefinition>();

        for (var i = 0; i < carConfig.ChannelDefinitions.Count; i++)
        {
            var def = carConfig.ChannelDefinitions[i];
            idToGuid[i] = def.Id;
            guidToId[def.Id] = i;
            channels[i] = def;
        }

        // 2. Build CAN message-to-channel mappings.
        var messages = new Dictionary<(int Bus, uint Id), CanMessageMapping>();
        for (var busIdx = 0; busIdx < carConfig.CanConfig.Interfaces.Count; busIdx++)
        {
            if (busIdx >= carConfig.CanConfig.CanBusEnabled.Count ||
                !carConfig.CanConfig.CanBusEnabled[busIdx])
            {
                continue;
            }

            var iface = carConfig.CanConfig.Interfaces[busIdx];
            foreach (var msg in iface.Messages.Where(m => m.IsEnabled && m.IsReceive))
            {
                var channelMappings = new List<CanChannelMapping>();
                foreach (var asgn in msg.ChannelAssignments)
                {
                    // CanChannelAssignmentConfig.Id == ChannelDefinition.Id (Guid)
                    if (!guidToId.TryGetValue(asgn.Id, out var channelId)) continue;
                    channelMappings.Add(new CanChannelMapping(
                        ChannelId: channelId,
                        Offset: asgn.Offset,
                        Length: asgn.Length,
                        Mask: asgn.Mask,
                        IsSigned: asgn.IsSigned,
                        IsBigEndian: msg.IsBigEndian,
                        FormulaMultiplier: asgn.FormulaMultiplier,
                        FormulaDivider: asgn.FormulaDivider,
                        FormulaConst: asgn.FormulaConst));
                }
                if (channelMappings.Count == 0) continue;
                var key = (busIdx, (uint)msg.CanId);
                messages[key] = new CanMessageMapping(
                    busIdx, (uint)msg.CanId, msg.Length, msg.IsBigEndian, channelMappings);
            }
        }

        // 3. Deadbands: use display resolution for analog channels, 0 for integer/string.
        var deadbands = new Dictionary<int, double>();
        for (var i = 0; i < carConfig.ChannelDefinitions.Count; i++)
        {
            var def = carConfig.ChannelDefinitions[i];
            if (def.BaseDecimalPlaces == 0)
            {
                deadbands[i] = 0d;
            }
            else
            {
                deadbands[i] = System.Math.Pow(10, -def.OutputDecimalPlaces);
            }
        }

        // 4. Per-channel definition hashes (used for state migration on reload).
        var definitionHashes = new Dictionary<int, ulong>();
        for (var i = 0; i < carConfig.ChannelDefinitions.Count; i++)
        {
            definitionHashes[i] = ComputeHash(carConfig.ChannelDefinitions[i]);
        }

        // 5. Build derived evaluator.
        var evaluator = new PipelineDerivedChannelEvaluator(
            idToGuid, guidToId, channels,
            carConfig.MathDefinitions,
            carConfig.TableDefinitions,
            carConfig.TimerDefinitions,
            carConfig.CounterDefinitions,
            carConfig.UserConditions,
            _time);

        return new ActiveConfiguration
        {
            ConfigurationId = carConfig.ConfigurationId,
            Channels = channels,
            ChannelIdsByDefinition = guidToId,
            Messages = messages,
            Deadbands = deadbands,
            DefinitionHashes = definitionHashes,
            DerivedEvaluator = evaluator,
        };
    }

    private static ulong ComputeHash(ChannelDefinition def)
    {
        var json = JsonSerializer.Serialize(def);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return BitConverter.ToUInt64(bytes, 0);
    }
}
