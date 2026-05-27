using UnityEngine;

public class GraphManager : MonoBehaviour
{
    [SerializeField] GraphVisualizer visualizer;
    [SerializeField] float defaultGroundY;
    [Tooltip("도로 빌드 후 보행 시뮬 네트워크 갱신 (비우면 생략)")]
    [SerializeField] PedestrianCrowdSim pedestrianCrowdSim;

    [Header("Small-world API")]
    [SerializeField] SmallWorldApiClient apiClient;

    [Header("Auto-Generate")]
    [SerializeField] int autoVertexCount = 10;

    GraphData _graphData;
    readonly System.Collections.Generic.List<string> _parseWarnings = new();

    public GraphData CurrentGraph => _graphData;
    public float DefaultGroundY => defaultGroundY;

    void Awake()
    {
        EnsureApiClient();
    }

    void OnEnable()
    {
        EnsureApiClient();

        if (apiClient != null)
        {
            apiClient.OnResponseReceived += OnSmallWorldResponse;
            apiClient.OnRequestFailed += OnSmallWorldFailed;
        }
    }

    void OnDisable()
    {
        if (apiClient != null)
        {
            apiClient.OnResponseReceived -= OnSmallWorldResponse;
            apiClient.OnRequestFailed -= OnSmallWorldFailed;
        }
    }

    public void BuildGraphFromInput(string inputText)
    {
        _parseWarnings.Clear();
        if (!GraphInputParser.TryParseUndirected(inputText, out var data, out var error, _parseWarnings))
        {
            Debug.LogWarning("[GraphManager] " + error);
            return;
        }

        LogParseWarnings();
        data.Directed = false;
        _graphData = data;
        pedestrianCrowdSim?.ClearAllAgents();
        visualizer.Clear();
        visualizer.BuildFromGraph(_graphData, defaultGroundY);
    }

    public void RequestSmallWorldFromInput(string inputText)
    {
        EnsureApiClient();

        _parseWarnings.Clear();
        if (!GraphInputParser.TryParseDirected(inputText, out var requestData, out var err, _parseWarnings))
        {
            Debug.LogWarning("[GraphManager] " + err);
            return;
        }
        LogParseWarnings();

        if (apiClient == null)
        {
            Debug.LogWarning("[GraphManager] SmallWorldApiClient를 찾을 수 없습니다.");
            return;
        }

        apiClient.SendRequest(requestData);
    }

    public void GeneratePlanarGraph()
    {
        var gen = new PlanarGraphGenerator(autoVertexCount, PlanarGraphGenerator.DefaultRemoveRatio);
        var data = gen.Generate();

        _graphData = data;
        pedestrianCrowdSim?.ClearAllAgents();
        visualizer.Clear();
        visualizer.BuildFromGraph(_graphData, defaultGroundY, keepExistingPositions: true);
    }

    void OnSmallWorldResponse(GraphData result)
    {
        _graphData = result;
        pedestrianCrowdSim?.ClearAllAgents();
        visualizer.Clear();
        visualizer.BuildFromGraph(_graphData, defaultGroundY);
    }

    void OnSmallWorldFailed(string error)
    {
        Debug.LogWarning("[GraphManager] Small-world API 실패: " + error);
    }

    void EnsureApiClient()
    {
        if (apiClient != null)
            return;

        apiClient = GetComponent<SmallWorldApiClient>();
        if (apiClient == null)
            apiClient = gameObject.AddComponent<SmallWorldApiClient>();
    }

    void LogParseWarnings()
    {
        foreach (var warning in _parseWarnings)
            Debug.LogWarning("[GraphManager] " + warning);
    }

    public void ConfirmLayoutAndBuildRoads()
    {
        if (_graphData == null || _graphData.Nodes.Count == 0)
        {
            Debug.LogWarning("[GraphManager] 확정할 그래프가 없습니다.");
            return;
        }

        visualizer.SyncPositionsFromSceneToData(_graphData);
        visualizer.BuildRoads(_graphData, defaultGroundY);
        pedestrianCrowdSim?.OnRoadsBuilt(_graphData, defaultGroundY);
    }

    public void ClearAll()
    {
        _graphData = null;
        pedestrianCrowdSim?.ClearAllAgents();
        visualizer.Clear();
    }

}
