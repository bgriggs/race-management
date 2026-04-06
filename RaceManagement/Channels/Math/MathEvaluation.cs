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
        var parameters = await mathRepository.GetParameters();

        foreach (var p in parameters.OrderBy(p => p.Order))
        {
            double output = 0;
            var channel1 = await GetChannelQuantity(p.Channel1Id) ?? throw new InvalidOperationException($"Channel {p.Channel1Id} not found");
            switch (p.Type)
            {
                // Output = value1 / (value1 + value2)
                case MathType.Bias:
                    var channel2 = await GetChannelQuantity(p.Channel2Id) ?? throw new InvalidOperationException($"Channel {p.Channel2Id} not found");
                    output = (double)channel1.Value / ((double)channel1.Value + (double)channel2.Value);
                    break;

                // Output = (value1 * a) + b
                case MathType.LinearCorrector:
                    var v = (double)channel1.Value;
                    var a = (double)p.A;
                    var b = (double)p.B;
                    var inter = v * a;
                    output = inter + b;

                    output = ((double)channel1.Value * (double)p.A) + (double)p.B;
                    break;

                // Output = value1 +-*/ value2
                case MathType.SimpleOperation:
                    double value2 = (double)p.A;
                    if (p.Channel2Id > 0)
                    {
                        var ch2 = await GetChannelQuantity(p.Channel2Id) ?? throw new InvalidOperationException($"Channel {p.Channel2Id} not found");
                        value2 = (double)ch2.Value;
                    }
                    switch (p.SimpleOperationType)
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
                    output = System.Math.Truncate((double)channel1.Value / ((double)p.A));
                    break;

                // Output = value1 % a
                case MathType.DivisionModulo:
                    output = System.Math.Truncate((double)channel1.Value % ((double)p.A));
                    break;
            }

            var outputChMap = await channelDefinitionRepository.GetChannelDefinitionAsync(p.OutputChannelId);
            var outputValue = new ChannelValue { Id = p.OutputChannelId };
            outputValue.SetBaseValue(output, outputChMap);
            await channelRepository.SetChannelValueAsync(outputValue);
        }
    }

    private async Task<IQuantity?> GetChannelQuantity(int chId)
    {
        var ch = await channelRepository.GetChannelValueAsync(chId);
        var map = await channelDefinitionRepository.GetChannelDefinitionAsync(chId);
        return ch.GetOutputQuantity(map);
    }
}
