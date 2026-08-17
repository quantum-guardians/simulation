# Project Context

Fill this document during project initialization. Agents must verify commands against repository configuration before running them.

## Overview

- Product: MR2S Unity simulation — a 3D pedestrian crowd simulation over a
  generated planar graph, with edge directions obtained from the MR2S API.
- Repository: https://github.com/quantum-guardians/simulation
- Primary users: demo audiences; the WebGL build is published to GitHub Pages.
- Core domain: planar graph generation, walk-network pathfinding, Social Force
  Model crowd movement, graph visualization.
- Runtime environment: Unity 6000.4.1f1 (URP), C#. Target platform WebGL.

## Architecture

- Entry points: `Assets/Scenes/SampleScene.unity`; behavior is attached through
  MonoBehaviours, with `GraphManager` as the central coordinator.
- Main modules: `Assets/Scripts/Manager/` (`GraphManager`,
  `PlanarGraphGenerator`, `GraphInputParser`, `SmallWorldApiClient`,
  `SmallWorldApiCodec`, `SmallWorldApiDtos`), `Assets/Scripts/Data/`
  (`GraphData`, `WalkSegment`), `Assets/Scripts/Simulation/`
  (`PedestrianCrowdSim`, `PedestrianWalkNetwork`, `PedestrianAgentRuntime`,
  `RoadPathfinding`, `PedestrianViewFactory`, `PedestrianCommandInput`),
  `Assets/Scripts/Visualization/` (`GraphVisualizer`, `NodeLabelBillboard`),
  `Assets/Scripts/UI/` (`GraphInputUI`, `PedestrianCrowdUI`).
- Dependency direction: UI and Visualization depend on Manager and Simulation;
  Simulation depends on Data; Data depends on nothing.
- External systems: the MR2S backend at
  `https://quantum.yunseong.dev/api/v1/mr2s`, called from
  `SmallWorldApiClient` (serialized `apiUrl` field, falling back to
  `http://localhost:8000/api/v1/mr2s` when blank).
- Persistent data: none beyond scene and project assets.

## Commands

| Purpose | Command |
|---|---|
| Install dependencies | Open the project in Unity 6000.4.1f1; the Package Manager resolves `Packages/manifest.json` |
| Run locally | Play mode on `Assets/Scenes/SampleScene.unity` in the Unity Editor |
| Format | TODO — none configured |
| Lint | TODO — none configured |
| Type-check | Unity compiles on domain reload; check the Console for errors |
| Unit tests | `com.unity.test-framework` is installed but no tests exist yet; run via Window ▸ General ▸ Test Runner |
| Integration tests | TODO — none |
| Build | CI: `game-ci/unity-builder@v4`, WebGL, into `Builds/`. Locally: File ▸ Build Settings ▸ WebGL |

## Constraints

- Supported platforms: WebGL is the shipped target; the Editor is the
  development environment.
- Compatibility requirements: pin to Unity 6000.4.1f1 — `ProjectVersion.txt`
  and the CI `unityVersion` must stay in sync. WebGL has no threads, so keep
  API calls on coroutines and `UnityWebRequest`.
- Performance constraints: crowd simulation runs per-frame in `Update`; agent
  count is the main cost driver.
- Security or privacy requirements: the API URL is a serialized inspector
  field, not a secret. Unity license credentials live only in GitHub secrets
  (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`).

## Ownership

- Maintainers: Yunseong <me@yunseong.dev>
- Sensitive modules: `Assets/Scripts/Simulation/PedestrianCrowdSim.cs`,
  `Assets/Scripts/Manager/SmallWorldApiClient.cs`, `ProjectSettings/`
- Changes requiring explicit review: Unity or package version bumps, scene and
  ProjectSettings changes, force-model tuning, API contract changes.
