// System: DateTime, Exception 등 기본 타입
using System;
// System.Collections.Generic: List, Dictionary 컬렉션
using System.Collections.Generic;
// System.IO: File, Path 파일 입출력
using System.IO;
// System.Linq: Where, Take, Sum, Count 등 LINQ 확장 메서드
using System.Linq;
using UnityEngine;

/// <summary>
/// 이벤트를 로컬 JSON에 저장하고 오프라인 큐를 관리.
/// APIClient가 전송에 성공하면 transmitted = true 로 마킹.
/// </summary>
public class SoapUsageLogger : MonoBehaviour
{
    // HoursPerDay: 시간대 배열 크기 (0~23시)
    private const int HoursPerDay = 24;
    // LogFileName: 로컬 저장 파일명
    private const string LogFileName = "usage_log.json";
    // SoapEventType: 비누 이벤트 타입 식별자
    private const string SoapEventType = "soap_dispensed";
    // DateFormat: 날짜 문자열 포맷 (ISO 8601)
    private const string DateFormat = "yyyy-MM-dd";

    // 싱글톤 패턴: 유일한 인스턴스를 저장하는 정적 필드
    private static SoapUsageLogger _instance;
    // Instance: 외부에서 싱글톤 인스턴스에 접근하는 프로퍼티
    public  static SoapUsageLogger Instance => _instance;

    // [Header]: 인스펙터에서 필드 그룹 제목 표시
    [Header("References")]
    // [Tooltip]: 인스펙터에서 마우스 오버 시 설명 표시
    [Tooltip("StationData와 hourlyUsageCount 동기화용 (선택)")]
    // stationData: UI 갱신을 위해 시간대별 데이터 동기화
    public StationData stationData;

    // _config: API URL, 큐 크기 등 설정값 (Resources/AnalyticsConfig.json)
    private AnalyticsConfig _config;
    // _queue: 이벤트 목록을 담는 컨테이너 클래스
    private UsageEventQueue _queue;
    // _savePath: JSON 파일의 전체 경로 (persistentDataPath + 파일명)
    private string          _savePath;

    // _hourlyCount: 시간대별(0~23시) 비누 사용 횟수 캐시
    private int[] _hourlyCount = new int[HoursPerDay];

    // HourlyCount: 외부에서 시간대별 데이터 읽기 (BarChart용)
    public int[]   HourlyCount  => _hourlyCount;
    // TodayTotal: 오늘 총 사용 횟수 (LINQ Sum으로 배열 합계)
    public int     TodayTotal   => _hourlyCount.Sum();
    // PeakHour: 가장 많이 사용한 시간 (0-23)
    public int     PeakHour     { get; private set; }
    // PeakCount: 피크 시간의 사용 횟수
    public int     PeakCount    { get; private set; }

    // Awake(): MonoBehaviour 생명주기 - Start보다 먼저 호출
    void Awake()
    {
        // 싱글톤 보장: 이미 인스턴스가 있으면 자신을 파괴
        if (_instance != null)
        {
            // Destroy(): 게임 오브젝트 제거 (중복 방지)
            Destroy(gameObject);
            return;
        }
        // 자신을 싱글톤 인스턴스로 등록
        _instance = this;

        // 초기화 순서: 설정 → 큐 → 캐시 → UI 동기화
        LoadConfig();
        LoadQueue();
        RebuildHourlyCache();
        SyncToStationData();
    }

    // LoadConfig(): Resources 폴더에서 설정 JSON 로드
    private void LoadConfig()
    {
        // Resources.Load<TextAsset>(): Resources 폴더의 텍스트 파일 로드
        // 경로: Assets/Resources/AnalyticsConfig.json (.json 확장자 생략)
        var json = Resources.Load<TextAsset>("AnalyticsConfig");
        // 파일이 없으면 기본 설정 객체 생성
        _config = json != null
            ? JsonUtility.FromJson<AnalyticsConfig>(json.text)
            : new AnalyticsConfig();
    }

