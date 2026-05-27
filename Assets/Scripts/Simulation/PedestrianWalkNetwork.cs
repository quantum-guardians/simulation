using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the walkable road network: segments, node positions, A* edges,
/// and wall boundaries for the social force model.
///
/// The network is rebuilt when road geometry changes (graph confirmed).
/// Segments are one-directional (A→B); reverse travel uses separate reverse segments.
/// </summary>
public sealed class PedestrianWalkNetwork
{
    readonly List<int> _segmentCandidates = new(4);

    /// <summary>All walk segments (one per directed edge).</summary>
    public readonly List<WalkSegment> Segments = new();

    /// <summary>Outgoing segment indices per node ID.</summary>
    public readonly Dictionary<int, List<int>> Outgoing = new();

    /// <summary>A* edge list: per-node, list of (neighborNode, cost) edges.</summary>
    public readonly Dictionary<int, List<(int to, float cost)>> AstarEdges = new();

    /// <summary>World position per node ID (for nearest-node lookups).</summary>
    public readonly Dictionary<int, Vector3> NodeWorld = new();

    /// <summary>Cached list of all node IDs, refreshed on rebuild.</summary>
    public List<int> NodeIds { get; private set; } = new();

    /// ---- Wall boundary system ----

    /// <summary>All wall segments (edge walls + junction walls) in one flat list.</summary>
    public readonly List<SocialForceWallSegment> AllWalls = new();

    /// <summary>Edge key → (left wall index, right wall index) into AllWalls.</summary>
    public readonly Dictionary<(int min, int max), (int leftIdx, int rightIdx)> EdgeWallMap = new();

    /// <summary>Node ID → list of junction wall indices into AllWalls.</summary>
    public readonly Dictionary<int, List<int>> JunctionWallMap = new();

    /// <summary>Radius of the junction circle at each node (= road half-width).</summary>
    public float JunctionRadius { get; private set; }

    public float RoadSurfaceWorldY { get; private set; }

    /// <summary>
    /// Half the walkable width, reduced by agent radius to prevent agents
    /// from overlapping wall boundaries when placed with lateral offset.
    /// </summary>
    public float WalkHalfWidth { get; private set; }

    public bool HasPathData => AstarEdges.Count > 0 && NodeWorld.Count > 0;

    /// <summary>Clears all network data and notifies the simulator.</summary>
    public void Clear(SocialForceSimulator simulator)
    {
        Segments.Clear();
        Outgoing.Clear();
        AstarEdges.Clear();
        NodeWorld.Clear();
        NodeIds.Clear();
        AllWalls.Clear();
        EdgeWallMap.Clear();
        JunctionWallMap.Clear();
        JunctionRadius = 0f;
        simulator.SetWalls(AllWalls, EdgeWallMap, JunctionWallMap);
    }

    /// <summary>
    /// Rebuilds the entire walk network from graph data.
    /// Steps: build segments → node world positions → A* edge graph → wall boundaries.
    /// </summary>
    public void Rebuild(
        GraphVisualizer visualizer,
        GraphData graph,
        float groundY,
        float agentRadius,
        SocialForceParameters parameters,
        SocialForceSimulator simulator)
    {
        RoadSurfaceWorldY = visualizer.GetRoadSurfaceWorldY(groundY);
        var roadHalfWidth = Mathf.Max(0.05f, visualizer.GetStreetWalkHalfWidth());
        WalkHalfWidth = Mathf.Max(0.05f, roadHalfWidth - agentRadius);
        JunctionRadius = roadHalfWidth;

        visualizer.BuildWalkNetwork(graph, groundY, Segments, Outgoing);
        RebuildNodeWorld(graph);
        RebuildAstarEdges();
        WalkBoundaryBuilder.Build(Segments, roadHalfWidth, NodeWorld, AllWalls, EdgeWallMap, JunctionWallMap);
        simulator.SetWalls(AllWalls, parameters.WallForceDistance + agentRadius);
    }

    /// <summary>
    /// Finds the nearest graph node to a world point (by XZ distance, ignoring Y).
    /// Returns -1 if no nodes exist.
    /// </summary>
    public int NearestNodeId(Vector3 p)
    {
        var best = -1;
        var bd = float.MaxValue;
        foreach (var kv in NodeWorld)
        {
            var q = kv.Value;
            var d = (new Vector2(q.x - p.x, q.z - p.z)).sqrMagnitude;
            if (d < bd)
            {
                bd = d;
                best = kv.Key;
            }
        }

        return best;
    }

    /// <summary>
    /// Returns the segment index matching a specific (fromNode → toNode) pair.
    /// Returns -1 if not found.
    /// </summary>
    public int FindSegmentIndex(int fromNode, int toNode)
    {
        for (var i = 0; i < Segments.Count; i++)
        {
            var s = Segments[i];
            if (s.FromNode == fromNode && s.ToNode == toNode)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Picks the next segment for an agent at a given node.
    /// Priority: 1) next node in command path, 2) random outgoing, 3) random any segment.
    /// </summary>
    public int PickNextSegmentIndex(int atNode, PedestrianAgentRuntime agent)
    {
        if (agent.CommandPath != null &&
            agent.CommandTargetIdx < agent.CommandPath.Count &&
            Outgoing.TryGetValue(atNode, out var outs))
        {
            var target = agent.CommandPath[agent.CommandTargetIdx];
            _segmentCandidates.Clear();
            foreach (var si in outs)
            {
                if (Segments[si].ToNode == target)
                    _segmentCandidates.Add(si);
            }

            if (_segmentCandidates.Count > 0)
                return _segmentCandidates[Random.Range(0, _segmentCandidates.Count)];
        }

        if (Outgoing.TryGetValue(atNode, out var fallback) && fallback.Count > 0)
            return fallback[Random.Range(0, fallback.Count)];

        return Segments.Count == 0 ? 0 : Random.Range(0, Segments.Count);
    }

    void RebuildNodeWorld(GraphData graph)
    {
        NodeWorld.Clear();
        NodeIds.Clear();
        foreach (var kv in graph.Nodes)
        {
            NodeWorld[kv.Key] = kv.Value.Position;
            NodeIds.Add(kv.Key);
        }
    }

    void RebuildAstarEdges()
    {
        AstarEdges.Clear();
        var best = new Dictionary<(int from, int to), float>();
        foreach (var seg in Segments)
        {
            var k = (seg.FromNode, seg.ToNode);
            if (!best.TryGetValue(k, out var len) || seg.Length < len)
                best[k] = seg.Length;
        }

        foreach (var kv in best)
        {
            if (!AstarEdges.TryGetValue(kv.Key.from, out var list))
            {
                list = new List<(int to, float cost)>();
                AstarEdges[kv.Key.from] = list;
            }

            list.Add((kv.Key.to, kv.Value));
        }
    }
}
