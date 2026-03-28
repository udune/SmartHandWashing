# UsageEvent.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Analytics/UsageEvent.cs`
- **목적**: 비누/물/에어 사용 이벤트 데이터 구조 및 설정 클래스 정의

## 파일 구조

이 파일은 4개의 클래스를 정의합니다:

| 클래스 | 역할 |
|--------|------|
| `UsageEvent` | 개별 사용 이벤트 데이터 |
| `UsageEventQueue` | 이벤트 목록 컨테이너 (JSON 직렬화용) |
| `AnalyticsConfig` | API/저장 설정 |
| `PredictionConfig` | 교체 예측 알람 설정 |

---

## 코드 분석

### UsageEvent 클래스

#### 상수 정의 (Line 10-14)

```csharp
public const string TypeSoap  = "soap_dispensed";
public const string TypeWater = "water_on";
public const string TypeAir   = "air_on";

private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";
```

| 상수 | 값 | 용도 |
|------|-----|------|
| `TypeSoap` | "soap_dispensed" | 비누 사용 이벤트 타입 |
| `TypeWater` | "water_on" | 물 사용 이벤트 타입 |
| `TypeAir` | "air_on" | 에어 드라이 이벤트 타입 |
| `TimestampFormat` | ISO 8601 형식 | UTC 타임스탬프 포맷 |

---

#### 필드 정의 (Line 16-22)

