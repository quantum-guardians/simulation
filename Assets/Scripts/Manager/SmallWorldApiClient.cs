using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class SmallWorldApiClient : MonoBehaviour
{
    [SerializeField] string apiUrl = "https://quantum.yunseong.dev/api/v1/mr2s";

    public bool IsRequestInProgress { get; private set; }

    public event Action<GraphData> OnResponseReceived;
    public event Action<string> OnRequestFailed;

    public void SendRequest(GraphData directedInput)
    {
        if (IsRequestInProgress)
        {
            Debug.LogWarning("[SmallWorldApiClient] 이미 요청 중입니다.");
            return;
        }

        if (directedInput == null || directedInput.Edges.Count == 0)
        {
            Debug.LogWarning("[SmallWorldApiClient] 전송할 간선이 없습니다.");
            return;
        }

        StartCoroutine(PostRoutine(directedInput));
    }

    IEnumerator PostRoutine(GraphData directedInput)
    {
        IsRequestInProgress = true;
        var json = SmallWorldApiCodec.BuildRequestJson(directedInput);
        var url = string.IsNullOrWhiteSpace(apiUrl)
            ? "http://localhost:8000/api/v1/mr2s"
            : apiUrl.Trim();

        using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
        var bodyRaw = Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
        var ok = req.result == UnityWebRequest.Result.Success;
#else
        var ok = !req.isNetworkError && !req.isHttpError;
#endif
        if (!ok)
        {
            var errorMsg = $"Small-world 요청 실패: {req.error} / HTTP {req.responseCode}\n{req.downloadHandler.text}";
            Debug.LogWarning("[SmallWorldApiClient] " + errorMsg);
            IsRequestInProgress = false;
            OnRequestFailed?.Invoke(errorMsg);
            yield break;
        }

        var text = req.downloadHandler.text;
        if (!SmallWorldApiCodec.TryParseResponseBody(text, out var result, out var parseErr))
        {
            var errorMsg = "응답 파싱 실패: " + parseErr + "\n본문:\n" + text;
            Debug.LogWarning("[SmallWorldApiClient] " + errorMsg);
            IsRequestInProgress = false;
            OnRequestFailed?.Invoke(errorMsg);
            yield break;
        }

        IsRequestInProgress = false;
        OnResponseReceived?.Invoke(result);
    }
}
