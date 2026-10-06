namespace CasaEngine.Framework.Audio.Mixing;

/// <summary>A bus an <see cref="AudioMixerAssetValidator"/> retained, with what will be applied to it.</summary>
public sealed class AudioMixerPlanBus
{
    private readonly List<AudioMixerEffectData> _effects = new();
    private readonly List<AudioMixerSendData> _sends = new();

    internal AudioMixerPlanBus(AudioMixerBusData data, string name, string parent, bool existsLive)
    {
        Data = data;
        Name = name;
        Parent = parent;
        ExistsLive = existsLive;
    }

    /// <summary>The bus as declared in the asset.</summary>
    public AudioMixerBusData Data { get; }

    public string Name { get; }

    /// <summary>The resolved parent: Master when the asset names none, otherwise a live bus or an earlier bus of the plan.</summary>
    public string Parent { get; }

    /// <summary>True when the live mixer already holds a bus of that name (it is then neither created nor reparented).</summary>
    public bool ExistsLive { get; }

    public float Volume => Data.Volume;

    /// <summary>Retained effects, in order.</summary>
    public IReadOnlyList<AudioMixerEffectData> Effects => _effects;

    /// <summary>Retained sends, in order, with their level clamped to [0,1].</summary>
    public IReadOnlyList<AudioMixerSendData> Sends => _sends;

    internal List<AudioMixerEffectData> EffectList => _effects;

    internal List<AudioMixerSendData> SendList => _sends;
}

/// <summary>What <see cref="AudioMixerAssetValidator.Validate"/> decided to apply: buses in application order and the problems found.</summary>
public sealed class AudioMixerPlan
{
    internal AudioMixerPlan(string assetName, IReadOnlyList<AudioMixerPlanBus> buses, IReadOnlyList<string> problems)
    {
        AssetName = assetName;
        Buses = buses;
        Problems = problems;
    }

    public string AssetName { get; }

    /// <summary>Retained buses, parents first. The order follows the file for buses that do not depend on each other.</summary>
    public IReadOnlyList<AudioMixerPlanBus> Buses { get; }

    /// <summary>English texts naming the asset and the bus, one per ignored entry or noticed mismatch.</summary>
    public IReadOnlyList<string> Problems { get; }
}

/// <summary>
/// Tolerant validation of an <see cref="AudioMixerAsset"/> against the live mixer (plan decision P47): an invalid entry is
/// ignored with a problem, nothing throws. The graph rules mirror <see cref="AudioMixer"/> (parents, sends and ducking
/// relations make one graph that must stay acyclic).
/// </summary>
public static class AudioMixerAssetValidator
{
    private enum State
    {
        Unvisited,
        InProgress,
        Retained,
        Rejected,
    }

    private sealed class Candidate
    {
        public AudioMixerBusData Data;
        public string Name;
        public string Parent;
        public State State;
    }

    /// <summary>Directed graph of the signal flow: bus to parent, sender to target, ducking source to ducked bus.</summary>
    private sealed class Graph
    {
        private readonly Dictionary<string, string> _parent = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<string>> _edges = new(StringComparer.OrdinalIgnoreCase);

        public void SetParent(string bus, string parent)
        {
            if (parent != null)
            {
                _parent[bus] = parent;
            }
        }

        public void AddEdge(string from, string to)
        {
            if (!_edges.TryGetValue(from, out var list))
            {
                list = new List<string>();
                _edges.Add(from, list);
            }

            list.Add(to);
        }