```csharp
public string eventId;
public string stationId;
public string timestamp;
public string type;
public float  soapLevelBefore;
public float  soapLevelAfter;
public bool   transmitted;
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `eventId` | `string` | GUID — 이벤트 고유 식별자 (중복 방지) |
| `stationId` | `string` | 스테이션 식별자 (설정에서 가져옴) |
| `timestamp` | `string` | ISO 8601 형식 UTC 시간 |
| `type` | `string` | 이벤트 타입 (TypeSoap/TypeWater/TypeAir) |
| `soapLevelBefore` | `float` | 비누 사용 전 잔량 (%) — 비누 이벤트만 |
| `soapLevelAfter` | `float` | 비누 사용 후 잔량 (%) — 비누 이벤트만 |
| `transmitted` | `bool` | API 서버 전송 완료 여부 |

---

#### 팩토리 메서드

**CreateSoapEvent (Line 24-30)**
```csharp
public static UsageEvent CreateSoapEvent(string stationId, float before, float after)
{
    var ev = CreateBase(stationId, TypeSoap);
    ev.soapLevelBefore = before;
    ev.soapLevelAfter  = after;
    return ev;
}
```
- 비누 사용 이벤트 생성
- `soapLevelBefore/After` 추가 설정
- 호출처: `SoapUsageLogger.LogSoap()`

**CreateWaterEvent (Line 32-35)**
```csharp
public static UsageEvent CreateWaterEvent(string stationId)
{
    return CreateBase(stationId, TypeWater);
}
```
- 물 사용 이벤트 생성
- 잔량 정보 없음

**CreateAirEvent (Line 37-40)**
```csharp
public static UsageEvent CreateAirEvent(string stationId)
{
    return CreateBase(stationId, TypeAir);
}
```
- 에어 드라이 이벤트 생성
- 잔량 정보 없음

**CreateBase (Line 42-52)**
```csharp
private static UsageEvent CreateBase(string stationId, string type)
{
    return new UsageEvent
    {
        eventId     = Guid.NewGuid().ToString(),
        stationId   = stationId,
        timestamp   = DateTime.UtcNow.ToString(TimestampFormat),
        type        = type,
        transmitted = false
    };
}
```
- 공통 초기화 로직 추출 (DRY 원칙)
- `Guid.NewGuid()`: 전역 고유 식별자 생성
- `DateTime.UtcNow`: UTC 기준 현재 시간

---

### UsageEventQueue 클래스 (Line 55-60)

```csharp
[Serializable]
public class UsageEventQueue
{
    public System.Collections.Generic.List<UsageEvent> events
        = new System.Collections.Generic.List<UsageEvent>();
}
```

**목적**: `JsonUtility`가 `List<T>`를 직접 직렬화하지 못하기 때문에 래퍼 클래스 사용

**JSON 출력 예시**:
```json
{
  "events": [
    { "eventId": "...", "type": "soap_dispensed", ... },
    { "eventId": "...", "type": "water_on", ... }
  ]
}
```

---

### AnalyticsConfig 클래스 (Line 62-74)

```csharp
[Serializable]
public class AnalyticsConfig
{
    public string stationId      = "station-01";
    public string stationName    = "스마트 손 씻기 스테이션";
    public string apiBaseUrl     = "";
    public bool   apiEnabled     = false;
    public int    apiTimeoutMs   = 5000;
    public int    maxQueueSize   = 1000;
    public int    batchSendSize  = 50;
    public float  soapDecreasePerUse = 5f;
    public PredictionConfig prediction = new PredictionConfig();
}
```

| 필드 | 기본값 | 설명 |
|------|--------|------|
| `stationId` | "station-01" | 스테이션 식별자 |
| `stationName` | "스마트 손 씻기 스테이션" | 표시명 |
| `apiBaseUrl` | "" | API 서버 기본 URL |
| `apiEnabled` | false | API 전송 활성화 여부 |
| `apiTimeoutMs` | 5000 | HTTP 요청 타임아웃 (ms) |
| `maxQueueSize` | 1000 | 로컬 큐 최대 크기 |
| `batchSendSize` | 50 | 한 번에 전송할 이벤트 수 |
| `soapDecreasePerUse` | 5f | 비누 1회 사용당 감소량 (%) |
| `prediction` | PredictionConfig | 예측 관련 설정 |

**로드 경로**: `Assets/Resources/AnalyticsConfig.json`

---

### PredictionConfig 클래스 (Line 76-82)

```csharp
[Serializable]
public class PredictionConfig
{
    public int lookbackDays = 7;
    public int warnDays     = 7;
    public int alertDays    = 3;
}
```

| 필드 | 기본값 | 설명 |
|------|--------|------|
| `lookbackDays` | 7 | 예측에 사용할 과거 일수 |
| `warnDays` | 7 | 주의 알람 임계값 (잔여일 ≤ 7일) |
| `alertDays` | 3 | 긴급 알람 임계값 (잔여일 ≤ 3일) |

---

## 직렬화 구조

### [Serializable] 어트리뷰트

```csharp
[Serializable]
public class UsageEvent { ... }
```

- Unity의 `JsonUtility`가 직렬화/역직렬화할 수 있도록 표시
- `public` 필드만 직렬화됨 (프로퍼티 X)
- 중첩 클래스도 `[Serializable]` 필요

### JSON 예시

**AnalyticsConfig.json**:
```json
{
  "stationId": "station-01",
  "stationName": "스마트 손 씻기 스테이션",
  "apiBaseUrl": "https://api.example.com",
  "apiEnabled": true,
  "apiTimeoutMs": 5000,
  "maxQueueSize": 1000,
  "batchSendSize": 50,
  "soapDecreasePerUse": 5.0,
  "prediction": {
    "lookbackDays": 7,
    "warnDays": 7,
    "alertDays": 3
  }
}
```

**UsageEvent**:
```json
{
  "eventId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "stationId": "station-01",
  "timestamp": "2024-01-15T14:30:00Z",
  "type": "soap_dispensed",
  "soapLevelBefore": 80.0,
  "soapLevelAfter": 75.0,
  "transmitted": false
}
```

---

## 사용 관계

```
SoapUsageLogger
    ├── UsageEvent.CreateSoapEvent()
    ├── UsageEvent.CreateWaterEvent()
    ├── UsageEvent.CreateAirEvent()
    ├── UsageEventQueue (로컬 저장)
    └── AnalyticsConfig (설정 로드)

ReplenishPredictor
    └── PredictionConfig (알람 임계값)

APIClient
    ├── AnalyticsConfig (API 설정)
    └── UsageEventQueue (전송 페이로드)
```
