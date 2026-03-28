# SoapUsageLogger.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Analytics/SoapUsageLogger.cs`
- **목적**: 비누/물/에어 사용 이벤트를 로컬 JSON 파일에 저장하고 시간대별 집계를 관리
- **패턴**: 싱글톤 (Singleton)

## 핵심 기능

| 기능 | 메서드 | 설명 |
|------|--------|------|
| 이벤트 기록 | `LogSoap()`, `LogWater()`, `LogAir()` | 사용 이벤트를 큐에 추가 |
| 로컬 저장 | `SaveQueue()`, `LoadQueue()` | JSON 파일로 영속화 |
| 시간대 집계 | `RebuildHourlyCache()` | 0~23시별 사용 횟수 집계 |
| 전송 관리 | `MarkTransmitted()`, `GetPendingEvents()` | API 전송 상태 관리 |
| 예측 지원 | `GetDailyUsage()` | 일별 사용 횟수 반환 |

---

## 코드 분석

### Line 13-16: 상수 정의

```csharp
private const int HoursPerDay = 24;
private const string LogFileName = "usage_log.json";
private const string SoapEventType = "soap_dispensed";
private const string DateFormat = "yyyy-MM-dd";
```

| 상수 | 값 | 용도 |
|------|-----|------|
| `HoursPerDay` | 24 | 시간대 배열 크기 |
| `LogFileName` | "usage_log.json" | 로컬 저장 파일명 |
| `SoapEventType` | "soap_dispensed" | 비누 이벤트 타입 식별자 |
| `DateFormat` | "yyyy-MM-dd" | 날짜 문자열 포맷 |

---

### Line 18-19: 싱글톤 패턴

```csharp
private static SoapUsageLogger _instance;
public  static SoapUsageLogger Instance => _instance;
```

- `_instance`: 유일한 인스턴스를 저장하는 정적 필드
- `Instance`: 외부에서 접근하는 읽기 전용 프로퍼티
- `Awake()`에서 중복 인스턴스 방지

**싱글톤 사용 이유**: 여러 스크립트에서 동일한 로거 인스턴스에 접근 필요

---

### Line 21-34: 필드 및 프로퍼티

```csharp
[Header("References")]
[Tooltip("StationData와 hourlyUsageCount 동기화용 (선택)")]
public StationData stationData;

private AnalyticsConfig _config;
private UsageEventQueue _queue;
private string          _savePath;

private int[] _hourlyCount = new int[HoursPerDay];

public int[]   HourlyCount  => _hourlyCount;
public int     TodayTotal   => _hourlyCount.Sum();
public int     PeakHour     { get; private set; }
public int     PeakCount    { get; private set; }
```

| 필드/프로퍼티 | 타입 | 설명 |
|---------------|------|------|
| `stationData` | `StationData` | UI 동기화용 ScriptableObject |
| `_config` | `AnalyticsConfig` | API/저장 설정 |
| `_queue` | `UsageEventQueue` | 이벤트 목록 컨테이너 |
| `_savePath` | `string` | JSON 파일 전체 경로 |
| `_hourlyCount` | `int[24]` | 시간대별 사용 횟수 |
| `TodayTotal` | `int` | 오늘 총 사용 횟수 (LINQ Sum) |
| `PeakHour` | `int` | 가장 많이 사용한 시간 (0-23) |
| `PeakCount` | `int` | 피크 시간의 사용 횟수 |

---

### Line 36-49: Awake() — 초기화

```csharp
void Awake()
{
    if (_instance != null)
    {
        Destroy(gameObject);
        return;
    }
    _instance = this;

    LoadConfig();
    LoadQueue();
    RebuildHourlyCache();
    SyncToStationData();
}
```

**싱글톤 보장 흐름**:
1. 이미 인스턴스가 있으면 자신을 파괴
2. 없으면 자신을 인스턴스로 등록
3. 설정/큐 로드 → 캐시 재구성 → UI 동기화

---

### Line 51-57: LoadConfig() — 설정 로드

```csharp
private void LoadConfig()
{
    var json = Resources.Load<TextAsset>("AnalyticsConfig");
    _config = json != null
        ? JsonUtility.FromJson<AnalyticsConfig>(json.text)
        : new AnalyticsConfig();
}
```

- `Resources.Load<TextAsset>()`: Resources 폴더에서 JSON 파일 로드
- 파일이 없으면 기본 설정 객체 생성
- 경로: `Assets/Resources/AnalyticsConfig.json`

---

### Line 59-78: LoadQueue() — 큐 로드

```csharp
private void LoadQueue()
{
    _savePath = Path.Combine(Application.persistentDataPath, LogFileName);

    if (!File.Exists(_savePath))
    {
        _queue = new UsageEventQueue();
        return;
    }

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
```

**Early Return 패턴**:
1. 파일이 없으면 빈 큐 생성 후 즉시 반환
2. 파일이 있으면 JSON 파싱 시도
3. 실패 시 빈 큐로 폴백

**저장 경로**:
- Windows: `C:\Users\{user}\AppData\LocalLow\{company}\{product}\usage_log.json`
- Android: `/data/data/{package}/files/usage_log.json`

---

### Line 80-90: SaveQueue() — 큐 저장

```csharp
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
```

- `JsonUtility.ToJson(_queue, true)`: 두 번째 인자 `true`는 pretty-print
- 디스크 오류 시 경고 로그만 출력 (앱 중단 방지)

---

### Line 92-111: 이벤트 기록 메서드

