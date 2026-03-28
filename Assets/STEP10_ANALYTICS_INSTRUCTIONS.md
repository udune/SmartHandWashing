# STEP 10 — 사용량 분석 + 교체 예측 + API 친화적 설계 (Claude Code 작업 지시서)

> **전제 조건:** STEP 4~9 구현 완료 상태  
> **핵심 원칙:** 로컬 JSON으로 완전 동작 → 추후 API 서버 연결 시 코드 수정 없음  
> **추가되는 UI:** 우측 상단 시간대별 사용량 그래프 패널 + 비누 교체 예측 알람 배너

---

## 추가/수정 파일 목록

```
Assets/
  Scripts/
    Analytics/
      UsageEvent.cs            ← 신규: 이벤트 데이터 모델 (API 스키마와 동일)
      SoapUsageLogger.cs       ← 신규: 로컬 저장 + 오프라인 큐 관리
      ReplenishPredictor.cs    ← 신규: 비누 교체 예측일 계산
      APIClient.cs             ← 신규: HTTP POST (서버 없으면 자동 비활성)
    UI/
      AnalyticsPanel.cs        ← 신규: 그래프 패널 제어
      BarChartElement.cs       ← 신규: painter2D 막대 그래프 커스텀 엘리먼트
      AlarmBanner.cs           ← 신규: 교체 예측 배너 제어
  UI/
    UXML/
      AnalyticsPanel.uxml      ← 신규: 우측 상단 그래프 패널
      AlarmBanner.uxml         ← 신규: 교체 알람 배너
    USS/
      AnalyticsTheme.uss       ← 신규: 분석 패널 전용 스타일
  Resources/
    AnalyticsConfig.json       ← 신규: stationId, API URL, 임계값 설정

  -- 기존 파일 수정 --
  Scripts/Data/StationData.cs          ← hourlyUsageCount 배열 추가
  Scripts/Core/StationController.cs    ← ActivateSoap()에 로거 호출 추가
  UI/UXML/MainHMI.uxml                 ← 패널 및 배너 삽입
```

---

## 10-1. AnalyticsConfig.json

`Assets/Resources/AnalyticsConfig.json` 을 아래 내용으로 생성해줘:

```json
{
  "stationId": "station-01",
  "stationName": "스마트 손 씻기 스테이션",
  "apiBaseUrl": "",
  "apiEnabled": false,
  "apiTimeoutMs": 5000,
  "maxQueueSize": 1000,
  "batchSendSize": 50,
  "prediction": {
    "lookbackDays": 7,
    "warnDays": 7,
    "alertDays": 3
  },
  "soapDecreasePerUse": 5.0
}
```

> **API 서버 완성 후:** `"apiBaseUrl": "http://your-server/api"`, `"apiEnabled": true` 로 변경

---

## 10-2. UsageEvent.cs — 이벤트 데이터 모델

`Assets/Scripts/Analytics/UsageEvent.cs` 를 생성해줘:

```csharp
using System;

/// <summary>
/// API 서버 전송 스키마와 동일한 구조.
/// 로컬 JSON 저장 → HTTP POST 전송 시 이 클래스를 그대로 직렬화.
/// </summary>
[Serializable]
public class UsageEvent
{
    public string eventId;          // GUID — 중복 방지
    public string stationId;        // AnalyticsConfig.stationId
    public string timestamp;        // ISO 8601: "2024-10-28T13:05:22Z"
    public string type;             // "soap_dispensed" | "water_on" | "air_on"
    public float  soapLevelBefore;  // 사용 전 잔량 (%)
    public float  soapLevelAfter;   // 사용 후 잔량 (%)
    public bool   transmitted;      // 서버 전송 완료 여부

    public static UsageEvent CreateSoapEvent(string stationId, float before, float after)
    {
        return new UsageEvent
        {
            eventId        = Guid.NewGuid().ToString(),
            stationId      = stationId,
            timestamp      = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            type           = "soap_dispensed",
            soapLevelBefore = before,
            soapLevelAfter  = after,
            transmitted    = false,
        };
    }

    public static UsageEvent CreateWaterEvent(string stationId)
    {
        return new UsageEvent
        {
            eventId    = Guid.NewGuid().ToString(),
            stationId  = stationId,
            timestamp  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            type       = "water_on",
            transmitted = false,
        };
    }

    public static UsageEvent CreateAirEvent(string stationId)
    {
        return new UsageEvent
        {
            eventId    = Guid.NewGuid().ToString(),
            stationId  = stationId,
            timestamp  = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            type       = "air_on",
            transmitted = false,
        };
    }
}

[Serializable]
public class UsageEventQueue
{
    public System.Collections.Generic.List<UsageEvent> events
        = new System.Collections.Generic.List<UsageEvent>();
}

[Serializable]
public class AnalyticsConfig
{
    public string stationId        = "station-01";
    public string stationName      = "스마트 손 씻기 스테이션";
    public string apiBaseUrl       = "";
    public bool   apiEnabled       = false;
    public int    apiTimeoutMs     = 5000;
    public int    maxQueueSize     = 1000;
    public int    batchSendSize    = 50;
    public PredictionConfig prediction = new PredictionConfig();
    public float  soapDecreasePerUse   = 5f;
}

[Serializable]
public class PredictionConfig
{
    public int lookbackDays = 7;   // 예측에 사용할 과거 일수
    public int warnDays     = 7;   // 주의 임계값 (일)
    public int alertDays    = 3;   // 경고 임계값 (일)
}
```

