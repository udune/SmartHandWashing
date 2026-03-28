// System: Guid, DateTime, Serializable 등 기본 타입
using System;

/// <summary>
/// API 서버 전송 스키마와 동일한 구조.
/// 로컬 JSON 저장 → HTTP POST 전송 시 이 클래스를 그대로 직렬화.
/// </summary>
// [Serializable]: JsonUtility가 이 클래스를 JSON으로 변환할 수 있도록 표시
[Serializable]
public class UsageEvent
{
    // ── 이벤트 타입 상수 (API 스키마와 동일) ──
    // TypeSoap: 비누 사용 이벤트 식별자
    public const string TypeSoap  = "soap_dispensed";
    // TypeWater: 물 사용 이벤트 식별자
    public const string TypeWater = "water_on";
    // TypeAir: 에어 드라이 이벤트 식별자
    public const string TypeAir   = "air_on";

    // TimestampFormat: ISO 8601 형식 UTC 타임스탬프
    // 예: "2024-01-15T14:30:00Z"
    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";

    // ── 이벤트 필드 (public = JSON 직렬화 대상) ──
    // eventId: GUID 형식 고유 식별자 — 중복 전송 방지
    public string eventId;
    // stationId: 스테이션 식별자 (AnalyticsConfig에서 가져옴)
    public string stationId;
    // timestamp: 이벤트 발생 시간 (UTC)
    public string timestamp;
    // type: 이벤트 타입 (TypeSoap/TypeWater/TypeAir 중 하나)
    public string type;
    // soapLevelBefore: 비누 사용 전 잔량 (%) — 비누 이벤트만 사용
    public float  soapLevelBefore;
    // soapLevelAfter: 비누 사용 후 잔량 (%) — 비누 이벤트만 사용
    public float  soapLevelAfter;
    // transmitted: API 서버 전송 완료 여부 (true면 재전송 안 함)
    public bool   transmitted;

    // ── 팩토리 메서드: 이벤트 생성 ──

    // CreateSoapEvent(): 비누 사용 이벤트 생성
    // stationId: 스테이션 식별자
    // before: 사용 전 비누 잔량 (%)
    // after: 사용 후 비누 잔량 (%)
    public static UsageEvent CreateSoapEvent(string stationId, float before, float after)
    {
        // 공통 필드 초기화 후 비누 전용 필드 추가
        var ev = CreateBase(stationId, TypeSoap);
        ev.soapLevelBefore = before;
        ev.soapLevelAfter  = after;
        return ev;
    }

    // CreateWaterEvent(): 물 사용 이벤트 생성 (잔량 정보 없음)
    public static UsageEvent CreateWaterEvent(string stationId)
    {
        return CreateBase(stationId, TypeWater);
    }

    // CreateAirEvent(): 에어 드라이 이벤트 생성 (잔량 정보 없음)
    public static UsageEvent CreateAirEvent(string stationId)
    {
        return CreateBase(stationId, TypeAir);
    }

    // CreateBase(): 공통 필드 초기화 헬퍼 (DRY 원칙)
    // stationId: 스테이션 식별자
    // type: 이벤트 타입 상수
    private static UsageEvent CreateBase(string stationId, string type)
    {
        // Object Initializer: new 와 동시에 필드 값 설정
        return new UsageEvent
        {
            // Guid.NewGuid(): 전역 고유 식별자 생성 (UUID v4)
            // .ToString(): GUID를 문자열로 변환
            eventId     = Guid.NewGuid().ToString(),
            stationId   = stationId,
            // DateTime.UtcNow: UTC 기준 현재 시간
            // .ToString(format): 지정 형식으로 문자열 변환
            timestamp   = DateTime.UtcNow.ToString(TimestampFormat),
            type        = type,
            // transmitted: 새 이벤트는 아직 전송되지 않음
            transmitted = false
        };
    }
}

// UsageEventQueue: 이벤트 목록 컨테이너
// JsonUtility가 List<T>를 직접 직렬화하지 못해 래퍼 클래스 필요
[Serializable]
public class UsageEventQueue
{
    // events: 이벤트 목록 — JSON의 "events" 배열에 매핑
    public System.Collections.Generic.List<UsageEvent> events
        = new System.Collections.Generic.List<UsageEvent>();
}

// AnalyticsConfig: 분석 시스템 설정
// 로드 경로: Assets/Resources/AnalyticsConfig.json
[Serializable]
public class AnalyticsConfig
{
    // stationId: 스테이션 고유 식별자 (API 전송 시 포함)
    public string stationId      = "station-01";
    // stationName: 표시용 스테이션 이름
    public string stationName    = "스마트 손 씻기 스테이션";
    // apiBaseUrl: API 서버 기본 URL (예: "https://api.example.com")
    public string apiBaseUrl     = "";
    // apiEnabled: false면 로컬 저장만, true면 서버 전송도 수행
    public bool   apiEnabled     = false;
    // apiTimeoutMs: HTTP 요청 타임아웃 (밀리초)
    public int    apiTimeoutMs   = 5000;
    // maxQueueSize: 로컬 큐 최대 크기 — 초과 시 오래된 것 삭제
    public int    maxQueueSize   = 1000;
    // batchSendSize: 한 번에 전송할 이벤트 수
    public int    batchSendSize  = 50;
    // soapDecreasePerUse: 비누 1회 사용당 감소량 (%)
    public float  soapDecreasePerUse = 5f;
    // prediction: 교체 예측 관련 설정 (중첩 객체)
    public PredictionConfig prediction = new PredictionConfig();
}

// PredictionConfig: 비누 교체 예측 알람 설정
[Serializable]
public class PredictionConfig
{
    // lookbackDays: 예측에 사용할 과거 일수 (이동 평균 기간)
    public int lookbackDays = 7;
    // warnDays: 주의 알람 임계값 — 잔여일 ≤ warnDays 이면 Warn
    public int warnDays     = 7;
    // alertDays: 긴급 알람 임계값 — 잔여일 ≤ alertDays 이면 Alert
    public int alertDays    = 3;
}
