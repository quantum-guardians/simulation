using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity-side runtime data for a single pedestrian agent.
/// Stores transform/renderer references and path-following state.
/// Paired 1:1 with a SocialForceAgentState in PedestrianAgentStore.
/// </summary>
public sealed class PedestrianAgentRuntime
{
    public int Id;
    public Transform Transform;
    public Renderer Renderer;

    /// <summary>Current road segment index the agent is traversing.</summary>
    public int SegmentIndex;

    /// <summary>User-commanded path (node ID list). Null when roaming freely.</summary>
    public List<int> CommandPath;

    /// <summary>Current index within CommandPath (which node we're heading toward).</summary>
    public int CommandTargetIdx;

    /// <summary>The To-node of the current segment (used for path advancement checks).</summary>
    public int TargetNodeId;
}