---

## 10-3. SoapUsageLogger.cs — 로컬 저장 + 오프라인 큐

`Assets/Scripts/Analytics/SoapUsageLogger.cs` 를 생성해줘:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 이벤트를 로컬 JSON에 저장하고 오프라인 큐를 관리.
/// APIClient가 전송에 성공하면 transmitted = true 로 마킹.
/// </summary>
public class SoapUsageLogger : MonoBehaviour
{
    private static SoapUsageLogger _instance;
    public  static SoapUsageLogger Instance => _instance;

    private AnalyticsConfig _config;
    private UsageEventQueue _queue;
    private string          _savePath;

    // 시간대별 집계 캐시 (0~23시) — 오늘 데이터
    private int[] _hourlyCount = new int[24];

    public int[]   HourlyCount  => _hourlyCount;
    public int     TodayTotal   => _hourlyCount.Sum();
    public int     PeakHour     { get; private set; }
    public int     PeakCount    { get; private set; }

    void Awake()
    {
        if (_instance != null) { Destroy(gameObject); return; }
        _instance = this;

        LoadConfig();
        LoadQueue();
        RebuildHourlyCache();
    }

    // ── 설정 로드 ────────────────────────────────────────────────────

    private void LoadConfig()
    {
        var json = Resources.Load<TextAsset>("AnalyticsConfig");
        _config  = json != null
            ? JsonUtility.FromJson<AnalyticsConfig>(json.text)
            : new AnalyticsConfig();
    }

    // ── 큐 저장/로드 ────────────────────────────────────────────────

    private void LoadQueue()
    {
        _savePath = Path.Combine(Application.persistentDataPath, "usage_log.json");

        if (File.Exists(_savePath))
        {
            try
            {
                string json = File.ReadAllText(_savePath);
                _queue = JsonUtility.FromJson<UsageEventQueue>(json) ?? new UsageEventQueue();
            }
            catch
            {
                _queue = new UsageEventQueue();
            }
        }
        else
        {
            _queue = new UsageEventQueue();
        }
    }

