using System.Collections.Generic;
using UnityEngine;

public sealed class SocialForceSimulator
{
    readonly SocialForceSpatialHash _hash = new();
    readonly SocialForceWallSpatialHash _wallHash = new();
    readonly List<int> _neighbors = new(64);
    readonly List<int> _wallCandidates = new(64);

    readonly List<Vector2> _forces = new(256);
    readonly List<float> _radialContactForces = new(256);

    IReadOnlyList<SocialForceWallSegment> _walls = System.Array.Empty<SocialForceWallSegment>();

    public void SetWalls(IReadOnlyList<SocialForceWallSegment> walls, float searchRadius)
    {
        _walls = walls ?? System.Array.Empty<SocialForceWallSegment>();
        _wallHash.Rebuild(_walls, Mathf.Max(0.25f, searchRadius));
    }

    // Overload for backward compat with PedestrianWalkNetwork.
    public void SetWalls(
        IReadOnlyList<SocialForceWallSegment> walls,
        Dictionary<(int, int), (int, int)> edgeMap,
        Dictionary<int, List<int>> juncMap)
    {
        _walls = walls ?? System.Array.Empty<SocialForceWallSegment>();
        _wallHash.Rebuild(_walls, Mathf.Max(0.25f, 1.1f));
    }

    public void Step(IReadOnlyList<SocialForceAgentState> agents, SocialForceParameters p, float dt)
    {
        var count = agents.Count;
        if (count == 0 || dt <= 0f) return;

        EnsureBuffers(count);
        for (var i = 0; i < count; i++)
        {
            _forces[i] = DrivingForce(agents[i], p);
            _radialContactForces[i] = 0f;
        }

        _hash.Rebuild(agents, p.SafeNeighborSearchRadius);
        AddPedestrianForces(agents, p);
        AddWallForces(agents, p);
        Integrate(agents, p, dt);
    }

    void EnsureBuffers(int count)
    {
        while (_forces.Count < count) _forces.Add(Vector2.zero);
        while (_radialContactForces.Count < count) _radialContactForces.Add(0f);
    }

    static Vector2 DrivingForce(SocialForceAgentState a, SocialForceParameters p) =>
        a.IsInjuredObstacle ? Vector2.zero
            : Mathf.Max(1f, a.Mass) * (a.DesiredVelocity - a.Velocity) / p.SafeRelaxationTime;

    void AddPedestrianForces(IReadOnlyList<SocialForceAgentState> agents, SocialForceParameters p)
    {
        var cutoffSq = p.SafeNeighborSearchRadius * p.SafeNeighborSearchRadius;

        for (var i = 0; i < agents.Count; i++)
        {
            var a = agents[i];
            a.DebugPedContactCount = 0;
            a.DebugPedMaxBodyForce = 0f;
            _hash.Query(a.Position, p.SafeNeighborSearchRadius, _neighbors);

            foreach (var j in _neighbors)
            {
                if (j <= i) continue;
                var b = agents[j];
                var delta = a.Position - b.Position;
                var distSq = delta.sqrMagnitude;
                var rSum = a.Radius + b.Radius;
                if (distSq > cutoffSq && distSq > rSum * rSum) continue;

                var n = SafeNormal(delta, i, j);
                var dist = Mathf.Sqrt(Mathf.Max(distSq, 1e-8f));
                var t = new Vector2(-n.y, n.x);
                var overlap = Mathf.Clamp(rSum - dist, 0f, rSum);
                var social = p.SocialRepulsionStrength *
                    Mathf.Exp(Mathf.Clamp((rSum - dist) / p.SafeSocialRepulsionRange, -20f, 20f));
                var body = p.BodyForceStiffness * overlap;
                var dv = Vector2.Dot(b.Velocity - a.Velocity, t);
                var friction = p.SlidingFriction * overlap * dv;
                var force = (social + body) * n + friction * t;

                _forces[i] += force;
                _forces[j] -= force;
                if (overlap > 0f)
                {
                    _radialContactForces[i] += Mathf.Abs(body);
                    _radialContactForces[j] += Mathf.Abs(body);
                    a.DebugPedContactCount++;
                    b.DebugPedContactCount++;
                    a.DebugPedMaxBodyForce = Mathf.Max(a.DebugPedMaxBodyForce, body);
                    b.DebugPedMaxBodyForce = Mathf.Max(b.DebugPedMaxBodyForce, body);
                }
            }
        }
    }

