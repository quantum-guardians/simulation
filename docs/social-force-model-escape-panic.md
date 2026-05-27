# Social Force Model for Escape Panic

Implementation notes from Dirk Helbing, Illes Farkas, and Tamas Vicsek,
"Simulating Dynamical Features of Escape Panic", arXiv:cond-mat/0009448,
Nature 407, 487-490 (2000).

Paper links:
- arXiv abstract: https://arxiv.org/abs/cond-mat/0009448
- ar5iv HTML: https://ar5iv.labs.arxiv.org/html/cond-mat/0009448

## Purpose

The paper models pedestrian escape panic as a continuous particle simulation.
Each pedestrian is a self-driven disk/cylinder that tries to move toward a
desired direction while being repelled by people and walls. Under dense contact,
extra body-compression and sliding-friction terms dominate. Those physical
contact terms are the mechanism behind clogging, arch formation at doors,
pressure buildup, and the faster-is-slower effect.

For this Unity project, this model should replace the current segment-only
motion in `PedestrianCrowdSim` with force integration in world XZ space.
Road/path graph logic can still provide desired directions, but actual motion
should come from acceleration, velocity, and collision/contact forces.

## State Per Agent

Keep these fields per pedestrian:

- `position`: `Vector2` or XZ projection of world position.
- `velocity`: current XZ velocity in m/s.
- `mass`: default 80 kg.
- `radius`: sample uniformly in `[0.25, 0.35]` m, matching paper diameter
  range `[0.5, 0.7]` m.
- `desiredSpeed`: `v0`, m/s.
- `desiredDirection`: unit vector `e0`.
- `relaxationTime`: `tau`, default 0.5 s.
- `panic`: `p`, optional `[0, 1]` for herding/impatience behavior.
- `isObstacleOrDead`: disables self-propulsion but leaves the body as an
  obstacle if implementing injury/fallen pedestrians.

Recommended defaults from the paper:

| Parameter | Symbol | Value |
| --- | --- | --- |
| Mass | `m` | `80 kg` |
| Relaxation time | `tau` | `0.5 s` |
| Social repulsion strength | `A` | `2000 N` |
| Social repulsion range | `B` | `0.08 m` |
| Body force stiffness | `k` | `1.2e5 kg/s^2` |
| Sliding friction | `kappa` | `2.4e5 kg/(m*s)` |
| Relaxed desired speed | `v0` | about `0.6 m/s` |
| Normal desired speed | `v0` | about `1.0 m/s` |
| Nervous desired speed | `v0` | up to `1.5 m/s` |
| Panic/high desired speed | `v0` | above `1.5 m/s`, can exceed `5 m/s` |

## Main Dynamics

For pedestrian `i`, integrate:

```text
m_i * dv_i/dt =
    m_i * (v_i^0 * e_i^0 - v_i) / tau_i
    + sum_j f_ij
    + sum_W f_iW

dr_i/dt = v_i
```

The first term is the driving force. It accelerates the pedestrian toward
the desired velocity `v0 * e0` over `tau` seconds.

Implementation:

```csharp
Vector2 desiredVelocity = desiredSpeed * desiredDirection;
Vector2 drivingForce = mass * (desiredVelocity - velocity) / relaxationTime;
```

Then add pairwise pedestrian forces and wall forces, divide total force by
mass, integrate velocity and position.

Prefer semi-implicit Euler for stability:

```csharp
velocity += acceleration * dt;
position += velocity * dt;
```

Use a fixed timestep. Clamp maximum speed and acceleration defensively so
bad overlaps do not explode the simulation.

## Pedestrian-Pedestrian Force

Definitions for agent pair `i, j`:

```text
delta = position_i - position_j
d_ij = |delta|
n_ij = delta / d_ij              // normal from j to i
t_ij = (-n_ij.y, n_ij.x)         // tangent
r_ij = radius_i + radius_j
overlap = r_ij - d_ij
g(x) = max(x, 0)
deltaVt = dot(velocity_j - velocity_i, t_ij)
```

Force on `i` from `j`:

```text
f_ij =
    (A * exp((r_ij - d_ij) / B) + k * g(r_ij - d_ij)) * n_ij
    + kappa * g(r_ij - d_ij) * deltaVt * t_ij
```

