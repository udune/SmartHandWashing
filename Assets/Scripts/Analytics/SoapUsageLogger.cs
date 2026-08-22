using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 이벤트를 로컬 JSON에 저장하고 오프라인 큐를 관리.
/// APIClient가 전송에 성공하면 transmitted = true 로 마킹.
/// </summary>
public class SoapUsageLogger : MonoBehaviour
{
    private const int HoursPerDay = 24;
    private const string LogFileName = "usage_log.json";
    private const string SoapEventType = "soap_dispensed";
    private const string DateFormat = "yyyy-MM-dd";

    // 자정을 넘겨 연속 가동할 때 _hourlyCount를 비우기 위한 감시 주기.
    private const float DateCheckIntervalSec = 30f;

    private static SoapUsageLogger _instance;

    public static SoapUsageLogger Instance
    {
        get { return _instance; }
    }

    [Header("References")]
    [Tooltip("StationData와 hourlyUsageCount 동기화용 (선택)")]
    public StationData stationData;

    private AnalyticsConfig _config;
    private UsageEventQueue _queue;
    private string          _savePath;

    private int[] _hourlyCount = new int[HoursPerDay];

    // _hourlyCount가 담고 있는 로컬 날짜. 이 값이 오늘과 다르면 캐시가 어제 것이다.
    private DateTime _cacheDate;

    public int[] HourlyCount
    {
        get { return _hourlyCount; }
    }

    public int TodayTotal
    {
        get { return _hourlyCount.Sum(); }
    }

    public int PeakHour  { get; private set; }
    public int PeakCount { get; private set; }

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

        // 중복 인스턴스 가드를 통과한 경우에만 감시를 시작한다.
        StartCoroutine(DateRolloverWatcher());
    }

    /// <summary>
    /// 사용이 없는 채로 자정을 넘기면 이벤트 경로가 돌지 않아 캐시가 어제 값에 머문다.
    /// 주기적으로 날짜를 확인해 그래프를 비운다.
    /// </summary>
    private IEnumerator DateRolloverWatcher()
    {
        var wait = new WaitForSeconds(DateCheckIntervalSec);
        while (true)
        {
            yield return wait;
            RefreshIfDateChanged(DateTime.Now);
        }
    }

    private void RefreshIfDateChanged(DateTime now)
    {
        if (now.Date == _cacheDate)
        {
            return;
        }
        RebuildHourlyCache();
        SyncToStationData();
    }

    private void LoadConfig()
    {
        var json = Resources.Load<TextAsset>("AnalyticsConfig");
        _config = json != null
            ? JsonUtility.FromJson<AnalyticsConfig>(json.text)
            : new AnalyticsConfig();
    }

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

    public void LogSoap(float levelBefore, float levelAfter)
    {
        // 시각을 한 번만 읽어 날짜 판정과 시(hour) 버킷이 같은 순간을 가리키게 한다.
        DateTime now = DateTime.Now;

        // Enqueue 전에 확인해야 한다 — 재집계가 방금 넣은 이벤트까지 세면 이중 카운트된다.
        RefreshIfDateChanged(now);

        var ev = UsageEvent.CreateSoapEvent(_config.stationId, levelBefore, levelAfter);
        Enqueue(ev);

        _hourlyCount[now.Hour]++;
        UpdatePeak();
        SyncToStationData();
    }

    public void LogWater()
    {
        Enqueue(UsageEvent.CreateWaterEvent(_config.stationId));
    }

    public void LogAir()
    {
        Enqueue(UsageEvent.CreateAirEvent(_config.stationId));
    }

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

    public List<UsageEvent> GetPendingEvents(int limit)
    {
        return _queue.events
            .Where(e => !e.transmitted)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// timestamp(UTC ISO 8601) → 로컬 시각.
    /// 날짜/시 버킷팅은 반드시 이 헬퍼를 거친다 — 문자열 앞자리 비교는 UTC 날짜를
    /// 로컬 날짜와 맞대어 KST 기준 00:00~09:00 이벤트를 통째로 누락시킨다.
    /// </summary>
    private static bool TryGetLocalTime(UsageEvent ev, out DateTime local)
    {
        local = default;

        if (ev == null || string.IsNullOrEmpty(ev.timestamp))
        {
            return false;
        }

        if (!DateTime.TryParse(ev.timestamp, CultureInfo.InvariantCulture,
                               DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            return false;
        }

        // 오프셋 표기가 없는 타임스탬프는 UTC로 간주한다 (저장 포맷이 항상 UTC이므로).
        if (parsed.Kind == DateTimeKind.Unspecified)
        {
            parsed = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        local = parsed.ToLocalTime();
        return true;
    }

    private void RebuildHourlyCache()
    {
        _hourlyCount = new int[HoursPerDay];

        DateTime today = DateTime.Now.Date;
        _cacheDate = today;

        foreach (var ev in _queue.events)
        {
            if (ev.type != SoapEventType)
            {
                continue;
            }
            if (!TryGetLocalTime(ev, out DateTime local))
            {
                continue;
            }
            if (local.Date != today)
            {
                continue;
            }

            _hourlyCount[local.Hour]++;
        }
        UpdatePeak();
    }

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

    /// <summary>과거 N일간 날짜별 비누 사용 횟수 반환</summary>
    public Dictionary<string, int> GetDailyUsage(int days)
    {
        var result = new Dictionary<string, int>(days);

        DateTime today = DateTime.Now.Date;
        for (int i = 0; i < days; i++)
        {
            result[today.AddDays(-i).ToString(DateFormat)] = 0;
        }

        foreach (var ev in _queue.events)
        {
            if (ev.type != SoapEventType) continue;
            if (!TryGetLocalTime(ev, out DateTime local)) continue;

            string dateKey = local.ToString(DateFormat);
            if (result.ContainsKey(dateKey))
            {
                result[dateKey]++;
            }
        }

        return result;
    }

    public AnalyticsConfig Config
    {
        get { return _config; }
    }

    private void SyncToStationData()
    {
        if (stationData == null)
        {
            return;
        }

        for (int i = 0; i < HoursPerDay; i++)
        {
            stationData.hourlyUsageCount[i] = _hourlyCount[i];
        }
    }
}
