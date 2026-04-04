// System: Action<T> 델리게이트 타입 사용
using System;
// System.Collections: IEnumerator 코루틴 반환 타입
using System.Collections;
using Network;
using UnityEngine;

/// <summary>
/// PLC 연결 관리 + 100ms 폴링 루프.
/// StationData를 PLC 데이터로 갱신한다.
/// </summary>
// MonoBehaviour: Unity 생명주기(Start, OnDestroy) 및 코루틴 사용
public class NetworkManager : MonoBehaviour
{
    // ── 싱글톤 패턴 ──
    // Instance: 전역 접근용 정적 프로퍼티 (StationController에서 사용)
    public static NetworkManager Instance { get; private set; }

    // [Header]: Inspector에서 섹션 구분용 라벨 표시
    [Header("References")]
    // public 필드: Inspector에서 드래그&드롭으로 연결 가능
    public StationData stationData;
    public StationController stationController;  // PLC → HMI 동작 연동

    // ── 설정 (PLCConfig.json에서 로드) ───────────────────────────────
    // _config: Resources/PLCConfig.json에서 역직렬화된 설정 객체
    private PLCConfig _config;
    // _client: IPLCClient 인터페이스 (Mock/SLMP 교체 가능)
    private IPLCClient _client;

    // ── 상태 ─────────────────────────────────────────────────────────
    private bool _hasConnectedOnce = false;  // 최초 연결 여부 추적
    // _prevBits: 상승 에지 감지용 (이전 프레임과 비교하여 OFF→ON 검출)
    private bool[] _prevBits = new bool[3];  // 이전 비트 상태 (에지 검출용)
    // _prevSoapSignal: 비누 사용 로깅용 이전 신호 상태
    private bool _prevSoapSignal = false;

    // IsMockMode: Mock 모드 여부 (StationController에서 참조)
    // PLCConfig.useMock 값 기반으로 판단
    public bool IsMockMode => _config?.useMock ?? true;

    // IsConnected: null 체크 후 클라이언트 연결 상태 반환
    public bool IsConnected
    {
        get
        {
            if (_client != null && _client.IsConnected)
            {
                return true;
            }
            return false;
        }
    }
    // auto-property with initializer: private set으로 외부 수정 차단
    public string StatusMessage { get; private set; } = "초기화 중...";

    // event: 상태 변화 시 구독자에게 알림 (Observer 패턴)
    public event Action<bool> OnConnectionChanged;
    public event Action<string> OnStatusChanged;

    // ── 생명주기 ─────────────────────────────────────────────────────