    // LoadQueue(): 로컬 JSON 파일에서 이벤트 큐 로드
    private void LoadQueue()
    {
        // Path.Combine(): 플랫폼에 맞는 경로 구분자로 결합
        // Application.persistentDataPath: 앱 데이터 저장 경로 (플랫폼별 상이)
        _savePath = Path.Combine(Application.persistentDataPath, LogFileName);

        // Early Return: 파일이 없으면 빈 큐 생성 후 종료
        if (!File.Exists(_savePath))
        {
            _queue = new UsageEventQueue();
            return;
        }

        // try-catch: 파일 읽기/파싱 실패 대비
        try
        {
            // File.ReadAllText(): 파일 전체를 문자열로 읽기
            string json = File.ReadAllText(_savePath);
            // JsonUtility.FromJson(): JSON → C# 객체 역직렬화
            // ?? new: 파싱 결과가 null이면 빈 큐 생성
            _queue = JsonUtility.FromJson<UsageEventQueue>(json) ?? new UsageEventQueue();
        }
        catch
        {
            // 파싱 실패 시 빈 큐로 폴백 (데이터 손실보다 앱 안정성 우선)
            _queue = new UsageEventQueue();
        }
    }

    // SaveQueue(): 현재 큐를 로컬 JSON 파일에 저장
    private void SaveQueue()
    {
        try
        {
            // JsonUtility.ToJson(obj, true): C# 객체 → JSON 문자열
            // 두 번째 인자 true: pretty-print (들여쓰기)
            // File.WriteAllText(): 파일에 문자열 저장 (기존 덮어쓰기)
            File.WriteAllText(_savePath, JsonUtility.ToJson(_queue, true));
        }
        catch (Exception e)
        {
            // 디스크 오류 시 경고만 출력 (앱 중단 방지)
            Debug.LogWarning($"[Logger] 큐 저장 실패: {e.Message}");
        }
    }

    // LogSoap(): 비누 사용 이벤트 기록 (StationController에서 호출)
    // levelBefore: 사용 전 비누 잔량 (%)
    // levelAfter: 사용 후 비누 잔량 (%)
    public void LogSoap(float levelBefore, float levelAfter)
    {
        // UsageEvent.CreateSoapEvent(): 팩토리 메서드로 이벤트 생성
        var ev = UsageEvent.CreateSoapEvent(_config.stationId, levelBefore, levelAfter);
        Enqueue(ev);

        // DateTime.Now.Hour: 현재 시간 (0-23)
        int hour = DateTime.Now.Hour;
        // 해당 시간대 카운트 증가
        _hourlyCount[hour]++;
        // 피크 시간 재계산
        UpdatePeak();
        // StationData에 동기화 (UI 갱신)
        SyncToStationData();
    }

    // LogWater(): 물 사용 이벤트 기록 (시간대 집계 없음)
    public void LogWater()
    {
        Enqueue(UsageEvent.CreateWaterEvent(_config.stationId));
    }

    // LogAir(): 에어 드라이 사용 이벤트 기록 (시간대 집계 없음)
    public void LogAir()
    {
        Enqueue(UsageEvent.CreateAirEvent(_config.stationId));
    }

    // Enqueue(): 이벤트를 큐에 추가하고 크기 제한 관리
    private void Enqueue(UsageEvent ev)
    {
        // List.Add(): 큐에 이벤트 추가
        _queue.events.Add(ev);

        // 큐 크기 제한 초과 시 정리
        if (_queue.events.Count > _config.maxQueueSize)
        {
            // RemoveAll(): 조건에 맞는 모든 요소 제거
            // 전송 완료된 이벤트 먼저 제거
            _queue.events.RemoveAll(e => e.transmitted);

            // 그래도 초과면 가장 오래된 것(인덱스 0)부터 제거
            while (_queue.events.Count > _config.maxQueueSize)
            {
                // RemoveAt(0): 첫 번째 요소 제거 (FIFO)
                _queue.events.RemoveAt(0);
            }
        }

        // 변경사항 디스크에 저장
        SaveQueue();
    }

