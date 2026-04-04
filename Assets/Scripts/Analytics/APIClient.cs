using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 오프라인 큐의 미전송 이벤트를 API 서버로 배치 전송.
/// apiEnabled = false 이면 아무것도 하지 않음.
/// </summary>
public class APIClient : MonoBehaviour
{
    [Header("References")]
    public SoapUsageLogger logger;

    private AnalyticsConfig _config;

    private const float RetryInterval = 30f;
    private const int   MsPerSecond   = 1000;

    void Start()
    {
        _config = logger?.Config ?? new AnalyticsConfig();

        if (!_config.apiEnabled)
        {
            Debug.Log("[APIClient] API 비활성 — 로컬 저장 전용 모드");
            return;
        }

        StartCoroutine(SendLoop());
    }

    private IEnumerator SendLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(RetryInterval);
            yield return SendPendingEvents();
        }
    }

    private IEnumerator SendPendingEvents()
    {
        if (logger == null)
        {
            yield break;
        }

        var pending = logger.GetPendingEvents(_config.batchSendSize);

        if (pending.Count == 0)
        {
            yield break;
        }

        string url  = $"{_config.apiBaseUrl}/events";
        string body = BuildBatchPayload(pending);

        using var req = new UnityWebRequest(url, "POST")
        {
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
            downloadHandler = new DownloadHandlerBuffer(),
            timeout         = _config.apiTimeoutMs / MsPerSecond
        };

        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var ids = pending
                .Select(e => e.eventId)
                .ToList();

            logger.MarkTransmitted(ids);
            Debug.Log($"[APIClient] {ids.Count}개 이벤트 전송 완료");
        }
        else
        {
            Debug.LogWarning($"[APIClient] 전송 실패: {req.error}");
        }
    }

    private string BuildBatchPayload(System.Collections.Generic.List<UsageEvent> events)
    {
        var wrapper = new UsageEventQueue();
        wrapper.events.AddRange(events);
        return JsonUtility.ToJson(wrapper);
    }

    /// <summary>즉시 전송 (외부 호출용)</summary>
    public void FlushNow()
    {
        StartCoroutine(SendPendingEvents());
    }
}