    private void SaveQueue()
    {
        try
        {
            File.WriteAllText(_savePath, JsonUtility.ToJson(_queue, true));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Logger] 큐 저장 실패: {e.Message}");
        }
    }

    // ── 이벤트 기록 ─────────────────────────────────────────────────

    public void LogSoap(float levelBefore, float levelAfter)
    {
        var ev = UsageEvent.CreateSoapEvent(_config.stationId, levelBefore, levelAfter);
        Enqueue(ev);

        int hour = DateTime.Now.Hour;
        _hourlyCount[hour]++;
        UpdatePeak();
    }

    public void LogWater() => Enqueue(UsageEvent.CreateWaterEvent(_config.stationId));
    public void LogAir()   => Enqueue(UsageEvent.CreateAirEvent(_config.stationId));

    private void Enqueue(UsageEvent ev)
    {
        _queue.events.Add(ev);

        // 최대 큐 크기 초과 시 전송 완료된 것부터 제거
        if (_queue.events.Count > _config.maxQueueSize)
        {
            _queue.events.RemoveAll(e => e.transmitted);
            // 그래도 초과면 가장 오래된 것 제거
            while (_queue.events.Count > _config.maxQueueSize)
                _queue.events.RemoveAt(0);
        }

        SaveQueue();
    }

    // ── 전송 완료 마킹 (APIClient에서 호출) ─────────────────────────

    public void MarkTransmitted(List<string> eventIds)
    {
        foreach (var ev in _queue.events)
            if (eventIds.Contains(ev.eventId))
                ev.transmitted = true;
        SaveQueue();
    }

    // ── 미전송 이벤트 조회 (APIClient에서 호출) ──────────────────────

    public List<UsageEvent> GetPendingEvents(int limit)
        => _queue.events
            .Where(e => !e.transmitted)
            .Take(limit)
            .ToList();

    // ── 시간대 집계 캐시 재구성 ──────────────────────────────────────

    private void RebuildHourlyCache()
    {
        _hourlyCount = new int[24];
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        foreach (var ev in _queue.events)
        {
            if (ev.type != "soap_dispensed") continue;
            if (!ev.timestamp.StartsWith(today)) continue;

            if (DateTime.TryParse(ev.timestamp, out DateTime dt))
                _hourlyCount[dt.Hour]++;
        }
        UpdatePeak();
    }

    private void UpdatePeak()
    {
        PeakCount = 0;
        PeakHour  = 0;
        for (int h = 0; h < 24; h++)
        {
            if (_hourlyCount[h] > PeakCount)
            {
                PeakCount = _hourlyCount[h];
                PeakHour  = h;
            }
        }
    }

    // ── 예측용 일별 사용 횟수 조회 ──────────────────────────────────

    /// <summary>과거 N일간 날짜별 비누 사용 횟수 반환</summary>
    public Dictionary<string, int> GetDailyUsage(int days)
    {
        var result = new Dictionary<string, int>();
        for (int i = 0; i < days; i++)
        {
            string date = DateTime.Now.AddDays(-i).ToString("yyyy-MM-dd");
            result[date] = _queue.events
                .Count(e => e.type == "soap_dispensed" && e.timestamp.StartsWith(date));
        }
        return result;
    }

    public AnalyticsConfig Config => _config;
}
```

---

## 10-4. ReplenishPredictor.cs — 교체 예측일 계산

`Assets/Scripts/Analytics/ReplenishPredictor.cs` 를 생성해줘:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AlarmLevel { None, Warn, Alert }

public class ReplenishResult
{
    public float      DaysRemaining;    // 예측 잔여일
    public DateTime   PredictedDate;    // 예측 교체일
    public AlarmLevel Level;            // 알람 단계
    public float      DailyUsageAvg;   // 일평균 사용 횟수
    public string     Message;          // 표시 메시지
}

/// <summary>
/// 최근 N일 이동 평균으로 비누 교체 예측일을 계산.
/// 알고리즘: 일평균 소비량(%) = 평균사용횟수 × 1회소비량
///           잔여일 = 현재잔량 / 일평균소비량
/// </summary>
public static class ReplenishPredictor
{
    public static ReplenishResult Predict(
        float currentSoapPct,
        Dictionary<string, int> dailyUsage,
        PredictionConfig config,
        float soapDecreasePerUse)
    {
        var result = new ReplenishResult();

        // 일평균 사용 횟수 계산 (데이터 없으면 기본값 10회)
        float avgDaily = dailyUsage.Count > 0
            ? (float)dailyUsage.Values.Average()
            : 10f;

        result.DailyUsageAvg = avgDaily;

        // 일평균 소비량 (%)
        float dailyConsumePct = avgDaily * soapDecreasePerUse;

        if (dailyConsumePct <= 0f)
        {
            result.DaysRemaining = float.MaxValue;
            result.Level         = AlarmLevel.None;
            result.Message       = "데이터 수집 중...";
            return result;
        }

        // 잔여일 계산
        result.DaysRemaining  = currentSoapPct / dailyConsumePct;
        result.PredictedDate  = DateTime.Now.AddDays(result.DaysRemaining);

        // 알람 단계 결정
        if (result.DaysRemaining <= config.alertDays)
        {
            result.Level   = AlarmLevel.Alert;
            result.Message = result.DaysRemaining < 1f
                ? "오늘 비누 교체 필요!"
                : $"D-{Mathf.CeilToInt(result.DaysRemaining)} 비누 교체 필요";
        }
        else if (result.DaysRemaining <= config.warnDays)
        {
            result.Level   = AlarmLevel.Warn;
            result.Message = $"약 {Mathf.CeilToInt(result.DaysRemaining)}일 후 교체 예정 ({result.PredictedDate:MM/dd})";
        }
        else
        {
            result.Level   = AlarmLevel.None;
            result.Message = $"교체 예정: {result.PredictedDate:MM/dd} (약 {Mathf.CeilToInt(result.DaysRemaining)}일 후)";
        }

        return result;
    }
}
```

---