    // MarkTransmitted(): API 전송 성공한 이벤트 마킹 (APIClient에서 호출)
    // eventIds: 전송 성공한 이벤트 ID 목록
    public void MarkTransmitted(List<string> eventIds)
    {
        foreach (var ev in _queue.events)
        {
            // List.Contains(): 목록에 해당 ID가 있는지 확인
            if (eventIds.Contains(ev.eventId))
            {
                // transmitted = true: 재전송 방지
                ev.transmitted = true;
            }
        }
        SaveQueue();
    }

    // GetPendingEvents(): 미전송 이벤트 조회 (APIClient에서 호출)
    // limit: 최대 반환 개수
    public List<UsageEvent> GetPendingEvents(int limit)
    {
        // LINQ 체이닝: 필터 → 제한 → 리스트 변환
        return _queue.events
            // Where(): 조건에 맞는 요소만 필터 (미전송)
            .Where(e => !e.transmitted)
            // Take(): 최대 개수 제한
            .Take(limit)
            // ToList(): IEnumerable → List 변환
            .ToList();
    }

    // RebuildHourlyCache(): 앱 시작 시 오늘의 시간대별 통계 복원
    private void RebuildHourlyCache()
    {
        // 배열 초기화 (모든 값 0)
        _hourlyCount = new int[HoursPerDay];
        // DateTime.Now.ToString(): 현재 날짜를 문자열로 변환
        string today = DateTime.Now.ToString(DateFormat);

        foreach (var ev in _queue.events)
        {
            // 비누 이벤트만 집계 (물/에어 제외)
            if (ev.type != SoapEventType)
            {
                continue;
            }
            // 오늘 날짜 이벤트만 집계
            // String.StartsWith(): 문자열이 특정 접두사로 시작하는지 확인
            if (!ev.timestamp.StartsWith(today))
            {
                continue;
            }

            // DateTime.TryParse(): 문자열 → DateTime 변환 시도
            // out DateTime dt: 변환 결과를 dt 변수에 저장
            if (DateTime.TryParse(ev.timestamp, out DateTime dt))
            {
                // dt.Hour: 시간 부분 추출 (0-23)
                _hourlyCount[dt.Hour]++;
            }
        }
        UpdatePeak();
    }

    // UpdatePeak(): 가장 많이 사용한 시간대 찾기
    private void UpdatePeak()
    {
        PeakCount = 0;
        PeakHour  = 0;

        // 24시간 순회하며 최대값 찾기
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
    // GetDailyUsage(): ReplenishPredictor에서 교체 예측에 사용
    // days: 조회할 과거 일수
    public Dictionary<string, int> GetDailyUsage(int days)
    {
        var result = new Dictionary<string, int>();

        for (int i = 0; i < days; i++)
        {
            // DateTime.AddDays(-i): i일 전 날짜
            string date = DateTime.Now.AddDays(-i).ToString(DateFormat);
            // LINQ Count(): 조건에 맞는 요소 개수
            result[date] = _queue.events
                .Count(e => e.type == SoapEventType && e.timestamp.StartsWith(date));
        }

        return result;
    }

    // Config: 외부에서 설정값 읽기 (APIClient에서 사용)
    public AnalyticsConfig Config => _config;

    // SyncToStationData(): 시간대별 데이터를 StationData에 복사 (UI 갱신용)
    private void SyncToStationData()
    {
        // null 체크: stationData가 연결되지 않았으면 건너뜀
        if (stationData == null)
        {
            return;
        }

        // 배열 요소별 복사
        for (int i = 0; i < HoursPerDay; i++)
        {
            stationData.hourlyUsageCount[i] = _hourlyCount[i];
        }
    }
}