Mechanism:

- The exponential term is the non-contact social/psychological repulsion.
- The `k * overlap` term is a body-compression force that prevents overlap.
- The `kappa * overlap * deltaVt` term is sliding friction. It resists
  relative tangential motion during physical contact.

The friction term is central for panic behavior. With high desired speeds,
people push into a bottleneck, contact friction rises, arch-like blockings
form, and outflow becomes intermittent.

Implementation details:

- If `d_ij` is almost zero, use a small epsilon and a random or cached normal.
- Social repulsion can be computed for neighbors within a cutoff, for example
  `max(radiusSum + 3 * B, 1.0 m)`. Contact forces require all overlapping
  neighbors.
- Use a spatial hash/grid; O(N^2) is only acceptable for small debug scenes.
- Accumulate equal and opposite forces if doing pair iteration once, or compute
  force-on-i directly for simpler code.

## Wall Force

For a wall or obstacle boundary `W`, find:

```text
d_iW = shortest distance from agent center to wall surface
n_iW = inward normal pointing from wall toward agent
t_iW = wall tangent
overlap = radius_i - d_iW
```

Force on agent `i` from wall `W`:

```text
f_iW =
    (A * exp((radius_i - d_iW) / B) + k * g(radius_i - d_iW)) * n_iW
    - kappa * g(radius_i - d_iW) * dot(velocity_i, t_iW) * t_iW
```

Use this for road borders, building walls, exit frames, and static obstacles.
For this project's graph-generated streets, each road segment can contribute
two line-segment boundaries. Intersections/plazas need either merged boundary
geometry or a simpler walkable-area constraint to avoid artificial wall forces
inside junctions.

## Desired Direction

In normal navigation, `desiredDirection` should point along the path:

1. Pick current path target, usually the next graph node or waypoint.
2. `desiredDirection = normalize(target - position)`.
3. Advance target when the agent is within a small arrival radius.

The social force model does not replace pathfinding. It replaces the local
movement/collision response between waypoints.

## Herding / Panic Direction

The paper models mass behavior by blending individual direction with the
average direction of nearby pedestrians:

```text
e_i^0 = normalize((1 - p_i) * e_i + p_i * average(e_j^0))
```

Where:

- `e_i` is the individual's own chosen/search direction.
- `average(e_j^0)` is the average desired direction of neighbors inside radius
  `R`.
- `p_i` is the panic/herding parameter.

Behavior:

- Low `p`: individualistic search. People may discover exits, but many wander.
- High `p`: strong herding. The crowd may overuse one exit and jam.
- Intermediate `p`: best escape performance in the paper's smoky-room scenario.

For implementation, start with `p = 0` for normal commanded navigation. Add
herding later as a mode for panic/smoke scenarios.

## Impatience / Faster-Is-Slower

The paper also describes impatience by increasing desired speed when actual
progress is low:

```text
v_i^0(t) = (1 - p_i(t)) * v_i^0(0) + p_i(t) * v_i_max
p_i(t) = 1 - averageForwardSpeed_i(t) / v_i^0
```

Here `averageForwardSpeed_i` is speed projected onto the desired direction and
averaged over time.

Implementation sketch:

```csharp
float forwardSpeed = Vector2.Dot(velocity, desiredDirection);
agent.avgForwardSpeed = Mathf.Lerp(agent.avgForwardSpeed, forwardSpeed, alpha);
float impatience = 1f - agent.avgForwardSpeed / Mathf.Max(baseDesiredSpeed, 0.01f);
impatience = Mathf.Clamp01(impatience);
agent.desiredSpeed = Mathf.Lerp(baseDesiredSpeed, maxDesiredSpeed, impatience);
```

Use this only after the base force model works. It can destabilize scenes if
agents are allowed to push to high speed before contact handling is tuned.

## Injury / Pressure Rule

The paper treats injuries as non-moving obstacles when pressure is too high.
Its figure caption uses this criterion:

```text
sum(abs(radial contact forces)) / circumference > 1600 N/m
```

Implementation approximation:

