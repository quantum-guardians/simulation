using System.Collections.Generic;

/// <summary>
/// Stores paired pedestrian agent lists: the Unity runtime data (GameObject, transform, path)
/// and the domain-level social force model state. Indices are kept in sync so that
/// Agents[i] and SfmAgents[i] always refer to the same pedestrian.
///
/// Also provides O(1) agent lookup by ID via an internal dictionary.
/// </summary>
public sealed class PedestrianAgentStore
{
    readonly Dictionary<int, int> _indexById = new();

    /// <summary>Unity-facing agent runtime data (transform, renderer, path state).</summary>
    public readonly List<PedestrianAgentRuntime> Agents = new();

    /// <summary>Domain social force model state (position, velocity, forces, pressure).</summary>
    public readonly List<SocialForceAgentState> SfmAgents = new();

    public int Count => Agents.Count;

    public bool ContainsId(int id) => _indexById.ContainsKey(id);

    /// <summary>Look up the list index for a given agent ID.</summary>
    public bool TryGetIndex(int id, out int index) => _indexById.TryGetValue(id, out index);

    /// <summary>Adds a new agent, pairing its runtime and SFM state at the same index.</summary>
    public void Add(PedestrianAgentRuntime agent, SocialForceAgentState sfm)
    {
        _indexById[agent.Id] = Agents.Count;
        Agents.Add(agent);
        SfmAgents.Add(sfm);
    }

    /// <summary>Clears all agents and the ID lookup.</summary>
    public void Clear()
    {
        _indexById.Clear();
        Agents.Clear();
        SfmAgents.Clear();
    }

    /// <summary>
    /// Removes an agent at the given index using swap-and-pop.
    /// Swaps the last agent into the removed slot to keep both lists dense
    /// (the SFM simulator iterates by index, so no gaps allowed).
    /// </summary>
    public void RemoveAt(int index)
    {
        var last = Agents.Count - 1;
        var removedId = Agents[index].Id;
        _indexById.Remove(removedId);

        if (index != last)
        {
            Agents[index] = Agents[last];
            SfmAgents[index] = SfmAgents[last];
            _indexById[Agents[index].Id] = index;
        }

        Agents.RemoveAt(last);
        SfmAgents.RemoveAt(last);
    }
}
