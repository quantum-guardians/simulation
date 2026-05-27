using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PedestrianUnit : MonoBehaviour
{
    public int AgentId { get; private set; }
    public PedestrianCrowdSim Crowd { get; private set; }

    [Header("Debug (read-only)")]
    [SerializeField] Vector3 _desiredDirection;
    [SerializeField] float _desiredSpeed;
    [SerializeField] Vector3 _velocity;
    [SerializeField] Vector3 _position;
    [SerializeField] int _targetNodeId;
    [SerializeField] int _commandTargetIdx;
    [SerializeField] string _commandPath;
    [SerializeField] bool _isInjured;
    [SerializeField] float _pressure;
    [SerializeField] float _wallOverlapRaw;
    [SerializeField] float _wallOverlapClamped;
    [SerializeField] int _wallContactCount;

    public void Initialize(PedestrianCrowdSim crowd, int agentId)
    {
        Crowd = crowd;
        AgentId = agentId;
    }

    void Update()
    {
        if (Crowd == null) return;
        RefreshDebugInfo();
    }

    void RefreshDebugInfo()
    {
        // Access internal store via reflection-like approach — we read from the SFM state.
        var sfm = GetSfmState();
        var runtime = GetRuntime();
        if (sfm == null) return;

        _desiredDirection = new Vector3(sfm.DesiredDirection.x, 0f, sfm.DesiredDirection.y);
        _desiredSpeed = sfm.DesiredSpeed;
        _velocity = new Vector3(sfm.Velocity.x, 0f, sfm.Velocity.y);
        _position = new Vector3(sfm.Position.x, Crowd.RoadSurfaceWorldY, sfm.Position.y);
        _isInjured = sfm.IsInjuredObstacle;
        _pressure = sfm.Pressure;
        _wallOverlapRaw = sfm.DebugWallOverlapRaw;
        _wallOverlapClamped = sfm.DebugWallOverlapClamped;
        _wallContactCount = sfm.DebugWallContactCount;

        if (runtime != null)
        {
            _targetNodeId = runtime.TargetNodeId;
            _commandTargetIdx = runtime.CommandTargetIdx;
            _commandPath = runtime.CommandPath != null
                ? string.Join(" → ", runtime.CommandPath)
                : "(none)";
        }
    }

    SocialForceAgentState GetSfmState()
    {
        return Crowd.GetSfmState(AgentId);
    }

    PedestrianAgentRuntime GetRuntime()
    {
        return Crowd.GetRuntime(AgentId);
    }
}