    // Awake(): Start()보다 먼저 호출, 싱글톤 등록
    void Awake()
    {
        // 싱글톤 보장: 기존 인스턴스 있으면 자신 파괴
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Start(): 첫 프레임 전에 호출, 초기화 로직 수행
    void Start()
    {
        // StationController 자동 연결 (같은 GameObject에 있는 경우)
        // GetComponent<T>(): 동일 GameObject에서 컴포넌트 검색
        if (stationController == null)
        {
            stationController = GetComponent<StationController>();
        }

        LoadConfig();
        // StartCoroutine: 코루틴을 백그라운드에서 실행 시작
        StartCoroutine(ConnectionLoop());
    }

    // OnDestroy(): GameObject 파괴 시 호출, 리소스 정리
    void OnDestroy()
    {
        // ?. (null 조건부 연산자): _client가 null이면 Disconnect 호출 스킵
        _client?.Disconnect();
    }

    // ── 설정 로드 ────────────────────────────────────────────────────

    // LoadConfig: JSON 설정 파일 로드 및 PLC 클라이언트 인스턴스 생성
    private void LoadConfig()
    {
        // Resources.Load<T>(): Resources 폴더에서 에셋 로드 (확장자 생략)
        var json = Resources.Load<TextAsset>("PLCConfig");
        if (json != null)
        {
            // JsonUtility.FromJson<T>(): JSON 문자열 → C# 객체 역직렬화
            _config = JsonUtility.FromJson<PLCConfig>(json.text);
            Debug.Log($"[Network] 설정 로드: useMock={_config.useMock}, ip={_config.ip}");
        }
        else
        {
            // 설정 파일 없으면 기본값 사용 (PLCConfig 생성자의 기본값)
            _config = new PLCConfig();
            Debug.LogWarning("[Network] PLCConfig.json 없음 — 기본값(Mock) 사용");
        }

        // 설정에 따라 Mock 또는 실제 SLMP 클라이언트 생성 (전략 패턴)
        if (_config.useMock)
        {
            _client = new MockPLCClient();
        }
        else
        {
            _client = new SLMPClient();
        }
    }

    // ── 연결 루프 ────────────────────────────────────────────────────

    // ConnectionLoop: 연결 상태 감시 + 자동 재연결 무한 루프
    private IEnumerator ConnectionLoop()
    {
        // 최초 연결 시도 (Mock 모드에서도 ConnectAsync 호출 보장)
        if (!_hasConnectedOnce)
        {
            SetStatus("연결 시도 중...", false);
            // yield return 코루틴: 해당 코루틴 완료까지 대기
            yield return ConnectCoroutine();
            _hasConnectedOnce = true;
        }

        // while (true): Unity 프레임과 독립적인 영구 루프
        while (true)
        {
            if (!IsConnected)
            {
                SetStatus("연결 시도 중...", false);
                yield return ConnectCoroutine();
            }
            else
            {
                // 연결 중이면 폴링 실행
                yield return PollCoroutine();
            }
        }
    }

    // ConnectCoroutine: 비동기 Task를 코루틴으로 래핑하여 연결 시도
    private IEnumerator ConnectCoroutine()
    {
        var task = _client.ConnectAsync(_config.ip, _config.port, _config.timeoutMs);
        // WaitUntil: 조건이 true가 될 때까지 매 프레임 체크
        yield return new WaitUntil(() => task.IsCompleted);

        // task.IsFaulted: 비동기 작업 중 예외 발생 여부
        if (task.IsFaulted)
        {
            SetStatus($"연결 실패 — 5초 후 재시도", false);
            // WaitForSeconds: 지정 시간(초) 대기 후 다음 줄 실행
            yield return new WaitForSeconds(5f);
        }
        else
        {
            SetStatus("연결됨", true);
        }
    }

    // ── 폴링 루프 (100ms) ────────────────────────────────────────────

    // PollCoroutine: PLC에서 데이터 읽기 + StationData bool 직접 갱신
    private IEnumerator PollCoroutine()
    {
        // ── 비누 잔량 읽기 (D0, 워드) ────────────────────────────────
        var wordTask = _client.ReadWordsAsync(_config.devices.soapLevel, 1);
        yield return new WaitUntil(() => wordTask.IsCompleted);

        if (wordTask.IsFaulted)
        {
            SetStatus("통신 오류 — 재연결", false);
            _client.Disconnect();
            // yield break: 코루틴 즉시 종료 → ConnectionLoop에서 재연결 시도
            yield break;
        }

        // wordTask.Result: Task<int[]>의 결과값 접근
        // D0: 0~1000 → 0.0~100.0% 변환, Mathf.Clamp로 범위 제한
        float pct = Mathf.Clamp(wordTask.Result[0] / 10f, 0f, 100f);
        // Mathf.Abs: 차이가 0.1% 이상일 때만 갱신 (불필요한 이벤트 방지)
        if (Mathf.Abs(stationData.soapLevel - pct) > 0.1f)
        {
            stationData.soapLevel = pct;
            UpdateSystemStatus(pct);

            // FloorManager 8F 실시간 데이터 동기화
            FloorManager.Instance?.SyncRealFloorData();
        }

        // ── M 디바이스 읽기 (M0~M2: 비누/물/에어 ON/OFF 신호) ────────
        var bitTask = _client.ReadBitsAsync(_config.devices.soapBtn, 3);
        yield return new WaitUntil(() => bitTask.IsCompleted);

        if (!bitTask.IsFaulted)
        {
            bool[] bits = bitTask.Result;

            // TEST 모드일 때는 StationController의 코루틴이 
            // 직접 상태를 제어하므로 PLC에서 읽어온 값(보통 false)으로 덮어쓰지 않습니다.
            // 이를 통해 인터록(IsAnyRunning)과 타이머 UI가 정상적으로 유지됩니다.
            if (!AppModeManager.IsTestMode)
            {
                // StationData bool 값 갱신 → StationController.Update()가 감지
                stationData.isSoapRunning  = bits.Length > 0 && bits[0];
                stationData.isWaterRunning = bits.Length > 1 && bits[1];
                stationData.isAirRunning   = bits.Length > 2 && bits[2];

                // 비누 사용 감지: 신호 ON 시 로거 기록 (상승 에지)
                if (stationData.isSoapRunning && !_prevSoapSignal)
                {
                    // 사용 전 잔량 추정: 현재값 + 감소량
                    SoapUsageLogger.Instance?.LogSoap(pct + _config.soapDecreasePerUse, pct);
                }

                _prevSoapSignal = stationData.isSoapRunning;
            }
        }

        // pollIntervalMs(밀리초) → 초 변환하여 대기
        yield return new WaitForSeconds(_config.pollIntervalMs / 1000f);
    }

    // ── HMI → PLC 쓰기 (버튼 클릭 시 외부에서 호출) ─────────────────

    // WriteSoapButton: HMI에서 비누 버튼 클릭 시 PLC M0 비트에 쓰기
    public void WriteSoapButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.soapBtn, value));
    }

    // WriteWaterButton: HMI에서 물 버튼 클릭 시 PLC M1 비트에 쓰기
    public void WriteWaterButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.waterBtn, value));
    }

    // WriteAirButton: HMI에서 에어 버튼 클릭 시 PLC M2 비트에 쓰기
    public void WriteAirButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.airBtn, value));
    }

    // WriteBitCoroutine: 비동기 비트 쓰기를 코루틴으로 래핑
    private IEnumerator WriteBitCoroutine(string device, bool value)
    {
        // 연결 안 되어 있으면 즉시 종료
        if (!IsConnected)
        {
            yield break;
        }

        // new[] { value }: 단일 요소 배열 인라인 생성
        var task = _client.WriteBitsAsync(device, new[] { value });
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            // $"" (문자열 보간): 변수를 문자열에 삽입
            Debug.LogWarning($"[Network] 비트 쓰기 실패: {device} = {value}");
        }
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────

    // UpdateSystemStatus: 비누 잔량에 따른 시스템 상태 자동 판단
    // 0%: Error, 1-20%: Warning, 21-100%: Normal
    private void UpdateSystemStatus(float soapPct)
    {
        if (soapPct <= 0f)
        {
            stationData.systemStatus = StationData.SystemStatus.Error;
        }
        else if (soapPct <= 20f)
        {
            stationData.systemStatus = StationData.SystemStatus.Warning;
        }
        else
        {
            stationData.systemStatus = StationData.SystemStatus.Normal;
        }
    }

    // SetStatus: 상태 메시지 갱신 + 이벤트 발행 (UI 등 구독자에게 알림)
    private void SetStatus(string msg, bool connected)
    {
        StatusMessage = msg;
        // ?.Invoke(): 구독자가 없으면 (null이면) 호출 스킵
        OnStatusChanged?.Invoke(msg);
        OnConnectionChanged?.Invoke(connected);
        Debug.Log($"[Network] {msg}");
    }

    // ── Mock 전용: 비누 사용 시뮬레이션 ─────────────────────────────

    /// <summary>Inspector 또는 테스트 버튼에서 호출 — Mock 모드 전용</summary>
    // [ContextMenu]: Inspector 컴포넌트 우클릭 시 메뉴에 추가
    [ContextMenu("Mock: 센서 순차 동작 시뮬레이션")]
    public void MockSimulateSensorSequence()
    {
        if (_client is MockPLCClient mock)
        {
            _ = mock.SimulateSensorSequence();
        }
        else
        {
            Debug.LogWarning("Mock 모드가 아닙니다.");
        }
    }

    [ContextMenu("Mock: 비누 사용 시뮬레이션")]
    public void MockSimulateSoapUse()
    {
        // is 패턴 매칭: 타입 확인 + 캐스팅 + 변수 선언 동시 수행
        if (_client is MockPLCClient mock)
        {
            // 비트(M0)를 ON으로 설정 → 폴링에서 상승 에지 검출 → ActivateSoap() 호출
            mock.SetButtonBit(0, true);
            // 비누 잔량도 감소 (실제 PLC 동작 시뮬레이션)
            mock.SimulateSoapUse(50);
            // 잠시 후 비트 OFF (연속 시뮬레이션 가능하도록)
            StartCoroutine(ResetButtonBitAfterDelay(mock, 0, 0.5f));
        }
        else
        {
            Debug.LogWarning("Mock 모드가 아닙니다.");
        }
    }

    // ResetButtonBitAfterDelay: 지정 시간 후 버튼 비트 OFF (연속 테스트 가능)
    private IEnumerator ResetButtonBitAfterDelay(MockPLCClient mock, int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        mock.SetButtonBit(index, false);
    }

    /// <summary>비누 잔량 리셋 — Mock 모드 전용</summary>
    [ContextMenu("Mock: 비누 잔량 리셋 (100%)")]
    public void MockResetSoapLevel()
    {
        // is 패턴 매칭: MockPLCClient 타입일 때만 실행
        if (_client is MockPLCClient mock)
        {
            mock.ResetSoapLevel(1000);
        }
        else
        {
            Debug.LogWarning("Mock 모드가 아닙니다.");
        }
    }
}