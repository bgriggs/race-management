using MathNet.Numerics;
using MathNet.Numerics.Interpolation;

namespace Channels.Tables;

/// <summary>
/// Convert from input value to output channel using a table. 
/// This can resolve int or string input to another string/enum.
/// This can resolve double to double, such as analog voltage to specified units, like temperature.
/// </summary>
public class TableEvaluation
{
    private readonly ITableRepository tableRepository;
    private readonly IChannelRepository channelRepository;
    private readonly IChannelDefinitionRepository channelDefinitionRepository;

    public TableEvaluation(ITableRepository tableRepository, IChannelRepository channelRepository, IChannelDefinitionRepository channelDefinitionRepository)
    {
        this.tableRepository = tableRepository;
        this.channelRepository = channelRepository;
        this.channelDefinitionRepository = channelDefinitionRepository;
    }

    public async Task EvaluateAsync()
    {
        var definitions = await tableRepository.GetMappingsAsync();
        foreach (var definition in definitions)
        {
            var inputCh = await channelRepository.GetChannelValueAsync(definition.InputChannel);
            var inputMap = await channelDefinitionRepository.GetChannelDefinitionAsync(definition.InputChannel);
            var outputMap = await channelDefinitionRepository.GetChannelDefinitionAsync(definition.OutputChannel);
            var outputValue = new ChannelValue();

            //// String, straight mapping, e.g. enum
            //// String -> string
            //if (inputMap.IsStringValue)
            //{
            //    var c = StringComparison.Ordinal;
            //    if (definition.IgnoreCase)
            //    {
            //        c = StringComparison.OrdinalIgnoreCase;
            //    }

            //    foreach (var m in definition.Mappings)    
            //    {
            //        if (string.Compare(m.Input, inputCh.Value, c) == 0)
            //        {
            //            outputValue.Value = m.Output;
            //            break;
            //        }
            //    }
            //}
            //// Integers -> string
            //if (inputMap.BaseDecimalPlaces == 0)
            //{
            //    foreach (var m in definition.Mappings)
            //    {
            //        if (int.TryParse(m.Input, out int inVal) && inVal == inputCh.GetValueInt())
            //        {
            //            outputValue.Value = m.Output;
            //            break;
            //        }
            //    }
            //}
            // Double -> double: interpolate with the table
            //else
            {
                IInterpolation? interpolate = null;
                switch (definition.InterpolationType)
                {
                    case InterpolationType.Linear:
                        interpolate = Interpolate.Linear(definition.GetInputPoints(), definition.GetOutputValues());
                        break;
                    case InterpolationType.CubicSpline:
                        interpolate = Interpolate.CubicSpline(definition.GetInputPoints(), definition.GetOutputValues());
                        break;
                    case InterpolationType.Polynomial:
                        interpolate = Interpolate.Polynomial(definition.GetInputPoints(), definition.GetOutputValues());
                        break;
                }

                var interpolatedOutput = interpolate?.Interpolate(inputCh.GetValueDouble()) ?? 0.0;
                outputValue.Value = interpolatedOutput.ToString();
            }

            await channelRepository.SetChannelValueAsync(definition.OutputChannel, outputValue);
        }
    }
}