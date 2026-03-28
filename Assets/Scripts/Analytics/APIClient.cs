// System.Collections: IEnumerator 코루틴 반환 타입
using System.Collections;
// System.Linq: Select(), ToList() 등 LINQ 확장 메서드
using System.Linq;
// System.Text: Encoding.UTF8 문자열 인코딩
using System.Text;
using UnityEngine;
// UnityEngine.Networking: UnityWebRequest HTTP 통신 클래스
using UnityEngine.Networking;

/// <summary>
/// 오프라인 큐의 미전송 이벤트를 API 서버로 배치 전송.
/// apiEnabled = false 이면 아무것도 하지 않음.
/// </summary>
public class APIClient : MonoBehaviour
{
    // [Header]: 인스펙터에서 필드 그룹 제목 표시
    [Header("References")]
    // SoapUsageLogger: 미전송 이벤트 큐를 관리하는 싱글톤 로거
    public SoapUsageLogger logger;

    // AnalyticsConfig: API URL, 타임아웃, 배치 크기 등 설정
    private AnalyticsConfig _config;

    // RetryInterval: 전송 재시도 간격 (초)
    private const float RetryInterval = 30f;
    // MsPerSecond: 밀리초를 초로 변환하는 상수
    private const int   MsPerSecond   = 1000;

    // Start(): MonoBehaviour 생명주기 - 첫 프레임 전 1회 호출
    void Start()
    {
        // logger?.Config: logger가 null이 아닐 때만 Config 접근
        // ?? new AnalyticsConfig(): null이면 기본 설정 생성
        _config = logger?.Config ?? new AnalyticsConfig();

        // apiEnabled가 false면 전송 비활성화 (로컬 저장 전용 모드)
        if (!_config.apiEnabled)
        {
            Debug.Log("[APIClient] API 비활성 — 로컬 저장 전용 모드");
            return;
        }

        // StartCoroutine(): 코루틴 시작 - 무한 전송 루프 실행
        StartCoroutine(SendLoop());
    }

    // IEnumerator: 코루틴 반환 타입 - yield로 실행 중단/재개 가능
    private IEnumerator SendLoop()
    {
        // while (true): 무한 루프 - 30초마다 전송 시도
        while (true)
        {
            // WaitForSeconds: 지정 시간(초) 동안 코루틴 일시 중단
            yield return new WaitForSeconds(RetryInterval);
            // yield return 코루틴: 내부 코루틴 완료까지 대기
            yield return SendPendingEvents();
        }
    }

    // SendPendingEvents(): 미전송 이벤트를 HTTP POST로 서버에 전송
    private IEnumerator SendPendingEvents()
    {
        // 가드 조건: logger가 없으면 즉시 종료
        if (logger == null)
        {
            // yield break: 코루틴 즉시 종료 (return과 유사)
            yield break;
        }

        // GetPendingEvents(): 미전송 이벤트를 batchSendSize만큼 조회
        var pending = logger.GetPendingEvents(_config.batchSendSize);

        // 전송할 이벤트가 없으면 종료
        if (pending.Count == 0)
        {
            yield break;
        }

        // 문자열 보간($): apiBaseUrl에 /events 엔드포인트 추가
        string url  = $"{_config.apiBaseUrl}/events";
        // BuildBatchPayload(): 이벤트 목록을 JSON 문자열로 변환
        string body = BuildBatchPayload(pending);

        // using var: 스코프 종료 시 자동으로 Dispose() 호출
        // UnityWebRequest: Unity의 HTTP 클라이언트 클래스
        using var req = new UnityWebRequest(url, "POST")
        {
            // UploadHandlerRaw: 바이트 배열을 요청 본문으로 설정
            uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
            // DownloadHandlerBuffer: 응답 본문을 메모리에 저장
            downloadHandler = new DownloadHandlerBuffer(),
            // timeout: 요청 제한 시간 (초 단위)
            timeout         = _config.apiTimeoutMs / MsPerSecond
        };

        // SetRequestHeader(): HTTP 헤더 설정 - JSON 컨텐츠 타입 명시
        req.SetRequestHeader("Content-Type", "application/json");

        // SendWebRequest(): 비동기 HTTP 요청 전송 - 완료까지 대기
        yield return req.SendWebRequest();

        // Result.Success: HTTP 요청이 성공적으로 완료됨
        if (req.result == UnityWebRequest.Result.Success)
        {
            // Select(): 각 이벤트에서 eventId만 추출 (LINQ)
            // ToList(): IEnumerable을 List로 변환
            var ids = pending
                .Select(e => e.eventId)
                .ToList();

            // MarkTransmitted(): 전송 완료된 이벤트 ID를 로거에 알림
            logger.MarkTransmitted(ids);
            Debug.Log($"[APIClient] {ids.Count}개 이벤트 전송 완료");
        }
        else
        {
            // req.error: HTTP 오류 메시지 (타임아웃, 네트워크 오류 등)
            Debug.LogWarning($"[APIClient] 전송 실패: {req.error}");
        }
    }

    // BuildBatchPayload(): 이벤트 목록을 JSON 문자열로 직렬화
    private string BuildBatchPayload(System.Collections.Generic.List<UsageEvent> events)
    {
        // UsageEventQueue: JsonUtility가 List를 직렬화하지 못해 래퍼 클래스 사용
        var wrapper = new UsageEventQueue();
        // AddRange(): 컬렉션의 모든 요소를 한번에 추가
        wrapper.events.AddRange(events);
        // JsonUtility.ToJson(): Unity 내장 JSON 직렬화 (Newtonsoft 불필요)
        return JsonUtility.ToJson(wrapper);
    }

    /// <summary>즉시 전송 (외부 호출용)</summary>
    // FlushNow(): 30초 대기 없이 즉시 전송 (앱 종료 전 등에 사용)
    public void FlushNow()
    {
        StartCoroutine(SendPendingEvents());
    }
}