## 10-5. APIClient.cs — HTTP 배치 전송

`Assets/Scripts/Analytics/APIClient.cs` 를 생성해줘:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 오프라인 큐의 미전송 이벤트를 API 서버로 배치 전송.
/// apiEnabled = false 이면 아무것도 하지 않음 — 코드 수정 불필요.
/// </summary>
public class APIClient : MonoBehaviour
{
    [Header("References")]
    public SoapUsageLogger logger;

    private AnalyticsConfig _config;
    private float           _retryTimer;
    private const float     RetryInterval = 30f;   // 30초마다 재시도

    void Start()
    {
        _config = logger != null ? logger.Config : new AnalyticsConfig();

        if (_config.apiEnabled)
            StartCoroutine(SendLoop());
        else
            Debug.Log("[APIClient] API 비활성 — 로컬 저장 전용 모드");
    }

    // ── 전송 루프 ────────────────────────────────────────────────────

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
        if (logger == null) yield break;

        var pending = logger.GetPendingEvents(_config.batchSendSize);
        if (pending.Count == 0) yield break;

        string url  = $"{_config.apiBaseUrl}/events";
        string body = BuildBatchPayload(pending);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.timeout = _config.apiTimeoutMs / 1000;

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var ids = pending.Select(e => e.eventId).ToList();
            logger.MarkTransmitted(ids);
            Debug.Log($"[APIClient] {ids.Count}개 이벤트 전송 완료");
        }
        else
        {
            Debug.LogWarning($"[APIClient] 전송 실패: {req.error} — 다음 주기에 재시도");
        }
    }

    // ── 페이로드 빌드 ────────────────────────────────────────────────

    private string BuildBatchPayload(List<UsageEvent> events)
    {
        // JsonUtility는 List 직렬화를 지원하지 않으므로 래퍼 사용
        var wrapper = new UsageEventQueue();
        wrapper.events.AddRange(events);
        return JsonUtility.ToJson(wrapper);
    }

    // ── 즉시 전송 (외부 호출용) ──────────────────────────────────────
    public void FlushNow() => StartCoroutine(SendPendingEvents());
}
```

---

## 10-6. BarChartElement.cs — painter2D 막대 그래프

`Assets/Scripts/UI/BarChartElement.cs` 를 생성해줘:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit 커스텀 VisualElement.
/// painter2D로 24시간 막대 그래프를 직접 드로우.
/// </summary>
public class BarChartElement : VisualElement
{
    // USS 클래스 팩토리 등록 (UXML에서 <BarChartElement> 태그로 사용 가능)
    public new class UxmlFactory : UxmlFactory<BarChartElement> { }

    private int[] _data    = new int[24];
    private int   _peakHour = -1;

    private static readonly Color BarNormal  = new Color(0.26f, 0.60f, 0.26f, 1f);  // 초록
    private static readonly Color BarPeak    = new Color(1.00f, 0.57f, 0.00f, 1f);  // 주황
    private static readonly Color BarAxis    = new Color(0.47f, 0.56f, 0.61f, 0.5f);
    private static readonly Color TextColor  = new Color(0.47f, 0.56f, 0.61f, 1f);

    public BarChartElement()
    {
        generateVisualContent += OnGenerateVisualContent;
    }

    public void SetData(int[] hourlyCount, int peakHour)
    {
        _data     = hourlyCount;
        _peakHour = peakHour;
        MarkDirtyRepaint();
    }

    private void OnGenerateVisualContent(MeshGenerationContext ctx)
    {
        var painter = ctx.painter2D;
        float w     = contentRect.width;
        float h     = contentRect.height;

        if (w <= 0 || h <= 0) return;

        const float paddingLeft   = 8f;
        const float paddingRight  = 8f;
        const float paddingBottom = 20f;   // X축 레이블 공간
        const float paddingTop    = 8f;

        float chartW = w - paddingLeft - paddingRight;
        float chartH = h - paddingBottom - paddingTop;

        // 최댓값 계산 (최소 1 보정)
        int maxVal = 1;
        foreach (var v in _data) if (v > maxVal) maxVal = v;

        float barW   = chartW / 24f;
        float barGap = barW * 0.15f;
        float bottom = h - paddingBottom;

        // ── X축 기준선 ───────────────────────────────────────────────
        painter.strokeColor = BarAxis;
        painter.lineWidth   = 0.5f;
        painter.BeginPath();
        painter.MoveTo(new Vector2(paddingLeft, bottom));
        painter.LineTo(new Vector2(w - paddingRight, bottom));
        painter.Stroke();

        // ── 막대 드로우 ──────────────────────────────────────────────
        for (int hour = 0; hour < 24; hour++)
        {
            float barHeight = _data[hour] > 0
                ? (float)_data[hour] / maxVal * chartH
                : 1f;   // 최소 1px 표시

            float x = paddingLeft + hour * barW + barGap * 0.5f;
            float y = bottom - barHeight;

            painter.fillColor = (hour == _peakHour && _data[hour] > 0)
                ? BarPeak
                : BarNormal;

            painter.BeginPath();
            painter.MoveTo(new Vector2(x,           bottom));
            painter.LineTo(new Vector2(x,           y));
            painter.LineTo(new Vector2(x + barW - barGap, y));
            painter.LineTo(new Vector2(x + barW - barGap, bottom));
            painter.ClosePath();
            painter.Fill();
        }

        // ── X축 레이블 (0, 6, 12, 18, 24) ───────────────────────────
        // painter2D는 텍스트를 지원하지 않으므로
        // 레이블은 UXML의 Label 엘리먼트로 별도 처리 (AnalyticsPanel.cs 참조)
    }
}
```

