using UnityEngine;

/// <summary>
/// Position-based path following. Each tick:
///  1. Find nearest node in the command path to the agent.
///  2. Target the NEXT unvisited node toward the destination.
///  3. Drive toward that node's position; auto-advance on arrival.
///
/// No segment-index tracking — position alone determines where to go next.
/// Bounced back? Nearest node shifts backward → target auto-corrects.
/// </summary>
public sealed class PedestrianMotionController
{
    public void UpdateDesiredDirections(
        PedestrianAgentStore store,
        PedestrianWalkNetwork network,
        float nodeArrivalRadius,
        float desiredSpeed)
    {
        for (var i = 0; i < store.Count; i++)
        {
            var agent = store.Agents[i];
            var sfm = store.SfmAgents[i];

            if (sfm.IsInjuredObstacle)
            {
                sfm.DesiredSpeed = 0f;
                continue;
            }

            if (agent.CommandPath != null && agent.CommandPath.Count >= 2)
                FollowPath(agent, sfm, network, nodeArrivalRadius, desiredSpeed);
            else
                Wander(agent, sfm, network, nodeArrivalRadius, desiredSpeed);
        }
    }

    void FollowPath(PedestrianAgentRuntime agent, SocialForceAgentState sfm,
        PedestrianWalkNetwork network, float arrivalR, float baseSpeed)
    {
        var pos = sfm.Position;
        var path = agent.CommandPath;

        // Find the path segment closest to the agent (not just nearest node).
        // This ensures the agent targets the next node along the path,
        // not skipping ahead through walls at junctions.
        var bestSeg = 0;
        var bestDistSq = float.MaxValue;
        for (var s = 0; s < path.Count - 1; s++)
        {
            var a = ToXZ(network.NodeWorld[path[s]]);
            var b = ToXZ(network.NodeWorld[path[s + 1]]);
            var ab = b - a;
            var lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-6f) continue;
            var t = Mathf.Clamp01(Vector2.Dot(pos - a, ab) / lenSq);
            var closest = a + ab * t;
            var dSq = (pos - closest).sqrMagnitude;
            if (dSq < bestDistSq) { bestDistSq = dSq; bestSeg = s; }
        }

        // Target = end node of the closest segment.
        var targetIdx = bestSeg + 1;
        var targetNode = path[targetIdx];
        var targetPos = ToXZ(network.NodeWorld[targetNode]);

        // Auto-advance if within arrival radius.
        while (targetIdx < path.Count - 1 &&
               (pos - targetPos).sqrMagnitude <= arrivalR * arrivalR)
        {
            targetIdx++;
            targetNode = path[targetIdx];
            targetPos = ToXZ(network.NodeWorld[targetNode]);
        }

        // Destination reached?
        if (targetIdx >= path.Count - 1 &&
            (pos - targetPos).sqrMagnitude <= arrivalR * arrivalR)
        {
            agent.CommandPath = null;
            agent.CommandTargetIdx = 0;
            Wander(agent, sfm, network, arrivalR, baseSpeed);
            return;
        }

        // Drive toward target.
        sfm.DesiredDirection = DirectionTo(targetPos - pos);

        // Speed boost near junctions.
        var distToFrom = (pos - ToXZ(network.NodeWorld[path[bestSeg]])).magnitude;
        var distToTo = (pos - targetPos).magnitude;
        var nearJunction = distToFrom <= network.JunctionRadius || distToTo <= network.JunctionRadius;
        sfm.DesiredSpeed = nearJunction ? baseSpeed * 2.5f : baseSpeed;

        agent.CommandTargetIdx = targetIdx;
        agent.TargetNodeId = targetNode;

        SetSegmentAndEdgeKey(agent, sfm, network, path[bestSeg], targetNode);

        sfm.CurrentJunctionNodeId = -1;
    }

    void Wander(PedestrianAgentRuntime agent, SocialForceAgentState sfm,
        PedestrianWalkNetwork network, float arrivalR, float baseSpeed)
    {
        if (agent.SegmentIndex < 0 || agent.SegmentIndex >= network.Segments.Count)
            agent.SegmentIndex = network.PickNextSegmentIndex(
                network.NearestNodeId(ToWorld(sfm.Position, network)), agent);

        var seg = network.Segments[agent.SegmentIndex];
        var target = ToXZ(seg.B);
        var toTarget = target - sfm.Position;

        if (toTarget.sqrMagnitude <= arrivalR * arrivalR)
        {
            agent.SegmentIndex = network.PickNextSegmentIndex(seg.ToNode, agent);
            seg = network.Segments[agent.SegmentIndex];
            agent.TargetNodeId = seg.ToNode;
            target = ToXZ(seg.B);
            toTarget = target - sfm.Position;
        }

        sfm.DesiredDirection = DirectionTo(toTarget);
        sfm.DesiredSpeed = baseSpeed;
        sfm.CurrentEdgeMinNode = Mathf.Min(seg.FromNode, seg.ToNode);
        sfm.CurrentEdgeMaxNode = Mathf.Max(seg.FromNode, seg.ToNode);
        sfm.CurrentJunctionNodeId = -1;
    }

    static void SetSegmentAndEdgeKey(PedestrianAgentRuntime agent, SocialForceAgentState sfm,
        PedestrianWalkNetwork network, int fromNode, int toNode)
    {
        var si = network.FindSegmentIndex(fromNode, toNode);
        if (si < 0) si = network.FindSegmentIndex(toNode, fromNode);
        if (si >= 0)
        {
            agent.SegmentIndex = si;
            var seg = network.Segments[si];
            sfm.CurrentEdgeMinNode = Mathf.Min(seg.FromNode, seg.ToNode);
            sfm.CurrentEdgeMaxNode = Mathf.Max(seg.FromNode, seg.ToNode);
        }
    }

    static Vector3 ToWorld(Vector2 p, PedestrianWalkNetwork n) =>
        new(p.x, n.RoadSurfaceWorldY, p.y);

    static Vector2 ToXZ(Vector3 p) => new(p.x, p.z);

    static Vector2 DirectionTo(Vector2 v) =>
        v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
}