1. Track only compressive normal/contact forces, not social repulsion.
2. For each agent, accumulate magnitudes of radial body forces from neighbors
   and walls.
3. Compute `pressure = radialForceSum / (2 * PI * radius)`.
4. If pressure exceeds `1600 N/m` for some duration, mark the agent injured.
5. Injured agents stop driving force but remain as obstacles.

This is more faithful than the current neighbor-count death rule.

## Simulation Loop

Recommended force update:

```text
for each fixed step:
    rebuild spatial hash
    for each agent:
        desiredDirection = path or herding direction
        totalForce = driving force
    for each neighboring agent pair:
        add f_ij contact/social forces
    for each agent-wall candidate:
        add f_iW wall force
    for each agent:
        acceleration = totalForce / mass
        velocity = clamp(velocity + acceleration * dt)
        position = position + velocity * dt
        resolve hard world bounds if needed
        update transform from XZ position
```

The implemented domain layer keeps per-frame allocations low:

- `SocialForceSimulator` owns reusable force and pressure buffers.
- `SocialForceSpatialHash` reuses cell lists between frames instead of
  rebuilding all list objects.
- `SocialForceWallSpatialHash` is rebuilt only when walk boundaries change.
- `PedestrianCrowdSim` rebuilds road-wall boundaries only in `OnRoadsBuilt`.
- Runtime movement reuses cached segment candidate lists for path transitions.

Suggested timestep and clamps:

- Use Unity `FixedUpdate`.
- Start with `dt <= 0.02 s`.
- Clamp speed to around `max(desiredSpeed, 3 m/s)` for normal mode; allow more
  only in panic tests.
- Clamp acceleration during early tuning to prevent numeric blowups from deep
  overlaps.

## Integration Plan for This Repo

Current `PedestrianCrowdSim` stores a segment index, scalar progress `T`, and
lateral wander. To implement this paper's mechanism:

1. Add an SFM state struct with position, velocity, radius, mass, desired speed,
   path target, and accumulated force.
2. Keep `RoadPathfinding` for high-level route selection.
3. Replace `StepAgent` segment interpolation with force integration.
4. Build road boundaries from `WalkSegment` geometry or from visualizer road
   surfaces.
5. Add a spatial hash for neighbor lookup.
6. Replace neighbor-count pressure death with radial-force pressure.
7. Add debug visualization for desired velocity, actual velocity, contact force,
   and pressure.
8. Tune in a simple 15 m x 15 m room with a 1 m exit before using generated
   street graphs.

Initial implementation files:

- `Assets/Scripts/Domain/SocialForce/SocialForceAgentState.cs`
- `Assets/Scripts/Domain/SocialForce/SocialForceParameters.cs`
- `Assets/Scripts/Domain/SocialForce/SocialForceSimulator.cs`
- `Assets/Scripts/Domain/SocialForce/SocialForceSpatialHash.cs`
- `Assets/Scripts/Domain/SocialForce/SocialForceWallSpatialHash.cs`
- `Assets/Scripts/Domain/SocialForce/SocialForceWallSegment.cs`

`PedestrianCrowdSim` remains the Unity adapter. It owns GameObjects,
materials, graph path commands, and transform synchronization. The SFM domain
layer owns force calculation, pressure accumulation, contact friction, and
position/velocity integration.

## Expected Phenomena to Validate

Use these as acceptance tests:

- At normal desired speed, exit flow is regular.
- Above roughly `1.5 m/s`, bottleneck outflow becomes intermittent.
- With strong friction, increasing desired speed can reduce total outflow.
- High-density exits form arches/clogs.
- Injured/fallen agents act as new obstacles and slow escape.
- With herding enabled, high `p` overuses a single exit; intermediate `p`
  performs better than pure individualism or pure herding.

## Notes and Limitations

- The paper uses a 2D continuous model. In Unity, treat pedestrians as vertical
  cylinders but compute forces in XZ.
- The parameter values are calibrated for real-world meters, seconds, and
  Newtons. Keep Unity units as meters.
- The original paper intentionally uses identical core parameters except
  diameter variation. Add individual variation later, after matching baseline
  behavior.
- Do not mix this with a kinematic controller that teleports agents along
  segments; that bypasses the contact/friction mechanism.