---

## 10-7. AnalyticsPanel.uxml

`Assets/UI/UXML/AnalyticsPanel.uxml` 을 생성해줘:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <Style src="project://database/Assets/UI/USS/AnalyticsTheme.uss"/>

  <ui:VisualElement name="analytics-panel" class="analytics-panel">

    <!-- 헤더 -->
    <ui:VisualElement class="analytics-header">
      <ui:Label class="analytics-title" text="비누 사용량 분석"/>
      <ui:Button name="btn-close-analytics" class="analytics-close" text="✕"/>
    </ui:VisualElement>

    <!-- 피크 시간대 -->
    <ui:VisualElement class="peak-row">
      <ui:Label class="peak-label" text="가장 많은 사용 시간대:"/>
      <ui:Label name="peak-time-label" class="peak-value" text="--:-- - --:--"/>
    </ui:VisualElement>

    <!-- 막대 그래프 -->
    <ui:VisualElement class="chart-container">
      <BarChartElement name="bar-chart" class="bar-chart"/>
      <!-- X축 레이블 -->
      <ui:VisualElement class="x-axis-labels">
        <ui:Label class="axis-label" text="0"/>
        <ui:Label class="axis-label" text="6"/>
        <ui:Label class="axis-label" text="12"/>
        <ui:Label class="axis-label" text="18"/>
        <ui:Label class="axis-label" text="24"/>
      </ui:VisualElement>
    </ui:VisualElement>

    <!-- 통계 요약 -->
    <ui:VisualElement class="stats-row">
      <ui:Label name="avg-compare-label" class="stats-text"
                text="전체 평균 대비 --% 사용량"/>
    </ui:VisualElement>

  </ui:VisualElement>
</ui:UXML>
```

---

## 10-8. AlarmBanner.uxml

`Assets/UI/UXML/AlarmBanner.uxml` 을 생성해줘:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
  <Style src="project://database/Assets/UI/USS/AnalyticsTheme.uss"/>

  <ui:VisualElement name="alarm-banner" class="alarm-banner alarm-hidden">
    <ui:Label class="alarm-icon" text="⚠"/>
    <ui:Label name="alarm-message" class="alarm-message" text=""/>
    <ui:Label name="alarm-detail" class="alarm-detail" text=""/>
  </ui:VisualElement>
</ui:UXML>
```

---

## 10-9. AnalyticsTheme.uss

`Assets/UI/USS/AnalyticsTheme.uss` 를 생성해줘:

