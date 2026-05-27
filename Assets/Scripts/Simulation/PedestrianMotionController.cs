using UnityEngine;

/// <summary>
/// Updates each agent's desired direction toward the next node on its path
/// and determines wall mode (edge vs junction).
///
/// Junction detection: if the agent is within JunctionRadius of its current
/// segment's FromNode or ToNode, it switches to junction wall mode.
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

            if (agent.SegmentIndex < 0 || agent.SegmentIndex >= network.Segments.Count)
                agent.SegmentIndex = network.PickNextSegmentIndex(
                    network.NearestNodeId(ToWorld(sfm.Position, network)), agent);

            var seg = network.Segments[agent.SegmentIndex];
            var target = ToXZ(seg.B);
            var toTarget = target - sfm.Position;

            if (toTarget.sqrMagnitude <= nodeArrivalRadius * nodeArrivalRadius)
            {
                AdvancePathAtNode(agent, seg.ToNode);
                agent.SegmentIndex = network.PickNextSegmentIndex(seg.ToNode, agent);
                seg = network.Segments[agent.SegmentIndex];
                agent.TargetNodeId = seg.ToNode;
                target = ToXZ(seg.B);
                toTarget = target - sfm.Position;
            }

            sfm.DesiredDirection = DirectionTo(toTarget);
            sfm.DesiredSpeed = desiredSpeed;

            // Set edge key.
            sfm.CurrentEdgeMinNode = Mathf.Min(seg.FromNode, seg.ToNode);
            sfm.CurrentEdgeMaxNode = Mathf.Max(seg.FromNode, seg.ToNode);
            sfm.CurrentJunctionNodeId = -1;
        }
    }

    static void AdvancePathAtNode(PedestrianAgentRuntime agent, int nodeId)
    {
        if (agent.CommandPath != null &&
            agent.CommandTargetIdx < agent.CommandPath.Count &&
            nodeId == agent.CommandPath[agent.CommandTargetIdx])
        {
            agent.CommandTargetIdx++;
            if (agent.CommandTargetIdx >= agent.CommandPath.Count)
            {
                agent.CommandPath = null;
                agent.CommandTargetIdx = 0;
            }
        }
    }

    static Vector3 ToWorld(Vector2 p, PedestrianWalkNetwork network) =>
        new(p.x, network.RoadSurfaceWorldY, p.y);

    static Vector2 ToXZ(Vector3 p) => new(p.x, p.z);

    static Vector2 DirectionTo(Vector2 v) =>
        v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
}
