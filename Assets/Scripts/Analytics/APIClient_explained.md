# APIClient.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Analytics/APIClient.cs`
- **목적**: 로컬에 저장된 미전송 사용 이벤트를 외부 API 서버로 배치 전송

## 의존성

| 네임스페이스 | 용도 |
|-------------|------|
| `System.Collections` | `IEnumerator` 코루틴 반환 타입 |
| `System.Linq` | `.Select()`, `.ToList()` LINQ 메서드 |
| `System.Text` | `Encoding.UTF8` 문자열 인코딩 |
| `UnityEngine` | `MonoBehaviour`, `Debug`, `JsonUtility` |
| `UnityEngine.Networking` | `UnityWebRequest` HTTP 요청 |

---

## 코드 분석

### Line 7-11: 클래스 선언

```csharp
/// <summary>
/// 오프라인 큐의 미전송 이벤트를 API 서버로 배치 전송.
/// apiEnabled = false 이면 아무것도 하지 않음.
/// </summary>
public class APIClient : MonoBehaviour
```
- `MonoBehaviour` 상속으로 Unity 생명주기 메서드(`Start`) 및 코루틴 사용 가능
- XML 문서 주석으로 클래스 목적 명시

---

### Line 13-19: 필드 선언

```csharp
[Header("References")]
public SoapUsageLogger logger;

private AnalyticsConfig _config;

private const float RetryInterval = 30f;
private const int   MsPerSecond   = 1000;
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `logger` | `SoapUsageLogger` | 미전송 이벤트 큐를 관리하는 로거 참조 |
| `_config` | `AnalyticsConfig` | API URL, 타임아웃 등 설정값 |
| `RetryInterval` | `float` | 전송 재시도 간격 (30초) |
| `MsPerSecond` | `int` | 밀리초→초 변환 상수 (1000) |

---

### Line 21-32: Start() — 초기화

```csharp
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
```

**동작 흐름:**
1. `logger?.Config` — logger가 null이 아니면 Config 속성 접근
2. `?? new AnalyticsConfig()` — null이면 기본 설정 생성
3. `apiEnabled`가 false면 코루틴 시작 안 함 (로컬 저장 전용)
4. `StartCoroutine(SendLoop())` — 무한 전송 루프 시작

---

### Line 34-41: SendLoop() — 무한 전송 루프

```csharp
private IEnumerator SendLoop()
{
    while (true)
    {
        yield return new WaitForSeconds(RetryInterval);
        yield return SendPendingEvents();
    }
}
```

**동작:**
- 30초(`RetryInterval`) 대기 후 `SendPendingEvents()` 실행
- 무한 반복 (`while (true)`)
- `yield return` 두 번 사용: 대기 완료 후 전송 완료까지 대기

---

### Line 43-84: SendPendingEvents() — 실제 HTTP 전송

```csharp
private IEnumerator SendPendingEvents()
```

#### 단계 1: 가드 조건 (Line 45-55)
```csharp
if (logger == null) { yield break; }

var pending = logger.GetPendingEvents(_config.batchSendSize);

if (pending.Count == 0) { yield break; }
```
- logger가 없거나 전송할 이벤트가 없으면 즉시 종료
- `batchSendSize` 만큼 미전송 이벤트 조회

#### 단계 2: HTTP 요청 생성 (Line 57-67)
```csharp
string url  = $"{_config.apiBaseUrl}/events";
string body = BuildBatchPayload(pending);

using var req = new UnityWebRequest(url, "POST")
{
    uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
    downloadHandler = new DownloadHandlerBuffer(),
    timeout         = _config.apiTimeoutMs / MsPerSecond
};

req.SetRequestHeader("Content-Type", "application/json");
```

| 구성 요소 | 설명 |
|----------|------|
| `url` | `{apiBaseUrl}/events` 엔드포인트 |
| `uploadHandler` | JSON 본문을 UTF-8 바이트로 인코딩 |
| `downloadHandler` | 응답 데이터 수신 버퍼 |
| `timeout` | ms → 초로 변환된 타임아웃 |
| `using var` | 요청 완료 후 자동 리소스 해제 |

#### 단계 3: 요청 전송 및 응답 처리 (Line 69-83)
```csharp
yield return req.SendWebRequest();

if (req.result == UnityWebRequest.Result.Success)
{
    var ids = pending.Select(e => e.eventId).ToList();
    logger.MarkTransmitted(ids);
    Debug.Log($"[APIClient] {ids.Count}개 이벤트 전송 완료");
}
else
{
    Debug.LogWarning($"[APIClient] 전송 실패: {req.error}");
}
```

**성공 시:**
1. 전송된 이벤트의 ID 목록 추출 (LINQ `Select`)
2. `MarkTransmitted()` 호출하여 로컬 큐에서 전송 완료 표시
3. 성공 로그 출력

**실패 시:**
- 경고 로그만 출력 (다음 주기에 자동 재시도)

---

### Line 86-91: BuildBatchPayload() — JSON 페이로드 생성

```csharp
private string BuildBatchPayload(System.Collections.Generic.List<UsageEvent> events)
{
    var wrapper = new UsageEventQueue();
    wrapper.events.AddRange(events);
    return JsonUtility.ToJson(wrapper);
}
```

**동작:**
- `JsonUtility`는 `List<T>` 직접 직렬화 불가
- `UsageEventQueue` 래퍼 클래스로 감싸서 JSON 변환
- 반환 예: `{"events":[{...},{...}]}`

---

### Line 93-97: FlushNow() — 즉시 전송 API

```csharp
/// <summary>즉시 전송 (외부 호출용)</summary>
public void FlushNow()
{
    StartCoroutine(SendPendingEvents());
}
```

- 외부 스크립트에서 호출 가능한 `public` 메서드
- 30초 대기 없이 즉시 전송 시도
- 예: 앱 종료 전 강제 플러시

---

## 데이터 흐름

```
SoapUsageLogger.GetPendingEvents()
        ↓
  List<UsageEvent>
        ↓
BuildBatchPayload() → JSON 문자열
        ↓
UnityWebRequest POST
        ↓
성공 → MarkTransmitted(ids)
실패 → 30초 후 재시도
```

## 설정 의존성 (AnalyticsConfig)

| 속성 | 용도 |
|------|------|
| `apiEnabled` | false면 전송 비활성화 |
| `apiBaseUrl` | API 서버 기본 URL |
| `apiTimeoutMs` | 요청 타임아웃 (밀리초) |
| `batchSendSize` | 한 번에 전송할 최대 이벤트 수 |
