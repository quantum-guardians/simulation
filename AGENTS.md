# Project Agent Instructions

## Scope and Precedence

This file is the repository-level entrypoint for coding agents.

Read `.agents/docs/project.md` before non-trivial work. Repository-specific
commands, constraints, and narrower instructions take precedence over these
template defaults.

## Project Workflow

For non-trivial work, follow:

- `.agents/docs/workflow.md`
- `.agents/docs/testing.md`

For tracked Git work, follow:

- `.agents/docs/issue.md`
- `.agents/docs/branch.md`
- `.agents/docs/commit.md`
- `.agents/docs/pull-request.md`

For published releases, follow:

- `.agents/docs/release.md`

Use project-local skills when installed and applicable. Skill instructions
define their own triggers, formats, and output paths.

## Project Structure & Module Organization

This is a Unity 6000.4.1f1 project on the Universal Render Pipeline. All code
lives under `Assets/Scripts/`, split by responsibility: `Manager/` coordinates
the graph and the API (`GraphManager`, `PlanarGraphGenerator`,
`GraphInputParser`, `SmallWorldApiClient`, `SmallWorldApiCodec`,
`SmallWorldApiDtos`), `Data/` holds plain models (`GraphData`, `WalkSegment`),
`Simulation/` holds crowd behavior (`PedestrianCrowdSim`,
`PedestrianWalkNetwork`, `PedestrianAgentRuntime`, `RoadPathfinding`,
`PedestrianViewFactory`, `PedestrianCommandInput`), `Visualization/` draws the
graph, and `UI/` wires the panels. `Assets/Scenes/SampleScene.unity` is the only
scene. `Packages/manifest.json` and `ProjectSettings/` are project
configuration. Reference material is in `docs/social-force-model-escape-panic.md`.

## Unity-Specific Rules

Every asset carries a sibling `.meta` file — always commit the `.meta` with the
asset, and never delete or regenerate one on its own, since the GUID inside it
is what scene and prefab references point at. Do not commit `Library/`,
`Temp/`, `Builds/`, `Logs/`, or `UserSettings/`; `.gitignore` already covers
them. Scene and `ProjectSettings` files are YAML that merges badly — coordinate
before editing them in parallel, and describe such changes explicitly in the
pull request. Keep the Unity version pinned: `ProjectSettings/ProjectVersion.txt`
and the `unityVersion` in `.github/workflows/unity.yml` must match.

## Build, Test, and Development Commands

Open the project in Unity 6000.4.1f1 and enter Play mode on `SampleScene` to
run it. Push to `main` triggers `.github/workflows/unity.yml`, which builds
WebGL through `game-ci/unity-builder@v4` and deploys it to GitHub Pages, so a
compile error on `main` breaks the published demo. Build locally through
File ▸ Build Settings ▸ WebGL when verifying a WebGL-specific change.

## Coding Style & Naming Conventions

Follow the existing C# style: 4-space indentation, `PascalCase` for types,
methods, and public members, `camelCase` for private fields and locals,
one type per file with the file named after the type. MonoBehaviours expose
tunables with `[SerializeField]` private fields rather than public fields.
Communicate across systems with C# events, as `SmallWorldApiClient` does with
`OnResponseReceived` and `OnRequestFailed`. Network work goes through
coroutines and `UnityWebRequest` — WebGL has no threads. Existing log messages
are prefixed with the class name in brackets and written in Korean; match that.

## Testing Guidelines

`com.unity.test-framework` is installed but no tests exist yet. Add EditMode
tests for pure logic — graph generation, parsing, pathfinding — under an
`Assets/Tests/` folder with its own assembly definition, and run them from
Window ▸ General ▸ Test Runner. Crowd behavior is verified in Play mode; when
changing it, state in the pull request which scenario you ran, the agent count,
and what you observed.

## Commit & Pull Request Guidelines

History uses Conventional Commit subjects with a scope, such as
`feat(simulation): add social force model for pedestrian crowds` and
`fix(simulation): position-based path following, wall force tuning`; some
earlier entries are in Korean. Branch names follow `<tag>/<issue num>`, for
example `feat/1`. Pull requests should describe the behavioral change, link the
issue, call out any scene or ProjectSettings edit, and include a clip or
screenshot when visuals or crowd behavior change.