```css
/* ── 분석 패널 ── */
.analytics-panel {
    width: 320px;
    background-color: #0D1F2D;
    border-radius: 12px;
    border-width: 1px;
    border-color: #00E676;
    padding: 14px;
    position: absolute;
    top: 64px;
    right: 16px;
}

.analytics-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 10px;
}

.analytics-title {
    color: #E0E0E0;
    font-size: 15px;
    -unity-font-style: bold;
}

.analytics-close {
    width: 24px;
    height: 24px;
    background-color: transparent;
    border-width: 0;
    color: #78909C;
    font-size: 14px;
    padding: 0;
    cursor: hand;
}

/* ── 피크 시간대 ── */
.peak-row {
    flex-direction: row;
    align-items: center;
    margin-bottom: 8px;
    flex-wrap: wrap;
}

.peak-label {
    color: #B0BEC5;
    font-size: 12px;
    margin-right: 6px;
}

.peak-value {
    color: #FF9100;
    font-size: 14px;
    -unity-font-style: bold;
}

/* ── 그래프 영역 ── */
.chart-container {
    height: 100px;
    margin-bottom: 4px;
}

.bar-chart {
    flex-grow: 1;
    height: 80px;
}

.x-axis-labels {
    flex-direction: row;
    justify-content: space-between;
    height: 16px;
    padding: 0 8px;
}

.axis-label {
    color: #546E7A;
    font-size: 10px;
}

/* ── 통계 요약 ── */
.stats-row {
    margin-top: 6px;
    align-items: center;
}

.stats-text {
    color: #B0BEC5;
    font-size: 12px;
}

.stats-highlight {
    color: #00E676;
    -unity-font-style: bold;
}

/* ── 알람 배너 ── */
.alarm-banner {
    flex-direction: row;
    align-items: center;
    padding: 10px 16px;
    border-radius: 8px;
    margin: 4px 8px;
}

.alarm-banner.alarm-warn {
    background-color: #2D1F00;
    border-width: 1px;
    border-color: #FF9100;
}

.alarm-banner.alarm-alert {
    background-color: #2D0000;
    border-width: 1px;
    border-color: #FF1744;
}

.alarm-hidden {
    display: none;
}

.alarm-icon {
    font-size: 18px;
    margin-right: 10px;
    color: #FF9100;
}

.alarm-banner.alarm-alert .alarm-icon {
    color: #FF1744;
}

.alarm-message {
    color: #E0E0E0;
    font-size: 13px;
    -unity-font-style: bold;
    flex-grow: 1;
}

.alarm-detail {
    color: #78909C;
    font-size: 11px;
    margin-top: 2px;
}
```

---

## 10-10. AnalyticsPanel.cs — 패널 제어

`Assets/Scripts/UI/AnalyticsPanel.cs` 를 생성해줘:

```csharp
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 우측 상단 분석 패널 제어.
/// SoapUsageLogger에서 데이터를 읽어 BarChartElement와 Label을 갱신.
/// </summary>
public class AnalyticsPanel : MonoBehaviour
{
    [Header("References")]
    public UIDocument     uiDocument;
    public SoapUsageLogger logger;
    public StationData    stationData;

    private VisualElement  _panel;
    private BarChartElement _barChart;
    private Label          _peakTimeLabel;
    private Label          _avgCompareLabel;

    private ReplenishResult _lastPrediction;
    private float           _refreshTimer;
    private const float     RefreshInterval = 10f;   // 10초마다 갱신

    void Start()
    {
        var root = uiDocument.rootVisualElement;

        _panel           = root.Q<VisualElement>("analytics-panel");
        _barChart        = root.Q<BarChartElement>("bar-chart");
        _peakTimeLabel   = root.Q<Label>("peak-time-label");
        _avgCompareLabel = root.Q<Label>("avg-compare-label");

        // 닫기 버튼
        root.Q<Button>("btn-close-analytics")
            ?.RegisterCallback<ClickEvent>(_ => _panel.style.display = DisplayStyle.None);

        RefreshChart();
    }

    void Update()
    {
        _refreshTimer += Time.deltaTime;
        if (_refreshTimer >= RefreshInterval)
        {
            _refreshTimer = 0f;
            RefreshChart();
        }
    }

    private void RefreshChart()
    {
        if (logger == null) return;

        // 막대 그래프 갱신
        _barChart?.SetData(logger.HourlyCount, logger.PeakHour);

        // 피크 시간대 텍스트
        if (_peakTimeLabel != null)
        {
            int peak  = logger.PeakHour;
            _peakTimeLabel.text = logger.TodayTotal > 0
                ? $"{peak:D2}:00 - {(peak + 1) % 24:D2}:00"
                : "데이터 없음";
        }

        // 전체 평균 대비 %
        if (_avgCompareLabel != null)
        {
            float overall = logger.HourlyCount.Where(v => v > 0).DefaultIfEmpty(1).Average();
            float peak    = logger.PeakCount > 0 ? logger.PeakCount : 1f;
            int   pct     = Mathf.RoundToInt(peak / overall * 100f);
            _avgCompareLabel.text = logger.TodayTotal > 0
                ? $"전체 평균 대비 {pct}% 사용량"
                : "데이터 수집 중...";
        }

        // 교체 예측 갱신
        RefreshPrediction();
    }

    private void RefreshPrediction()
    {
        if (logger == null || stationData == null) return;

        var dailyUsage = logger.GetDailyUsage(logger.Config.prediction.lookbackDays);
        _lastPrediction = ReplenishPredictor.Predict(
            stationData.soapLevel,
            dailyUsage,
            logger.Config.prediction,
            logger.Config.soapDecreasePerUse
        );
    }

    public ReplenishResult GetLastPrediction() => _lastPrediction;
}
```