        /// <summary>True when the signal of <paramref name="from"/> reaches <paramref name="goal"/>, itself included.</summary>
        public bool Reaches(string from, string goal)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<string>();
            stack.Push(from);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (string.Equals(current, goal, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (!visited.Add(current))
                {
                    continue;
                }

                if (_parent.TryGetValue(current, out var parent))
                {
                    stack.Push(parent);
                }

                if (_edges.TryGetValue(current, out var edges))
                {
                    for (var i = 0; i < edges.Count; i++)
                    {
                        stack.Push(edges[i]);
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Validates <paramref name="asset"/>. <paramref name="liveMixer"/> null means the six buses of
    /// <see cref="AudioBusNames.CreateDefaultMixer"/>; <paramref name="busCapacity"/> is the number of buses the backend holds,
    /// Master included. Never throws.
    /// </summary>
    public static AudioMixerPlan Validate(AudioMixerAsset asset, AudioMixer liveMixer, int busCapacity)
    {
        var problems = new List<string>();
        var plan = new List<AudioMixerPlanBus>();

        if (asset == null)
        {
            problems.Add("[AudioMixerAsset] the asset is null, nothing to apply");
            return new AudioMixerPlan(string.Empty, plan, problems);
        }

        var assetName = asset.Name ?? string.Empty;
        var mixer = liveMixer ?? AudioBusNames.CreateDefaultMixer();

        void Problem(string busName, string message)
        {
            problems.Add(busName == null
                ? $"[AudioMixerAsset] '{assetName}': {message}"
                : $"[AudioMixerAsset] '{assetName}': bus '{busName}': {message}");
        }

        // Live buses: actual name and parent name.
        var liveName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var liveParent = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var liveBuses = mixer.Buses;

        for (var i = 0; i < liveBuses.Count; i++)
        {
            liveName[liveBuses[i].Name] = liveBuses[i].Name;
            liveParent[liveBuses[i].Name] = liveBuses[i].Parent?.Name;
        }

        // Phase A: candidates, in file order.
        var candidates = new Dictionary<string, Candidate>(StringComparer.OrdinalIgnoreCase);
        var order = new List<Candidate>();

        for (var i = 0; i < asset.Buses.Count; i++)
        {
            var data = asset.Buses[i];

            if (data == null || string.IsNullOrWhiteSpace(data.Name))
            {
                Problem(null, "a bus without a name is ignored");
                continue;
            }

            var name = data.Name;

            if (string.Equals(name, AudioBusNames.Master, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, AudioBusNames.Editor, StringComparison.OrdinalIgnoreCase))
            {
                Problem(name, "the name is reserved by the engine, ignored");
                continue;
            }

            if (candidates.ContainsKey(name))
            {
                Problem(name, "declared twice, the first declaration wins");
                continue;
            }

            var parent = string.IsNullOrWhiteSpace(data.Parent) ? AudioBusNames.Master : data.Parent;

            if (string.Equals(parent, AudioBusNames.Master, StringComparison.OrdinalIgnoreCase))
            {
                parent = AudioBusNames.Master;
            }
            else if (string.Equals(parent, AudioBusNames.Editor, StringComparison.OrdinalIgnoreCase))
            {
                Problem(name, $"the parent '{parent}' is not allowed (the Editor bus is for previews), ignored");
                continue;
            }

            var candidate = new Candidate { Data = data, Name = name, Parent = parent };
            candidates.Add(name, candidate);
            order.Add(candidate);
        }

        // Phase B: parents first, iterative so a very deep file cannot overflow the stack.
        var retained = new Dictionary<string, AudioMixerPlanBus>(StringComparer.OrdinalIgnoreCase);
        var newCount = 0;
        var stack = new List<Candidate>();

        for (var c = 0; c < order.Count; c++)
        {
            if (order[c].State != State.Unvisited)
            {
                continue;
            }

            order[c].State = State.InProgress;
            stack.Add(order[c]);

            while (stack.Count > 0)
            {
                var current = stack[^1];
                var parentName = current.Parent;
                var parentIsMaster = string.Equals(parentName, AudioBusNames.Master, StringComparison.OrdinalIgnoreCase);

                if (!parentIsMaster)
                {
                    if (candidates.TryGetValue(parentName, out var parentCandidate))
                    {
                        if (parentCandidate.State == State.Unvisited)
                        {
                            parentCandidate.State = State.InProgress;
                            stack.Add(parentCandidate);
                            continue;
                        }

                        if (parentCandidate.State == State.InProgress)
                        {
                            // Cycle: every bus from the parent to the top of the stack is part of it.
                            var start = stack.LastIndexOf(parentCandidate);
                            for (var s = stack.Count - 1; s >= start; s--)
                            {
                                stack[s].State = State.Rejected;
                                Problem(stack[s].Name, $"its parent chain makes a cycle through '{parentName}', ignored");
                            }

                            stack.RemoveRange(start, stack.Count - start);
                            continue;
                        }

                        if (parentCandidate.State == State.Rejected)
                        {
                            current.State = State.Rejected;
                            Problem(current.Name, $"its parent '{parentName}' is ignored, so the bus is ignored");
                            stack.RemoveAt(stack.Count - 1);
                            continue;
                        }
                    }
                    else if (!liveName.ContainsKey(parentName))
                    {
                        current.State = State.Rejected;
                        Problem(current.Name, $"unknown parent '{parentName}', ignored");
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }
                }

                // The parent is usable: Master, a live bus or a retained bus.
                var existsLive = liveName.ContainsKey(current.Name);

                if (!existsLive)
                {
                    if ((long)liveBuses.Count + newCount >= busCapacity)
                    {
                        current.State = State.Rejected;
                        Problem(current.Name, $"the bus capacity of {busCapacity} is reached, ignored");
                        stack.RemoveAt(stack.Count - 1);
                        continue;
                    }

                    newCount++;
                }

                var canonicalParent = candidates.TryGetValue(parentName, out var retainedParent)
                    ? retainedParent.Name
                    : parentIsMaster ? AudioBusNames.Master : liveName[parentName];
                var planBus = new AudioMixerPlanBus(current.Data, current.Name, canonicalParent, existsLive);
                plan.Add(planBus);
                retained.Add(current.Name, planBus);
                current.State = State.Retained;
                stack.RemoveAt(stack.Count - 1);
            }
        }

        // The graph of the mixer after the application: live parents win over the asset's for the buses that exist.
        var graph = new Graph();

        for (var i = 0; i < liveBuses.Count; i++)
        {
            graph.SetParent(liveBuses[i].Name, liveBuses[i].Parent?.Name);
        }

        for (var i = 0; i < plan.Count; i++)
        {
            var planBus = plan[i];

            if (planBus.ExistsLive)
            {
                var actualParent = liveParent[planBus.Name];

                if (!string.Equals(actualParent, planBus.Parent, StringComparison.OrdinalIgnoreCase))
                {
                    Problem(null, $"bus '{planBus.Name}' is live under '{actualParent}', the asset says '{planBus.Parent}' (a live bus is never reparented)");
                }
            }
            else
            {
                graph.SetParent(planBus.Name, planBus.Parent);
            }
        }

        string Existing(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            if (retained.TryGetValue(name, out var bus))
            {
                return bus.Name;
            }

            return liveName.TryGetValue(name, out var live) ? live : null;
        }

        // Phase D: effects then sends, bus by bus in plan order (the order the applier uses, so the cycle checks agree).
        for (var i = 0; i < plan.Count; i++)
        {
            var planBus = plan[i];
            var data = planBus.Data;
            var seenTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var e = 0; e < data.Effects.Count; e++)
            {
                var effect = data.Effects[e];

                if (effect == null)
                {
                    continue;
                }

                if (planBus.EffectList.Count >= AudioBus.MaxEffects)
                {
                    Problem(planBus.Name, $"more than {AudioBus.MaxEffects} effects, the extra ones are ignored");
                    break;
                }

                if (effect is AudioMixerDuckingEffectData ducking)
                {
                    var source = Existing(ducking.Source);

                    if (source == null)
                    {
                        Problem(planBus.Name, $"ducking source '{ducking.Source}' does not exist, effect ignored");
                        continue;
                    }

                    if (string.Equals(source, planBus.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        Problem(planBus.Name, "a ducking cannot use its own bus as source, effect ignored");
                        continue;
                    }

                    // The relation is an edge source -> bus: it makes a cycle when the bus already reaches the source.
                    if (graph.Reaches(planBus.Name, source))
                    {
                        Problem(planBus.Name, $"a ducking by '{source}' would make a cycle, effect ignored");
                        continue;
                    }

                    graph.AddEdge(source, planBus.Name);
                }

                planBus.EffectList.Add(effect);
            }

            for (var s = 0; s < data.Sends.Count; s++)
            {
                var send = data.Sends[s];

                if (send == null)
                {
                    continue;
                }

                var target = Existing(send.Target);

                if (target == null)
                {
                    Problem(planBus.Name, $"send target '{send.Target}' does not exist, send ignored");
                    continue;
                }

                if (!seenTargets.Add(target))
                {
                    Problem(planBus.Name, $"a second send to '{target}', the first one wins");
                    continue;
                }

                if (string.Equals(target, planBus.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Problem(planBus.Name, "a bus cannot send to itself, send ignored");
                    continue;
                }

                if (float.IsNaN(send.Level))
                {
                    Problem(planBus.Name, $"the level of the send to '{target}' is not a number, send ignored");
                    continue;
                }

                var level = Math.Clamp(send.Level, AudioVoiceParameters.MinVolume, AudioVoiceParameters.MaxVolume);

                if (level <= 0f)
                {
                    Problem(planBus.Name, $"the send to '{target}' has a level of 0, send ignored");
                    continue;
                }

                if (planBus.SendList.Count >= AudioBus.MaxSends)
                {
                    Problem(planBus.Name, $"more than {AudioBus.MaxSends} sends, the extra ones are ignored");
                    break;
                }

                // The send is an edge bus -> target: it makes a cycle when the target already reaches the bus.
                if (graph.Reaches(target, planBus.Name))
                {
                    Problem(planBus.Name, $"a send to '{target}' would make a cycle, send ignored");
                    continue;
                }

                graph.AddEdge(planBus.Name, target);
                planBus.SendList.Add(new AudioMixerSendData(target, level));
            }
        }

        return new AudioMixerPlan(assetName, plan, problems);
    }

    /// <summary>
    /// True when an edge from <paramref name="fromBus"/> to <paramref name="toBus"/> (a send, or a ducking whose source is
    /// <paramref name="fromBus"/> and whose bus is <paramref name="toBus"/>) would make a cycle in the graph of the asset:
    /// <paramref name="toBus"/> already reaches <paramref name="fromBus"/> through parents, sends and ducking relations, or
    /// they are the same bus. Reads the asset as written (first declaration of a name wins, no other validation).
    /// </summary>
    public static bool WouldMakeCycle(AudioMixerAsset asset, string fromBus, string toBus)
    {
        if (string.IsNullOrWhiteSpace(fromBus) || string.IsNullOrWhiteSpace(toBus))
        {
            return false;
        }

        if (string.Equals(fromBus, toBus, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (asset == null)
        {
            return false;
        }

        var graph = new Graph();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < asset.Buses.Count; i++)
        {
            var bus = asset.Buses[i];

            if (bus == null || string.IsNullOrWhiteSpace(bus.Name) || !seen.Add(bus.Name))
            {
                continue;
            }

            if (!string.Equals(bus.Name, AudioBusNames.Master, StringComparison.OrdinalIgnoreCase))
            {
                graph.SetParent(bus.Name, string.IsNullOrWhiteSpace(bus.Parent) ? AudioBusNames.Master : bus.Parent);
            }

            for (var s = 0; s < bus.Sends.Count; s++)
            {
                if (bus.Sends[s] != null && !string.IsNullOrWhiteSpace(bus.Sends[s].Target))
                {
                    graph.AddEdge(bus.Name, bus.Sends[s].Target);
                }
            }

            for (var e = 0; e < bus.Effects.Count; e++)
            {
                if (bus.Effects[e] is AudioMixerDuckingEffectData ducking && !string.IsNullOrWhiteSpace(ducking.Source))
                {
                    graph.AddEdge(ducking.Source, bus.Name);
                }
            }
        }

        return graph.Reaches(toBus, fromBus);
    }
}
