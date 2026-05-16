using UnitsNet;

namespace Channels.Math;

public class MathEvaluation
{
    private readonly IMathRepository mathRepository;
    private readonly IChannelRepository channelRepository;
    private readonly IChannelDefinitionRepository channelDefinitionRepository;

    
    public MathEvaluation(IMathRepository mathRepository, IChannelRepository channelRepository, IChannelDefinitionRepository channelDefinitionRepository)
    {
        this.mathRepository = mathRepository;
        this.channelRepository = channelRepository;
        this.channelDefinitionRepository = channelDefinitionRepository;
    }


    public async Task RunCalculationsAsync()
    {
        var definitions = await mathRepository.GetDefinitionsAsync();

        foreach (var definition in definitions)
        {
            double output = 0;
            var channel1 = await GetChannelQuantity(definition.Channel1Id) ?? throw new InvalidOperationException($"Channel {definition.Channel1Id} not found");
            switch (definition.Type)
            {
                // Output = value1 / (value1 + value2)
                case MathType.Bias:
                    var channel2 = await GetChannelQuantity(definition.Channel2Id ?? Guid.Empty) ?? throw new InvalidOperationException($"Channel {definition.Channel2Id} not found");
                    output = (double)channel1.Value / ((double)channel1.Value + (double)channel2.Value);
                    break;

                // Output = (value1 * a) + b
                case MathType.LinearCorrector:
                    var v = (double)channel1.Value;
                    var a = (double)definition.A;
                    var b = (double)definition.B;
                    var inter = v * a;
                    output = inter + b;

                    output = ((double)channel1.Value * (double)definition.A) + (double)definition.B;
                    break;

                // Output = value1 +-*/ value2
                case MathType.SimpleOperation:
                    double value2 = (double)definition.A;
                    if (definition.Channel2Id != Guid.Empty)
                    {
                        var ch2 = await GetChannelQuantity(definition.Channel2Id ?? Guid.Empty) ?? throw new InvalidOperationException($"Channel {definition.Channel2Id} not found");
                        value2 = (double)ch2.Value;
                    }
                    switch (definition.SimpleOperationType)
                    {
                        case SimpleOperationType.Add:
                            output = (double)channel1.Value + value2;
                            break;
                        case SimpleOperationType.Subtract:
                            output = (double)channel1.Value - value2;
                            break;
                        case SimpleOperationType.Multiply:
                            output = (double)channel1.Value * value2;
                            break;
                        case SimpleOperationType.Divide:
                            output = (double)channel1.Value / value2;
                            break;
                    }
                    break;

                // Output = trunc(value1 / a)
                case MathType.DivisionInteger:
                    output = System.Math.Truncate((double)channel1.Value / ((double)definition.A));
                    break;

                // Output = value1 % a
                case MathType.DivisionModulo:
                    output = System.Math.Truncate((double)channel1.Value % ((double)definition.A));
                    break;
            }

            var outputChMap = await channelDefinitionRepository.GetChannelDefinitionAsync(definition.OutputChannelId);
            var outputValue = new ChannelValue();
            outputValue.SetBaseValue(output);
            await channelRepository.SetChannelValueAsync(definition.OutputChannelId, outputValue);
        }
    }

    private async Task<IQuantity?> GetChannelQuantity(Guid chId)
    {
        var ch = await channelRepository.GetChannelValueAsync(chId);
        var map = await channelDefinitionRepository.GetChannelDefinitionAsync(chId);
        return ch.GetOutputQuantity(map);
    }
}
