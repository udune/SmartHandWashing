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
    private const int HoursPerDay = 24;
    private const string LogFileName = "usage_log.json";
    private const string SoapEventType = "soap_dispensed";
    private const string DateFormat = "yyyy-MM-dd";

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
        var ev = UsageEvent.CreateSoapEvent(_config.stationId, levelBefore, levelAfter);
        Enqueue(ev);

        int hour = DateTime.Now.Hour;
        _hourlyCount[hour]++;
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

    private void RebuildHourlyCache()
    {
        _hourlyCount = new int[HoursPerDay];
        string today = DateTime.Now.ToString(DateFormat);

        foreach (var ev in _queue.events)
        {
            if (ev.type != SoapEventType)
            {
                continue;
            }
            if (!ev.timestamp.StartsWith(today))
            {
                continue;
            }

            if (DateTime.TryParse(ev.timestamp, out DateTime dt))
            {
                _hourlyCount[dt.Hour]++;
            }
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
        for (int i = 0; i < days; i++)
        {
            result[DateTime.Now.AddDays(-i).ToString(DateFormat)] = 0;
        }

        foreach (var ev in _queue.events)
        {
            if (ev.type != SoapEventType) continue;
            string dateKey = ev.timestamp.Length >= 10 ? ev.timestamp.Substring(0, 10) : ev.timestamp;
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