    void AddWallForces(IReadOnlyList<SocialForceAgentState> agents, SocialForceParameters p)
    {
        if (_walls.Count == 0) return;
        var influence = Mathf.Max(p.WallForceDistance, p.SafeSocialRepulsionRange * 4f);

        for (var i = 0; i < agents.Count; i++)
        {
            var agent = agents[i];
            agent.DebugWallOverlapRaw = 0f;
            agent.DebugWallOverlapClamped = 0f;
            agent.DebugWallContactCount = 0;

            _wallHash.Query(agent.Position, influence + agent.Radius, _wallCandidates);

            foreach (var c in _wallCandidates)
            {
                if (c < 0 || c >= _walls.Count) continue;
                var wall = _walls[c];

                var closest = wall.ClosestPoint(agent.Position);
                var fromWall = agent.Position - closest;
                var signedDist = Vector2.Dot(fromWall, wall.InwardNormal);

                // Skip walls far away regardless of which side.
                if (Mathf.Abs(signedDist) - agent.Radius > influence) continue;

                var surfaceDist = Mathf.Max(0f, signedDist);
                var normal = signedDist >= -0.05f ? wall.InwardNormal
                    : (fromWall.sqrMagnitude > 1e-6f ? fromWall.normalized : wall.InwardNormal);
                var tangent = wall.Tangent;

                var rawOverlap = agent.Radius - signedDist;
                var overlap = Mathf.Clamp(rawOverlap, 0f, agent.Radius * 0.005f);

                var social = p.SocialRepulsionStrength *
                    Mathf.Exp(Mathf.Clamp((agent.Radius - surfaceDist) / p.SafeSocialRepulsionRange, -20f, 20f));
                var body = p.BodyForceStiffness * overlap;
                var friction = -p.SlidingFriction * overlap * Vector2.Dot(agent.Velocity, tangent);
                var force = (social + body) * normal + friction * tangent;

                _forces[i] += force;
                if (overlap > 0f)
                {
                    _radialContactForces[i] += Mathf.Abs(body);
                    agent.DebugWallOverlapRaw = Mathf.Max(agent.DebugWallOverlapRaw, rawOverlap);
                    agent.DebugWallOverlapClamped = Mathf.Max(agent.DebugWallOverlapClamped, overlap);
                    agent.DebugWallContactCount++;
                }
            }
        }
    }

    void Integrate(IReadOnlyList<SocialForceAgentState> agents, SocialForceParameters p, float dt)
    {
        for (var i = 0; i < agents.Count; i++)
        {
            var a = agents[i];
            var accel = _forces[i] / Mathf.Max(1f, a.Mass);
            var maxA = Mathf.Max(0.1f, p.MaxAcceleration);
            if (accel.magnitude > maxA) accel = accel.normalized * maxA;

            if (!a.IsInjuredObstacle)
            {
                a.Velocity += accel * dt;
                var maxV = Mathf.Max(p.MaxSpeed, a.DesiredSpeed);
                if (a.Velocity.magnitude > maxV) a.Velocity = a.Velocity.normalized * maxV;
                a.Position += a.Velocity * dt;
            }
            else
            {
                a.Velocity = Vector2.zero;
            }

            a.Pressure = _radialContactForces[i] / Mathf.Max(0.01f, 2f * Mathf.PI * a.Radius);

            if (a.Pressure >= p.InjuryPressureThreshold) a.PressureDuration += dt;
            else a.PressureDuration = 0f;

            if (a.PressureDuration >= p.InjuryPressureDuration) a.IsInjuredObstacle = true;
        }
    }

    static Vector2 SafeNormal(Vector2 d, int i, int j)
    {
        if (d.sqrMagnitude > 1e-8f) return d.normalized;
        var angle = (i * 73856093 ^ j * 19349663) * 0.0001f;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }
}
