using System.Globalization;
using UnitsNet;

namespace Channels.Logic;

public class LogicEvaluation
{
    private readonly IChannelRepository channelRepository;
    private readonly IStatementRepository statementRepository;
    private readonly IChannelDefinitionRepository channelDefinitionRepository;
    private readonly IStatementStateRepository statementStateRepository;
    private readonly IComparisonDurationRepository comparisonDurationRepository;
    private readonly IPreviousChannelValueRepository previousChannelValueRepository;
    private readonly TimeProvider timeProvider;

    public LogicEvaluation(
        IChannelRepository channelRepository,
        IChannelDefinitionRepository channelDefinitionRepository,
        IStatementRepository statementRepository,
        IStatementStateRepository? statementStateRepository = null,
        IComparisonDurationRepository? comparisonDurationRepository = null,
        IPreviousChannelValueRepository? previousChannelValueRepository = null,
        TimeProvider? timeProvider = null)
    {
        this.channelRepository = channelRepository;
        this.channelDefinitionRepository = channelDefinitionRepository;
        this.statementRepository = statementRepository;
        this.statementStateRepository = statementStateRepository ?? new StatementStateMemoryRepository();
        this.comparisonDurationRepository = comparisonDurationRepository ?? new ComparisonDurationMemoryRepository();
        this.previousChannelValueRepository = previousChannelValueRepository ?? new PreviousChannelValueMemoryRepository();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<bool> EvaluateAsync(int statementsId)
    {
        var statements = await statementRepository.GetStatementsAsync(statementsId);

        if (statements.DeactivateComparisons is { Count: > 0 })
        {
            // With separate deactivate comparisons, deactivation takes priority.
            // If neither fires, the previous state is retained.
            bool deactivate = await EvaluateComparisonsAsync(statements.DeactivateComparisons);
            if (deactivate)
            {
                await statementStateRepository.SetStateAsync(statementsId, false);
                return false;
            }

            bool activate = await EvaluateComparisonsAsync(statements.ActivateComparisons);
            if (activate)
            {
                await statementStateRepository.SetStateAsync(statementsId, true);
                return true;
            }

            // Neither triggered — maintain previous state (default false)
            return await statementStateRepository.GetStateAsync(statementsId) ?? false;
        }

        // No deactivate comparisons: activate result directly determines state.
        // When false, the statement deactivates.
        bool result = await EvaluateComparisonsAsync(statements.ActivateComparisons);
        await statementStateRepository.SetStateAsync(statementsId, result);
        return result;
    }

    /// <summary>
    /// Evaluates comparison groups with OR logic across groups and AND logic within each group.
    /// Any single group evaluating to true causes the overall result to be true.
    /// </summary>
    private async Task<bool> EvaluateComparisonsAsync(List<List<Comparison>> comparisonGroups)
    {
        foreach (var group in comparisonGroups)
        {
            if (group.Count == 0) continue;

            bool groupResult = true;
            foreach (var comparison in group)
            {
                if (!await EvaluateComparisonAsync(comparison))
                {
                    groupResult = false;
                    break;
                }
            }

            if (groupResult) return true;
        }

        return false;
    }

    private async Task<bool> EvaluateComparisonAsync(Comparison comparison)
    {
        bool result = comparison.Logic switch
        {
            LogicType.True => true,
            LogicType.False => false,
            LogicType.Updated => await EvaluateUpdatedAsync(comparison),
            LogicType.ChangedBy => await EvaluateChangedByAsync(comparison),
            _ => await EvaluateRelationalAsync(comparison),
        };

        // The comparison must remain true for the specified duration before it is considered true.
        if (comparison.ForMs > 0)
        {
            result = await EvaluateForDurationAsync(comparison.ComparisonId, result, comparison.ForMs);
        }

        return comparison.ReverseResult ? !result : result;
    }

    /// <summary>
    /// Evaluates relational comparisons (GT, LT, GTE, LTE, EQ) between a channel value and either
    /// a static value or another channel's value. Uses UnitsNet for unit-aware conversion when both
    /// channels have units defined.
    /// </summary>
    private async Task<bool> EvaluateRelationalAsync(Comparison comparison)
    {
        var (leftValue, leftUnit) = await ResolveChannelWithUnitAsync(comparison.ChannelId);
        double rightValue;

        if (comparison.UseStaticComparison)
        {
            // Static values are assumed to be expressed in the same unit as the compared channel.
            rightValue = double.Parse(comparison.StaticValueComparison, CultureInfo.InvariantCulture);
        }
        else if (comparison.ChannelComparisonId.HasValue)
        {
            var (rawRight, rightUnit) = await ResolveChannelWithUnitAsync(comparison.ChannelComparisonId.Value);
            rightValue = ConvertForComparison(rawRight, rightUnit, leftUnit);
        }
        else
        {
            throw new InvalidOperationException(
                $"Comparison {comparison.ComparisonId} requires either a static value or a comparison channel.");
        }

        return Compare(leftValue, rightValue, comparison.Logic);
    }

    /// <summary>
    /// Evaluates whether a channel value has changed since the previous evaluation.
    /// </summary>
    private async Task<bool> EvaluateUpdatedAsync(Comparison comparison)
    {
        var channelValue = await channelRepository.GetChannelValueAsync(comparison.ChannelId);
        var previous = await previousChannelValueRepository.GetPreviousValueAsync(comparison.ChannelId);
        bool updated = previous != null && previous != channelValue.Value;
        await previousChannelValueRepository.SetPreviousValueAsync(comparison.ChannelId, channelValue.Value);
        return updated;
    }

    /// <summary>
    /// Evaluates whether a channel value has changed by at least a threshold amount since
    /// the previous evaluation. The threshold comes from the static value or comparison channel.
    /// </summary>
    private async Task<bool> EvaluateChangedByAsync(Comparison comparison)
    {
        var (currentValue, currentUnit) = await ResolveChannelWithUnitAsync(comparison.ChannelId);
        var previousStr = await previousChannelValueRepository.GetPreviousValueAsync(comparison.ChannelId);

        if (previousStr is null)
        {
            // No previous value — record and return false on first evaluation.
            await previousChannelValueRepository.SetPreviousValueAsync(comparison.ChannelId, currentValue.ToString(CultureInfo.InvariantCulture));
            return false;
        }

        double previousValue = double.Parse(previousStr, CultureInfo.InvariantCulture);
        await previousChannelValueRepository.SetPreviousValueAsync(comparison.ChannelId, currentValue.ToString(CultureInfo.InvariantCulture));

        double threshold;
        if (comparison.UseStaticComparison)
        {
            threshold = double.Parse(comparison.StaticValueComparison, CultureInfo.InvariantCulture);
        }
        else if (comparison.ChannelComparisonId.HasValue)
        {
            var (rawThreshold, thresholdUnit) = await ResolveChannelWithUnitAsync(comparison.ChannelComparisonId.Value);
            threshold = ConvertForComparison(rawThreshold, thresholdUnit, currentUnit);
        }
        else
        {
            throw new InvalidOperationException(
                $"Comparison {comparison.ComparisonId} (ChangedBy) requires either a static threshold or a comparison channel.");
        }

        return System.Math.Abs(currentValue - previousValue) >= threshold;
    }

    /// <summary>
    /// Enforces that a comparison must remain true for a minimum duration before it is considered true.
    /// Resets the timer when the comparison becomes false.
    /// </summary>
    private async Task<bool> EvaluateForDurationAsync(int comparisonId, bool currentResult, int requiredMs)
    {
        var now = timeProvider.GetUtcNow();

        if (!currentResult)
        {
            await comparisonDurationRepository.RemoveStartTimeAsync(comparisonId);
            return false;
        }

        var startTime = await comparisonDurationRepository.GetStartTimeAsync(comparisonId);
        if (startTime is null)
        {
            await comparisonDurationRepository.SetStartTimeAsync(comparisonId, now);
            return false; // Duration requirement not yet met
        }

        return (now - startTime.Value).TotalMilliseconds >= requiredMs;
    }

    private async Task<(double Value, string UnitType)> ResolveChannelWithUnitAsync(int channelId)
    {
        var channelValue = await channelRepository.GetChannelValueAsync(channelId);
        var channelDef = await channelDefinitionRepository.GetChannelDefinitionAsync(channelId);
        double value = double.Parse(channelValue.Value, CultureInfo.InvariantCulture);
        return (value, channelDef.BaseUnitType);
    }

    /// <summary>
    /// Converts a value from one unit to another for comparison.
    /// When both units are empty (dimensionless channels), the raw value is returned unchanged.
    /// Throws <see cref="IncompatibleUnitException"/> when the units belong to different quantity types
    /// (e.g., comparing Temperature against Speed).
    /// </summary>
    private static double ConvertForComparison(double value, string sourceUnit, string targetUnit)
    {
        bool sourceEmpty = string.IsNullOrEmpty(sourceUnit);
        bool targetEmpty = string.IsNullOrEmpty(targetUnit);

        if (sourceEmpty && targetEmpty)
            return value;

        if (sourceEmpty || targetEmpty)
            throw new IncompatibleUnitException(
                sourceUnit ?? string.Empty,
                targetUnit ?? string.Empty,
                "Cannot compare a dimensionless value with a unit-bearing value.");

        if (sourceUnit == targetUnit)
            return value;

        IQuantity sourceQuantity = Quantity.FromUnitAbbreviation(value, sourceUnit);
        IQuantity targetReference = Quantity.FromUnitAbbreviation(0, targetUnit);

        if (sourceQuantity.QuantityInfo.Name != targetReference.QuantityInfo.Name)
            throw new IncompatibleUnitException(
                sourceUnit,
                targetUnit,
                $"Cannot compare {sourceQuantity.QuantityInfo.Name} ({sourceUnit}) with {targetReference.QuantityInfo.Name} ({targetUnit}).");

        return sourceQuantity.ToUnit(targetReference.Unit).Value;
    }

    private static bool Compare(double left, double right, LogicType logic) => logic switch
    {
        LogicType.GreaterThan => left > right,
        LogicType.LessThan => left < right,
        LogicType.GreaterThanOrEqualTo => left >= right,
        LogicType.LessThanOrEqualTo => left <= right,
        LogicType.EqualTo => left == right,
        _ => throw new InvalidOperationException($"Unsupported relational logic type: {logic}"),
    };
}
