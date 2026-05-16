using Channels;
using Channels.Counters;
using Channels.Math;
using Channels.Tables;
using Channels.Timers;
using Channels.UserConditions;

namespace Racecar.Pipeline;

/// <summary>
/// Re-evaluates derived channels (math, logic, tables, counters, timers, user
/// conditions) whose inputs changed in the latest frame. The implementation
/// lives in the <c>Channels</c> project; the pipeline depends only on this
/// contract.
/// </summary>
public interface IDerivedChannelEvaluator
{
    /// <summary>
    /// Given the channel values that just changed, compute and return all
    /// derived channel values whose inputs depend on those IDs, in
    /// topological order. The returned values inherit the supplied timestamps.
    /// </summary>
    InternalChannelValue[] Evaluate(
        ReadOnlySpan<InternalChannelValue> changedInputs,
        long monotonicTicks,
        DateTime wallTime);
}

/// <summary>
/// Default no-op implementation used until the real evaluator is wired in.
/// </summary>
public sealed class NoOpDerivedEvaluator : IDerivedChannelEvaluator
{
    public static NoOpDerivedEvaluator Instance { get; } = new();

    public InternalChannelValue[] Evaluate(
        ReadOnlySpan<InternalChannelValue> changedInputs,
        long monotonicTicks,
        DateTime wallTime) => [];
}

/// <summary>
/// Real derived-channel evaluator. Wraps the existing <c>Channels</c> project
/// evaluation classes (Math, Tables, Timers, Counters, UserConditions) and runs
/// them against an in-memory channel repo that is kept in sync with the
/// pipeline's <see cref="InternalChannelValue"/> updates.
/// </summary>
/// <remarks>
/// All memory-backed repos complete synchronously so the <c>.GetAwaiter().GetResult()</c>
/// calls are safe on the pipeline worker thread.
/// </remarks>
public sealed class PipelineDerivedChannelEvaluator : IDerivedChannelEvaluator
{
    private readonly IReadOnlyDictionary<int, Guid> _idToGuid;
    private readonly IReadOnlyDictionary<Guid, int> _guidToId;
    private readonly IReadOnlyDictionary<int, ChannelDefinition> _channels;
    private readonly IReadOnlySet<Guid> _derivedOutputGuids;

    private readonly SyncChannelRepository _channelRepo;
    private readonly ChannelDefinitionMemoryRepository _defRepo;

    private readonly MathEvaluation _math;
    private readonly TableEvaluation _table;
    private readonly TimerEvaluation _timer;
    private readonly CounterEvaluation _counter;
    private readonly ConditionEvaluation _condition;

    private readonly Dictionary<Guid, string> _lastDerivedValues = new();