```csharp
public void LogSoap(float levelBefore, float levelAfter)
{
    var ev = UsageEvent.CreateSoapEvent(_config.stationId, levelBefore, levelAfter);
    Enqueue(ev);

    int hour = DateTime.Now.Hour;
    _hourlyCount[hour]++;
    UpdatePeak();
    SyncToStationData();
}

public void LogWater() { ... }
public void LogAir()   { ... }
```

**LogSoap 추가 작업**:
1. 이벤트 생성 및 큐에 추가
2. 현재 시간대 카운트 증가
3. 피크 시간 재계산
4. StationData에 동기화 (UI 갱신용)

**LogWater/LogAir**:
- 시간대 집계 없이 이벤트만 기록

---

### Line 113-128: Enqueue() — 큐 관리

```csharp
private void Enqueue(UsageEvent ev)
{
    _queue.events.Add(ev);

    if (_queue.events.Count > _config.maxQueueSize)
    {
        _queue.events.RemoveAll(e => e.transmitted);

        while (_queue.events.Count > _config.maxQueueSize)
        {
            _queue.events.RemoveAt(0);
        }
    }

    SaveQueue();
}
```

**큐 크기 제한 알고리즘**:
1. 이벤트 추가
2. 최대 크기 초과 시:
   - 먼저 전송 완료된 것 일괄 제거 (`RemoveAll`)
   - 그래도 초과면 가장 오래된 것부터 제거 (`RemoveAt(0)`)
3. 매번 디스크에 저장

---

### Line 130-140: MarkTransmitted() — 전송 완료 마킹

```csharp
public void MarkTransmitted(List<string> eventIds)
{
    foreach (var ev in _queue.events)
    {
        if (eventIds.Contains(ev.eventId))
        {
            ev.transmitted = true;
        }
    }
    SaveQueue();
}
```

- `APIClient`에서 전송 성공 후 호출
- 전송된 이벤트 ID 목록을 받아 `transmitted = true` 마킹
- 마킹된 이벤트는 재전송하지 않음

---

### Line 142-148: GetPendingEvents() — 미전송 이벤트 조회

```csharp
public List<UsageEvent> GetPendingEvents(int limit)
{
    return _queue.events
        .Where(e => !e.transmitted)
        .Take(limit)
        .ToList();
}
```

**LINQ 체이닝**:
1. `.Where()`: 미전송 이벤트만 필터
2. `.Take()`: 최대 개수 제한
3. `.ToList()`: List로 변환

---

### Line 150-172: RebuildHourlyCache() — 시간대 캐시 재구성

```csharp
private void RebuildHourlyCache()
{
    _hourlyCount = new int[HoursPerDay];
    string today = DateTime.Now.ToString(DateFormat);

    foreach (var ev in _queue.events)
    {
        if (ev.type != SoapEventType) continue;
        if (!ev.timestamp.StartsWith(today)) continue;

        if (DateTime.TryParse(ev.timestamp, out DateTime dt))
            _hourlyCount[dt.Hour]++;
    }
    UpdatePeak();
}
```

**필터링 조건**:
1. `soap_dispensed` 타입만 (물/에어 제외)
2. 오늘 날짜만 (어제 데이터 제외)

**용도**: 앱 재시작 시 오늘의 시간대별 통계 복원

---

### Line 174-187: UpdatePeak() — 피크 시간 계산

```csharp
private void UpdatePeak()
{
    PeakCount = 0;
    PeakHour  = 0;

    for (int h = 0; h < HoursPerDay; h++)
    {
        if (_hourlyCount[h] > PeakCount)
        {
            PeakCount = _hourlyCount[h];
            PeakHour  = h;
        }
    }
}
```

- 24시간 순회하며 최대값과 해당 시간 찾기
- `AnalyticsPanel`에서 "가장 많은 사용 시간대" 표시에 사용

---

### Line 189-202: GetDailyUsage() — 일별 사용량 조회

```csharp
public Dictionary<string, int> GetDailyUsage(int days)
{
    var result = new Dictionary<string, int>();

    for (int i = 0; i < days; i++)
    {
        string date = DateTime.Now.AddDays(-i).ToString(DateFormat);
        result[date] = _queue.events
            .Count(e => e.type == SoapEventType && e.timestamp.StartsWith(date));
    }

    return result;
}
```

- `ReplenishPredictor`에서 비누 교체 예측에 사용
- 반환 예: `{"2024-01-15": 25, "2024-01-14": 18, ...}`

---

### Line 206-217: SyncToStationData() — UI 동기화

```csharp
private void SyncToStationData()
{
    if (stationData == null) return;

    for (int i = 0; i < HoursPerDay; i++)
    {
        stationData.hourlyUsageCount[i] = _hourlyCount[i];
    }
}
```

- `StationData` ScriptableObject의 배열에 복사
- UI에서 `StationData`를 참조하여 실시간 갱신

---

## 데이터 흐름

```
[사용자 동작]
     ↓
LogSoap() / LogWater() / LogAir()
     ↓
Enqueue() → _queue.events.Add()
     ↓
SaveQueue() → usage_log.json 저장
     ↓
[30초마다]
     ↓
APIClient.GetPendingEvents()
     ↓
HTTP POST 전송
     ↓
MarkTransmitted() → transmitted = true
```

## 파일 구조

```
persistentDataPath/
└── usage_log.json
    {
      "events": [
        {
          "eventId": "uuid",
          "stationId": "station-01",
          "type": "soap_dispensed",
          "timestamp": "2024-01-15T14:30:00",
          "levelBefore": 80.0,
          "levelAfter": 79.5,
          "transmitted": false
        },
        ...
      ]
    }
```