---

## 10-11. AlarmBanner.cs — 교체 예측 배너 제어

`Assets/Scripts/UI/AlarmBanner.cs` 를 생성해줘:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// 비누 교체 예측 배너.
/// AnalyticsPanel의 예측 결과를 10초마다 확인해 배너를 표시/숨김.
/// </summary>
public class AlarmBanner : MonoBehaviour
{
    [Header("References")]
    public UIDocument    uiDocument;
    public AnalyticsPanel analyticsPanel;

    private VisualElement _banner;
    private Label         _message;
    private Label         _detail;

    private float         _checkTimer;
    private const float   CheckInterval = 10f;
    private AlarmLevel    _currentLevel = AlarmLevel.None;

    void Start()
    {
        var root = uiDocument.rootVisualElement;
        _banner  = root.Q<VisualElement>("alarm-banner");
        _message = root.Q<Label>("alarm-message");
        _detail  = root.Q<Label>("alarm-detail");

        UpdateBanner();
    }

    void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer >= CheckInterval)
        {
            _checkTimer = 0f;
            UpdateBanner();
        }
    }

    private void UpdateBanner()
    {
        var prediction = analyticsPanel?.GetLastPrediction();
        if (prediction == null || _banner == null) return;

        if (prediction.Level == AlarmLevel.None)
        {
            HideBanner();
            return;
        }

        // 레벨 변경 시에만 클래스 교체 (불필요한 재드로우 방지)
        if (prediction.Level != _currentLevel)
        {
            _banner.RemoveFromClassList("alarm-warn");
            _banner.RemoveFromClassList("alarm-alert");
            _banner.AddToClassList(prediction.Level == AlarmLevel.Alert
                ? "alarm-alert"
                : "alarm-warn");
            _currentLevel = prediction.Level;
        }

        _banner.RemoveFromClassList("alarm-hidden");
        if (_message != null) _message.text = prediction.Message;
        if (_detail  != null)
            _detail.text = $"일평균 {prediction.DailyUsageAvg:F1}회 사용 기준";
    }

    private void HideBanner()
    {
        _banner?.AddToClassList("alarm-hidden");
        _currentLevel = AlarmLevel.None;
    }
}
```

---

## 10-12. 기존 파일 수정

### StationData.cs 수정 — hourlyUsageCount 추가

기존 `StationData.cs` 에 아래 필드를 추가해줘:

```csharp
[Header("Analytics")]
public int[] hourlyUsageCount = new int[24];   // 시간대별 누적 (런타임 캐시용)
```

### StationController.cs 수정 — 로거 호출 추가

기존 `ActivateSoap()` 메서드 첫 줄에 아래를 추가해줘:

```csharp
public void ActivateSoap()
{
    if (stationData.isSoapRunning || stationData.soapLevel <= 0f) return;

    // ── 추가: 사용 전 잔량 기록 ──────────────────────────────────
    float levelBefore = stationData.soapLevel;

    if (_soapCoroutine != null) StopCoroutine(_soapCoroutine);
    _soapCoroutine = StartCoroutine(RunDispenser(
        setter:   v => stationData.isSoapRunning = v,
        duration: 3f,
        particle: soapParticle,
        onStart:  () =>
        {
            stationData.UseSoap();
            // ── 추가: 로거에 이벤트 기록 ─────────────────────────
            SoapUsageLogger.Instance?.LogSoap(levelBefore, stationData.soapLevel);
            OnSoapUpdated?.Invoke();
        },
        onEnd: () => OnSoapUpdated?.Invoke()
    ));
}
```

`ActivateWater()` 와 `ActivateAir()` 에도 각각 추가해줘:

```csharp
// ActivateWater onStart 람다 안에 추가
SoapUsageLogger.Instance?.LogWater();

