using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-facing composition root for pedestrian crowd simulation.
/// Domain movement is delegated to SocialForceSimulator.
///
/// Lifecycle:
///   Awake()           → configure view factory
///   OnRoadsBuilt()    → rebuild walk network and wall boundaries from graph
///   AddOne/RemoveOne  → spawn/despawn agents
///   FixedUpdate()     → update desired directions → assign destinations → SFM step → sync transforms
///
/// Each agent picks random start/destination nodes at spawn and navigates via A* pathfinding.
/// When the destination is reached, a new random destination is assigned.
/// A* results are cached per (from, to) node pair to avoid redundant path computations.
/// </summary>
public class PedestrianCrowdSim : MonoBehaviour
{
    [SerializeField] GraphVisualizer visualizer;
    [SerializeField] Transform pedestriansRoot;

    [Header("보행")]
    [SerializeField] float walkSpeed = 1.15f;
    [SerializeField] float cylinderRadiusXZ = 0.3f;
    [SerializeField] float cylinderHeight = 0.52f;
    [SerializeField] Color pedestrianColor = new(0.92f, 0.88f, 0.82f, 1f);
    [SerializeField] Color selectedTint = new(0.45f, 0.95f, 1f, 1f);

    [Header("Social Force Model")]
    [SerializeField] SocialForceParameters socialForce = new();
    [SerializeField] float nodeArrivalRadius = 0.45f;
    [Tooltip("When enabled, injured agents are removed from the scene instead of remaining as obstacles.")]
    [SerializeField] bool removeInjuredAgents;

    [Header("Debug")]
    [SerializeField] bool drawWallGizmos = true;
    [SerializeField] Color wallGizmoColor = new(1f, 0.3f, 0.1f, 0.7f);

    readonly PedestrianAgentStore _store = new();
    readonly PedestrianWalkNetwork _network = new();
    readonly PedestrianViewFactory _views = new();
    readonly PedestrianMotionController _motion = new();
    readonly SocialForceSimulator _sfm = new();

    /// <summary>Caches A* path results keyed by (fromNode, toNode) pair.</summary>
    readonly Dictionary<(int from, int to), System.Collections.Generic.List<int>> _pathCache = new();

    int _nextAgentId = 1;
    int? _selectedAgentId;

    public int LivingCount => _store.Count;

    /// <summary>Y coordinate of the road surface for world-space positioning.</summary>
    public float RoadSurfaceWorldY => _network.RoadSurfaceWorldY;

    void Awake()
    {
        EnsureSocialForceParameters();
        ConfigureViews();
    }

    void OnDestroy()
    {
        _views.Dispose();
    }

    public bool HasAgentId(int agentId) => _store.ContainsId(agentId);

    public SocialForceAgentState GetSfmState(int agentId) =>
        _store.TryGetIndex(agentId, out var idx) ? _store.SfmAgents[idx] : null;

    public PedestrianAgentRuntime GetRuntime(int agentId) =>
        _store.TryGetIndex(agentId, out var idx) ? _store.Agents[idx] : null;

    public void SetSelectedAgentId(int? agentId)
    {
        _selectedAgentId = agentId;
        RefreshAllHighlights();
    }

    public void SetSelectedUnit(PedestrianUnit unit) =>
        SetSelectedAgentId(unit != null ? unit.AgentId : null);

    public bool TryNavigateSelectedTo(Vector3 worldPoint)
    {
        return _selectedAgentId.HasValue &&
               TryOrderMoveToWorldPoint(_selectedAgentId.Value, worldPoint);
    }

    /// <summary>Destroys all agent GameObjects and clears internal state.</summary>
    public void ClearAllAgents()
    {
        ClearAgentViewsAndState();
        _network.Clear(_sfm);
        _pathCache.Clear();
        _selectedAgentId = null;
    }

