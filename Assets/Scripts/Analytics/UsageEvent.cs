using System;

/// <summary>
/// API 서버 전송 스키마와 동일한 구조.
/// 로컬 JSON 저장 → HTTP POST 전송 시 이 클래스를 그대로 직렬화.
/// </summary>
[Serializable]
public class UsageEvent
{
    public const string TypeSoap  = "soap_dispensed";
    public const string TypeWater = "water_on";
    public const string TypeAir   = "air_on";

    private const string TimestampFormat = "yyyy-MM-ddTHH:mm:ssZ";

    public string eventId;
    public string stationId;
    public string timestamp;
    public string type;
    public float  soapLevelBefore;
    public float  soapLevelAfter;
    public bool   transmitted;

    public static UsageEvent CreateSoapEvent(string stationId, float before, float after)
    {
        var ev = CreateBase(stationId, TypeSoap);
        ev.soapLevelBefore = before;
        ev.soapLevelAfter  = after;
        return ev;
    }

    public static UsageEvent CreateWaterEvent(string stationId)
    {
        return CreateBase(stationId, TypeWater);
    }

    public static UsageEvent CreateAirEvent(string stationId)
    {
        return CreateBase(stationId, TypeAir);
    }

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
}

/// <summary>JsonUtility가 List를 직접 직렬화하지 못해 래퍼 클래스 필요</summary>
[Serializable]
public class UsageEventQueue
{
    public System.Collections.Generic.List<UsageEvent> events
        = new System.Collections.Generic.List<UsageEvent>();
}

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

[Serializable]
public class PredictionConfig
{
    public int lookbackDays = 7;
    public int warnDays     = 7;
    public int alertDays    = 3;
}