// ActivateAir onStart 람다 안에 추가
SoapUsageLogger.Instance?.LogAir();
```

### MainHMI.uxml 수정 — 패널 및 배너 삽입

기존 `MainHMI.uxml` 의 `content-area` VisualElement 안에 아래를 추가해줘:

```xml
<!-- 우측 상단 분석 패널 (절대 위치) -->
<ui:VisualElement name="analytics-panel" class="analytics-panel">
  <!-- AnalyticsPanel.uxml 내용을 여기에 인라인으로 포함 -->
  <ui:VisualElement class="analytics-header">
    <ui:Label class="analytics-title" text="📊 비누 사용량 분석"/>
    <ui:Button name="btn-close-analytics" class="analytics-close" text="✕"/>
  </ui:VisualElement>
  <ui:VisualElement class="peak-row">
    <ui:Label class="peak-label" text="가장 많은 사용 시간대:"/>
    <ui:Label name="peak-time-label" class="peak-value" text="--:-- - --:--"/>
  </ui:VisualElement>
  <ui:VisualElement class="chart-container">
    <BarChartElement name="bar-chart" class="bar-chart"/>
    <ui:VisualElement class="x-axis-labels">
      <ui:Label class="axis-label" text="0"/>
      <ui:Label class="axis-label" text="6"/>
      <ui:Label class="axis-label" text="12"/>
      <ui:Label class="axis-label" text="18"/>
      <ui:Label class="axis-label" text="24"/>
    </ui:VisualElement>
  </ui:VisualElement>
  <ui:VisualElement class="stats-row">
    <ui:Label name="avg-compare-label" class="stats-text" text="데이터 수집 중..."/>
  </ui:VisualElement>
</ui:VisualElement>

<!-- 교체 예측 알람 배너 (content-area 상단) -->
<ui:VisualElement name="alarm-banner" class="alarm-banner alarm-hidden">
  <ui:Label class="alarm-icon" text="⚠"/>
  <ui:VisualElement class="alarm-text-col">
    <ui:Label name="alarm-message" class="alarm-message" text=""/>
    <ui:Label name="alarm-detail"  class="alarm-detail"  text=""/>
  </ui:VisualElement>
</ui:VisualElement>
```

`MainTheme.uss` 에도 아래 import를 추가해줘:

```css
@import url("project://database/Assets/UI/USS/AnalyticsTheme.uss");
```

---

## 10-13. 씬 조립

1. `StationManager` GameObject에 `SoapUsageLogger.cs` 컴포넌트 추가
2. `StationManager` GameObject에 `APIClient.cs` 컴포넌트 추가
   - `Logger` → `StationManager`의 SoapUsageLogger 연결
3. `UIRoot` GameObject에 `AnalyticsPanel.cs` 컴포넌트 추가
   - `UI Document` → UIDocument 연결
   - `Logger` → SoapUsageLogger 연결
   - `Station Data` → StationDataInstance.asset 연결
4. `UIRoot` GameObject에 `AlarmBanner.cs` 컴포넌트 추가
   - `UI Document` → UIDocument 연결
   - `Analytics Panel` → AnalyticsPanel 컴포넌트 연결
5. `BarChartElement` 커스텀 엘리먼트가 UXML에서 인식되려면
   Unity Editor 재시작 또는 **Assets → Reimport All** 실행

---

## 10-14. 저장 경로 및 API 연동 구조

| 항목 | 경로 / 설명 |
|---|---|
| 로컬 이벤트 큐 | `Application.persistentDataPath/usage_log.json` |
| 설정 파일 | `Assets/Resources/AnalyticsConfig.json` |
| API 엔드포인트 | `POST {apiBaseUrl}/events` — 배치 전송 |
| 전송 주기 | 30초마다 미전송 이벤트 최대 50개 배치 전송 |
| 오프라인 동작 | 서버 없어도 로컬 JSON 완전 동작, 연결 시 자동 동기화 |

### API 서버 완성 후 전환 방법

`AnalyticsConfig.json` 수정만으로 완료 — Unity 코드 수정 없음:

```json
{
  "apiBaseUrl": "http://192.168.1.100:8000/api",
  "apiEnabled": true
}
```

---

## 동작 확인 체크리스트

| 항목 | 확인 내용 |
|---|---|
| 비누 버튼 클릭 | `usage_log.json` 에 이벤트 기록 확인 |
| 그래프 패널 | 시간대별 막대 표시, 피크 막대 주황색 |
| 피크 시간대 | 가장 많이 사용한 시간 텍스트 표시 |
| 교체 예측 | 잔량 낮아질수록 배너 주황 → 빨강 전환 |
| 배너 텍스트 | `D-N 비누 교체 필요` 메시지 표시 |
| 오프라인 동작 | 서버 없어도 Console 오류 없이 정상 동작 |
| 앱 재시작 | 재실행 후 이전 사용 데이터 복원 확인 |
| Mock 테스트 | NetworkManager ContextMenu → 비누 사용 반복 → 그래프 갱신 확인 |