    /// <summary>
    /// Called by GraphManager after road geometry is confirmed.
    /// Rebuilds the walk network from graph data, creates wall boundaries,
    /// clears path cache, and removes any existing agents.
    /// </summary>
    public void OnRoadsBuilt(GraphData graph, float groundY)
    {
        if (visualizer == null || graph == null)
        {
            ClearAllAgents();
            return;
        }

        EnsureSocialForceParameters();
        _network.Rebuild(visualizer, graph, groundY, cylinderRadiusXZ, socialForce, _sfm);
        _pathCache.Clear();
        ClearAgentViewsAndState();
        _selectedAgentId = null;
    }

    /// <summary>
    /// Spawns a single pedestrian at a random position on a road segment.
    /// Picks two random distinct graph nodes as start (placement near it) and destination.
    /// Finds the A* path between them (with caching) and sets it as the agent's navigation path.
    /// </summary>
    public void AddOne()
    {
        if (_network.Segments.Count == 0)
        {
            Debug.LogWarning("[PedestrianCrowdSim] No roads built. Build the graph first.");
            return;
        }

        var nodeIds = _network.NodeIds;
        if (nodeIds.Count < 2)
        {
            Debug.LogWarning("[PedestrianCrowdSim] Need at least 2 graph nodes.");
            return;
        }

        EnsureSocialForceParameters();
        ConfigureViews();

        // Pick random distinct start and destination nodes.
        var startNode = nodeIds[Random.Range(0, nodeIds.Count)];
        var destNode = startNode;
        while (destNode == startNode)
            destNode = nodeIds[Random.Range(0, nodeIds.Count)];

        // A* pathfinding with cache lookup.
        if (!TryGetPath(startNode, destNode, out var path) || path.Count < 2)
        {
            Debug.LogWarning($"[PedestrianCrowdSim] No path from {startNode} to {destNode}.");
            return;
        }

        // Find the segment from startNode (path[0]) to the first waypoint (path[1]).
        var segmentIndex = _network.FindSegmentIndex(path[0], path[1]);
        if (segmentIndex < 0)
        {
            Debug.LogWarning("[PedestrianCrowdSim] First segment of path not found.");
            return;
        }

        var seg = _network.Segments[segmentIndex];

        // Place agent along the first segment (t=0.05–0.95, not at endpoints).
        // Add random lateral offset to distribute agents across the road width.
        var t = Random.Range(0.05f, 0.95f);
        var lateral = Random.Range(-_network.WalkHalfWidth * 0.35f, _network.WalkHalfWidth * 0.35f);
        var startPos = PositionOnSegment(seg, t, lateral);
        var id = _nextAgentId++;

        var agent = _views.Create(id, cylinderRadiusXZ, cylinderHeight, startPos, seg.B - seg.A, this);
        agent.SegmentIndex = segmentIndex;
        agent.CommandPath = path; // Owned by cache — AdvancePathAtNode only reads, never modifies.
        agent.CommandTargetIdx = 1; // Heading to path[1] (path[0] is the start node).
        agent.TargetNodeId = seg.ToNode;

        var sfm = new SocialForceAgentState
        {
            Id = id,
            Position = ToXZ(startPos),
            Velocity = Vector2.zero,
            DesiredSpeed = walkSpeed,
            DesiredDirection = DirectionTo(ToXZ(seg.B) - ToXZ(startPos)),
            Radius = cylinderRadiusXZ,
            Mass = socialForce.Mass,
            CurrentEdgeMinNode = Mathf.Min(seg.FromNode, seg.ToNode),
            CurrentEdgeMaxNode = Mathf.Max(seg.FromNode, seg.ToNode)
        };

        _store.Add(agent, sfm);
        _views.ApplyHighlight(agent, id == _selectedAgentId);
    }

    /// <summary>Removes a random pedestrian. If it was the selected agent, deselects.</summary>
    public void RemoveOne()
    {
        if (_store.Count == 0) return;
        var i = Random.Range(0, _store.Count);
        if (_selectedAgentId == _store.Agents[i].Id)
            _selectedAgentId = null;
        DestroyAgentAt(i);
        RefreshAllHighlights();
    }

