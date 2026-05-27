using UnityEngine;

/// <summary>
/// Per-agent state for the social force model (Helbing et al., 2000).
/// All positions and velocities are in world XZ space (Y is handled by the Unity adapter).
/// </summary>
public class SocialForceAgentState
{
    public int Id;

    /// <summary>World XZ position in meters.</summary>
    public Vector2 Position;

    /// <summary>Current XZ velocity in m/s.</summary>
    public Vector2 Velocity;

    /// <summary>Unit vector toward the next path/waypoint target.</summary>
    public Vector2 DesiredDirection = Vector2.right;

    /// <summary>Desired scalar speed v0 in m/s.</summary>
    public float DesiredSpeed;

    /// <summary>Agent radius in meters (paper range: 0.25–0.35 m, diameter 0.5–0.7 m).</summary>
    public float Radius = 0.3f;

    /// <summary>Mass in kg (default 80).</summary>
    public float Mass = 80f;

    /// <summary>When true, agent produces zero driving force and remains as a static obstacle.</summary>
    public bool IsInjuredObstacle;

    /// <summary>Contact pressure in N/m: sum of radial body-contact force magnitudes divided by circumference.</summary>
    public float Pressure;

    /// <summary>Consecutive time the agent has been above the injury pressure threshold.</summary>
    public float PressureDuration;

    /// <summary>Debug: max raw wall overlap before clamping (m).</summary>
    public float DebugWallOverlapRaw;

    /// <summary>Debug: max clamped wall overlap after capping to Radius (m).</summary>
    public float DebugWallOverlapClamped;

    /// <summary>Debug: number of walls in overlapping contact this step.</summary>
    public int DebugWallContactCount;

    /// <summary>Debug: number of pedestrian-pedestrian contacts this step.</summary>
    public int DebugPedContactCount;

    /// <summary>Debug: max pedestrian-pedestrian body force magnitude this step.</summary>
    public float DebugPedMaxBodyForce;

    /// <summary>
    /// Edge key of the road segment the agent is currently traversing.
    /// Used to filter wall forces: only walls belonging to this edge are processed.
    /// </summary>
    public int CurrentEdgeMinNode;
    public int CurrentEdgeMaxNode;

    /// <summary>
    /// Node ID of the junction the agent is currently inside, or -1 if on an edge.
    /// When >= 0, junction walls are used instead of edge walls.
    /// </summary>
    public int CurrentJunctionNodeId = -1;

    /// <summary>Desired velocity vector: DesiredDirection * DesiredSpeed.</summary>
    public Vector2 DesiredVelocity => DesiredDirection.sqrMagnitude > 1e-6f
        ? DesiredDirection.normalized * DesiredSpeed
        : Vector2.zero;
}
