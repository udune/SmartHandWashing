using System;
using UnityEngine;

/// <summary>
/// 층수 전환 관리자.
/// - 1F~7F: FloorSampleData.json 샘플 데이터 표시
/// - 8F:    실제 StationData + SoapUsageLogger 실시간 데이터
/// - 핵심 규칙: isRealPLC=false인 층 선택 중엔 PLC 신호 무시
/// </summary>
public class FloorManager : MonoBehaviour
{
    public static FloorManager Instance { get; private set; }

    [Header("References")]
    public StationData stationData;
    public SoapUsageLogger logger;

    private FloorData[] _floors;
    private int _currentIndex = 7;

    public FloorData CurrentFloor
    {
        get { return _floors?[_currentIndex]; }
    }

    /// <summary>현재 선택된 층이 실제 PLC 연동 층인지 여부</summary>
    public bool IsRealPLCFloor
    {
        get { return CurrentFloor?.isRealPLC ?? false; }
    }

    public event Action<FloorData> OnFloorChanged;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadFloorData();
    }

    void Start()
    {
        SyncRealFloorData();
        OnFloorChanged?.Invoke(CurrentFloor);
    }

    private void LoadFloorData()
    {
        var json = Resources.Load<TextAsset>("FloorSampleData");
        if (json == null)
        {
            Debug.LogError("[FloorManager] FloorSampleData.json 없음");
            return;
        }
        var list = JsonUtility.FromJson<FloorDataList>(json.text);
        _floors = list.floors;
    }

    /// <summary>floorId 예: "1F", "8F"</summary>
    public void SelectFloor(string floorId)
    {
        for (int i = 0; i < _floors.Length; i++)
        {
            if (_floors[i].floorId != floorId)
            {
                continue;
            }

            _currentIndex = i;

            if (_floors[i].isRealPLC)
            {
                SyncRealFloorData();
            }

            OnFloorChanged?.Invoke(CurrentFloor);
            Debug.Log($"[FloorManager] 층 전환: {floorId} (PLC연동={IsRealPLCFloor})");
            return;
        }
        Debug.LogWarning($"[FloorManager] 알 수 없는 층: {floorId}");
    }

    /// <summary>
    /// 8F 데이터를 실시간 값으로 갱신.
    /// NetworkManager 폴링 후, SoapUsageLogger 기록 후 호출.
    /// </summary>
    public void SyncRealFloorData()
    {
        if (_floors == null)
        {
            return;
        }

        var real = Array.Find(_floors, f => f.isRealPLC);
        if (real == null)
        {
            return;
        }

        if (stationData != null)
        {
            real.soapLevel = stationData.soapLevel;
        }

        if (logger != null)
        {
            int[] live = logger.HourlyCount;
            for (int h = 0; h < 24 && h < live.Length; h++)
            {
                real.hourlyUsage[h] = live[h];
            }
        }
    }

    public FloorData[] AllFloors
    {
        get { return _floors; }
    }
}