    /// <summary>
    /// Orders a specific agent to pathfind to a world point using A* on the road graph.
    /// Overrides any existing random destination with a user command.
    /// </summary>
    public bool TryOrderMoveToWorldPoint(int agentId, Vector3 worldPoint)
    {
        if (!_network.HasPathData || !_store.TryGetIndex(agentId, out var idx))
            return false;

        var start = _network.NearestNodeId(_store.Agents[idx].Transform.position);
        var goal = _network.NearestNodeId(worldPoint);
        if (start < 0 || goal < 0) return false;

        // User commands use direct A* (no cache) since they are rare and cache isn't needed.
        if (!RoadPathfinding.TryFindPath(start, goal, _network.AstarEdges, _network.NodeWorld, out var path) || path.Count < 2)
        {
            Debug.LogWarning("[PedestrianCrowdSim] No path found to target.");
            return false;
        }

        var segmentIndex = _network.FindSegmentIndex(path[0], path[1]);
        if (segmentIndex < 0)
        {
            Debug.LogWarning("[PedestrianCrowdSim] First edge of path not found.");
            return false;
        }

        var agent = _store.Agents[idx];
        agent.CommandPath = path;
        agent.CommandTargetIdx = 1;
        agent.SegmentIndex = segmentIndex;
        agent.TargetNodeId = _network.Segments[segmentIndex].ToNode;

        var sfm = _store.SfmAgents[idx];
        var seg = _network.Segments[segmentIndex];
        sfm.DesiredDirection = DirectionTo(ToXZ(seg.B) - sfm.Position);
        sfm.DesiredSpeed = walkSpeed;
        sfm.CurrentEdgeMinNode = Mathf.Min(seg.FromNode, seg.ToNode);
        sfm.CurrentEdgeMaxNode = Mathf.Max(seg.FromNode, seg.ToNode);
        return true;
    }

    /// <summary>
    /// Main simulation tick (FixedUpdate).
    /// 1. Update desired directions from path graph.
    /// 2. Assign new random destinations to agents that completed their previous paths.
    /// 3. Step the social force simulator (force computation + integration).
    /// 4. Sync SFM state back to Unity transforms.
    /// 5. Remove injured agents (if policy enabled).
    /// </summary>
    void FixedUpdate()
    {
        if (_network.Segments.Count == 0 || _store.Count == 0) return;

        _motion.UpdateDesiredDirections(_store, _network, nodeArrivalRadius, walkSpeed);
        AssignNewDestinations();
        _sfm.Step(_store.SfmAgents, socialForce, Time.fixedDeltaTime);
        SyncViewsFromDomain();
        ApplyInjuryPolicy();
    }

    /// <summary>
    /// After motion update, scans for agents whose CommandPath was just exhausted
    /// (destination reached) and assigns a new random destination via A*.
    ///
    /// An exhausted path means CommandPath is null but the agent is healthy (not injured).
    /// The MotionController already set a random fallback segment — we override it
    /// with the first segment of the new A* path.
    /// </summary>
    void AssignNewDestinations()
    {
        var nodeIds = _network.NodeIds;
        if (nodeIds.Count < 2) return;

        for (var i = 0; i < _store.Count; i++)
        {
            var agent = _store.Agents[i];
            if (agent.CommandPath != null) continue;
            if (_store.SfmAgents[i].IsInjuredObstacle) continue;

            var current = _network.NearestNodeId(agent.Transform.position);
            if (current < 0) continue;

            // Pick a random destination different from the current node.
            var dest = current;
            while (dest == current)
                dest = nodeIds[Random.Range(0, nodeIds.Count)];

            if (!TryGetPath(current, dest, out var path) || path.Count < 2) continue;

            var segmentIndex = _network.FindSegmentIndex(path[0], path[1]);
            if (segmentIndex < 0) continue;

            agent.CommandPath = path;
            agent.CommandTargetIdx = 1;
            agent.SegmentIndex = segmentIndex;
            agent.TargetNodeId = _network.Segments[segmentIndex].ToNode;

            var sfm = _store.SfmAgents[i];
            var seg = _network.Segments[segmentIndex];
            sfm.DesiredDirection = DirectionTo(ToXZ(seg.B) - sfm.Position);
            sfm.DesiredSpeed = walkSpeed;
            sfm.CurrentEdgeMinNode = Mathf.Min(seg.FromNode, seg.ToNode);
            sfm.CurrentEdgeMaxNode = Mathf.Max(seg.FromNode, seg.ToNode);
        }
    }

