using UnityEngine;
using UnityEngine.UI;

public class GraphInputUI : MonoBehaviour
{
    [SerializeField] GraphManager graphManager;
    [SerializeField] InputField edgeInput;
    [SerializeField] Button drawGraphButton;
    [Tooltip("Small-world API로 방향 그래프 전송·응답 시각화")]
    [SerializeField] Button drawNodeButton;
    [SerializeField] Button buildButton;

    void Reset()
    {
        graphManager = FindAnyObjectByType<GraphManager>();
    }

    void Awake()
    {
        if (edgeInput == null) return;
        edgeInput.lineType = InputField.LineType.MultiLineNewline;
    }

    void OnEnable()
    {
        if (drawGraphButton != null) drawGraphButton.onClick.AddListener(OnBuildClicked);
        if (drawNodeButton != null) drawNodeButton.onClick.AddListener(OnDrawNodeClicked);
        if (buildButton != null) buildButton.onClick.AddListener(OnConfirmClicked);
    }

    void OnDisable()
    {
        if (drawGraphButton != null) drawGraphButton.onClick.RemoveListener(OnBuildClicked);
        if (drawNodeButton != null) drawNodeButton.onClick.RemoveListener(OnDrawNodeClicked);
        if (buildButton != null) buildButton.onClick.RemoveListener(OnConfirmClicked);
    }

    void OnBuildClicked()
    {
        if (graphManager == null)
        {
            Debug.LogError("[GraphInputUI] GraphManager 참조가 없습니다.");
            return;
        }

        var text = edgeInput != null ? edgeInput.text : string.Empty;
        graphManager.BuildGraphFromInput(text);
    }

    void OnDrawNodeClicked()
    {
        if (graphManager == null)
        {
            Debug.LogError("[GraphInputUI] GraphManager 참조가 없습니다.");
            return;
        }

        var text = edgeInput != null ? edgeInput.text : string.Empty;
        graphManager.RequestSmallWorldFromInput(text);
    }

    void OnConfirmClicked()
    {
        if (graphManager == null)
        {
            Debug.LogError("[GraphInputUI] GraphManager 참조가 없습니다.");
            return;
        }

        graphManager.ConfirmLayoutAndBuildRoads();
    }
}