    public PipelineDerivedChannelEvaluator(
        IReadOnlyDictionary<int, Guid> idToGuid,
        IReadOnlyDictionary<Guid, int> guidToId,
        IReadOnlyDictionary<int, ChannelDefinition> channels,
        IReadOnlyList<MathDefinition> mathDefs,
        IReadOnlyList<TableDefinition> tableDefs,
        IReadOnlyList<TimerDefinition> timerDefs,
        IReadOnlyList<CounterDefinition> counterDefs,
        IReadOnlyList<ConditionDefinition> conditionDefs,
        TimeProvider? timeProvider = null)
    {
        _idToGuid = idToGuid;
        _guidToId = guidToId;
        _channels = channels;
        _channelRepo = new SyncChannelRepository();
        _defRepo = new ChannelDefinitionMemoryRepository();
        foreach (var (_, def) in channels) _defRepo.Set(def);

        var mathRepo = new MathMemoryRepository();
        foreach (var d in mathDefs) mathRepo.Add(d);

        var tableRepo = new TableMemoryRepository();
        foreach (var d in tableDefs) tableRepo.Add(d);

        var timerRepo = new TimerMemoryRepository();
        foreach (var d in timerDefs) timerRepo.AddTimer(d);

        var counterRepo = new CounterMemoryRepository();
        foreach (var d in counterDefs) counterRepo.Add(d);

        var conditionRepo = new ConditionMemoryRepository();
        foreach (var d in conditionDefs) conditionRepo.Add(d);

        _math = new MathEvaluation(mathRepo, _channelRepo, _defRepo);
        _table = new TableEvaluation(tableRepo, _channelRepo, _defRepo);
        _timer = new TimerEvaluation(timerRepo, _channelRepo, _defRepo, timeProvider);
        _counter = new CounterEvaluation(counterRepo, _channelRepo);
        _condition = new ConditionEvaluation(conditionRepo, _channelRepo, _defRepo, timeProvider);

        var outputs = new HashSet<Guid>();
        foreach (var d in mathDefs) if (d.OutputChannelId != Guid.Empty) outputs.Add(d.OutputChannelId);
        foreach (var d in tableDefs) if (d.OutputChannel != Guid.Empty) outputs.Add(d.OutputChannel);
        foreach (var d in timerDefs) if (d.OutputChId != Guid.Empty) outputs.Add(d.OutputChId);
        foreach (var d in counterDefs) if (d.OutputChId != Guid.Empty) outputs.Add(d.OutputChId);
        foreach (var d in conditionDefs) if (d.OutputChannelId != Guid.Empty) outputs.Add(d.OutputChannelId);
        _derivedOutputGuids = outputs;
    }

    public InternalChannelValue[] Evaluate(
        ReadOnlySpan<InternalChannelValue> changedInputs,
        long monotonicTicks,
        DateTime wallTime)
    {
        if (_derivedOutputGuids.Count == 0) return [];

        for (var i = 0; i < changedInputs.Length; i++)
        {
            ref readonly var v = ref changedInputs[i];
            if (!_idToGuid.TryGetValue(v.ChannelId, out var guid)) continue;
            if (!_channels.TryGetValue(v.ChannelId, out var def)) continue;
            var cv = new ChannelValue { SessionIndex = (ushort)v.ChannelId };
            cv.SetBaseValue(v.BaseValue);
            _channelRepo.Set(guid, cv);
        }

        _math.RunCalculationsAsync().GetAwaiter().GetResult();
        _table.EvaluateAsync().GetAwaiter().GetResult();
        _timer.UpdateTimersAsync().GetAwaiter().GetResult();
        _counter.UpdateCountersAsync().GetAwaiter().GetResult();
        _condition.UpdateAsync().GetAwaiter().GetResult();

        var result = new List<InternalChannelValue>();
        foreach (var guid in _derivedOutputGuids)
        {
            var cv = _channelRepo.Get(guid);
            if (cv is null || cv.Value == string.Empty) continue;
            _lastDerivedValues.TryGetValue(guid, out var prev);
            if (cv.Value == prev) continue;
            _lastDerivedValues[guid] = cv.Value;
            if (!_guidToId.TryGetValue(guid, out var channelId)) continue;
            if (!double.TryParse(cv.Value, out var baseValue)) continue;
            result.Add(new InternalChannelValue(channelId, baseValue, monotonicTicks, wallTime));
        }
        return result.Count == 0 ? [] : [.. result];
    }
}

/// <summary>
/// Thread-unsafe synchronous in-memory channel repo for use solely on the
/// pipeline worker thread.
/// </summary>
internal sealed class SyncChannelRepository : IChannelRepository
{
    private readonly Dictionary<Guid, ChannelValue> _values = new();

    public void Set(Guid id, ChannelValue value) => _values[id] = value;

    public ChannelValue? Get(Guid id) =>
        _values.TryGetValue(id, out var v) ? v : null;

    public Task<ChannelValue> GetChannelValueAsync(Guid channelId)
    {
        var v = _values.TryGetValue(channelId, out var val) ? val : new ChannelValue();
        return Task.FromResult(v);
    }

    public Task SetChannelValueAsync(Guid channelId, ChannelValue ch)
    {
        _values[channelId] = ch;
        return Task.CompletedTask;
    }
}