    /// <summary>
    /// Returns a cached or newly-computed A* path between two nodes.
    /// Cache key is the (from, to) pair. Since paths are read-only
    /// (AdvancePathAtNode only reads indices, never mutates the list),
    /// multiple agents can safely share the same cached list reference.
    /// </summary>
    bool TryGetPath(int from, int to, out System.Collections.Generic.List<int> path)
    {
        var key = (from, to);
        if (_pathCache.TryGetValue(key, out path))
            return true;

        if (RoadPathfinding.TryFindPath(from, to, _network.AstarEdges, _network.NodeWorld, out path))
        {
            _pathCache[key] = path;
            return true;
        }

        return false;
    }

    void ConfigureViews()
    {
        _views.Configure(transform, pedestriansRoot, pedestrianColor, selectedTint);
        pedestriansRoot = _views.Root;
    }

    void EnsureSocialForceParameters()
    {
        socialForce ??= new SocialForceParameters();
    }

    void ClearAgentViewsAndState()
    {
        for (var i = 0; i < _store.Count; i++)
            _views.DestroyAgent(_store.Agents[i]);
        _store.Clear();
    }

    void DestroyAgentAt(int index)
    {
        _views.DestroyAgent(_store.Agents[index]);
        _store.RemoveAt(index);
    }

    /// <summary>Copies SFM positions and velocities back to Unity transforms.</summary>
    void SyncViewsFromDomain()
    {
        for (var i = 0; i < _store.Count; i++)
        {
            var sfm = _store.SfmAgents[i];
            var agent = _store.Agents[i];

            // Convert XZ position back to 3D world space (Y = road surface height).
            agent.Transform.position = ToWorld(sfm.Position);

            // Only rotate when moving fast enough to avoid jitter.
            if (sfm.Velocity.sqrMagnitude > 0.01f)
                PedestrianViewFactory.FaceAlong(agent.Transform, new Vector3(sfm.Velocity.x, 0f, sfm.Velocity.y));
        }
    }

    /// <summary>Removes injured agents when removeInjuredAgents is enabled.</summary>
    void ApplyInjuryPolicy()
    {
        if (!removeInjuredAgents)
            return;

        for (var i = _store.Count - 1; i >= 0; i--)
        {
            if (!_store.SfmAgents[i].IsInjuredObstacle) continue;
            if (_selectedAgentId == _store.Agents[i].Id)
                _selectedAgentId = null;
            DestroyAgentAt(i);
        }

        if (_selectedAgentId.HasValue)
            RefreshAllHighlights();
    }

    void RefreshAllHighlights()
    {
        for (var i = 0; i < _store.Count; i++)
            _views.ApplyHighlight(_store.Agents[i], _store.Agents[i].Id == _selectedAgentId);
    }

    /// <summary>Converts an XZ Vector2 to a 3D world position (Y = road surface height).</summary>
    Vector3 ToWorld(Vector2 p) => new(p.x, _network.RoadSurfaceWorldY, p.y);

    /// <summary>Extracts XZ components from a 3D world position.</summary>
    static Vector2 ToXZ(Vector3 p) => new(p.x, p.z);

    static Vector2 DirectionTo(Vector2 v) => v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;

    /// <summary>
    /// Computes a position along a road segment at parameter t (0=A, 1=B) with a lateral offset.
    /// Lateral offset is perpendicular to the segment direction.
    /// </summary>
    static Vector3 PositionOnSegment(WalkSegment seg, float t, float lateral)
    {
        var fwd = seg.B - seg.A;
        fwd.Normalize();
        var right = Vector3.Cross(Vector3.up, fwd).normalized;
        return Vector3.Lerp(seg.A, seg.B, Mathf.Clamp01(t)) + right * lateral;
    }

    void OnDrawGizmos()
    {
        if (!drawWallGizmos) return;

        var y = _network.RoadSurfaceWorldY;
        if (_network.AllWalls.Count > 0)
            DrawWalls(y);
        if (_network.Segments.Count > 0)
            DrawRoadSurfaces(y);
        if (_store.Count > 0)
            DrawAgents(y);
    }

    void DrawWalls(float y)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = wallGizmoColor;
        foreach (var wall in _network.AllWalls)
        {
            var a = new Vector3(wall.A.x, y, wall.A.y);
            var b = new Vector3(wall.B.x, y, wall.B.y);
            var mid = (a + b) * 0.5f;
            UnityEditor.Handles.DrawLine(a, b, 3f);
            var normalEnd = mid + new Vector3(wall.InwardNormal.x, 0f, wall.InwardNormal.y) * 0.4f;
            UnityEditor.Handles.DrawLine(mid, normalEnd, 2f);
        }

        // Junction circles.
        if (_network.JunctionRadius > 0f)
        {
            UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.25f);
            foreach (var kv in _network.NodeWorld)
            {
                var center = new Vector3(kv.Value.x, y, kv.Value.z);
                DrawHandleCircle(center, _network.JunctionRadius, 24, 2f);
            }
        }
#else
        Gizmos.color = wallGizmoColor;
        foreach (var wall in _network.AllWalls)
        {
            var a = new Vector3(wall.A.x, y, wall.A.y);
            var b = new Vector3(wall.B.x, y, wall.B.y);
            Gizmos.DrawLine(a, b);
        }
#endif
    }

#if UNITY_EDITOR
    static void DrawHandleCircle(Vector3 center, float radius, int segments, float thickness)
    {
        var step = Mathf.PI * 2f / segments;
        var prev = center + new Vector3(Mathf.Cos(0f) * radius, 0f, Mathf.Sin(0f) * radius);
        for (var i = 1; i <= segments; i++)
        {
            var angle = step * i;
            var next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            UnityEditor.Handles.DrawLine(prev, next, thickness);
            prev = next;
        }
    }
#endif

    void DrawRoadSurfaces(float y)
    {
        // Draw road centerlines and walkable area (semi-transparent).
        var seen = new System.Collections.Generic.HashSet<(int, int)>();
        foreach (var seg in _network.Segments)
        {
            var key = seg.FromNode < seg.ToNode ? (seg.FromNode, seg.ToNode) : (seg.ToNode, seg.FromNode);
            if (!seen.Add(key)) continue;

            var a = new Vector3(seg.A.x, y, seg.A.z);
            var b = new Vector3(seg.B.x, y, seg.B.z);
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) continue;

            var fwd = d.normalized;
            var left = new Vector3(-fwd.z, 0f, fwd.x); // XZ left = (-fwd.y, fwd.x) → in 3D: (-fwd.z, 0, fwd.x)
            var halfWidth = _network.WalkHalfWidth + cylinderRadiusXZ; // roadHalfWidth

            // Centerline.
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.4f);
            Gizmos.DrawLine(a, b);

            // Walkable edge lines.
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.15f);
            Gizmos.DrawLine(a + left * halfWidth, b + left * halfWidth);
            Gizmos.DrawLine(a - left * halfWidth, b - left * halfWidth);
        }
    }

    void DrawAgents(float y)
    {
        for (var i = 0; i < _store.Count; i++)
        {
            var sfm = _store.SfmAgents[i];
            var pos = new Vector3(sfm.Position.x, y, sfm.Position.y);
            var r = sfm.Radius;

            Gizmos.color = sfm.IsInjuredObstacle ? Color.red : Color.green;
            Gizmos.DrawWireSphere(pos, r);

            // Desired direction arrow.
            if (sfm.DesiredDirection.sqrMagnitude > 1e-6f)
            {
                var dir = new Vector3(sfm.DesiredDirection.x, 0f, sfm.DesiredDirection.y);
                Gizmos.DrawRay(pos, dir * (r + 0.2f));
            }
        }
    }
}
