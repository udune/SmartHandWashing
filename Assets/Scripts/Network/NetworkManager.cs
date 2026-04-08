using System;
using System.Collections;
using Network;
using UnityEngine;

/// <summary>
/// PLC 연결 관리 + 100ms 폴링 루프.
/// 새 래더 구조: 4개 모드 선택 + 8단계 시퀀스
/// </summary>
public class NetworkManager : MonoBehaviour
{
    // ── 싱글톤 패턴 ──
    public static NetworkManager Instance { get; private set; }

    [Header("References")]
    public StationData stationData;
    public StationController stationController;

    // ── 설정 (PLCConfig.json에서 로드) ───────────────────────────────
    private PLCConfig _config;
    private IPLCClient _client;

    // ── 상태 ─────────────────────────────────────────────────────────
    private bool _hasConnectedOnce = false;
    private bool _prevSoapOutput = false;  // 비누 출력 에지 감지용

    public bool IsMockMode => _config?.useMock ?? true;

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

    public string StatusMessage { get; private set; } = "초기화 중...";

    public event Action<bool> OnConnectionChanged;
    public event Action<string> OnStatusChanged;

    // ── 생명주기 ─────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (stationController == null)
        {
            stationController = GetComponent<StationController>();
        }

        LoadConfig();
        SLMPClient.VerifyDeviceAddresses();
        StartCoroutine(ConnectionLoop());
    }

    void OnDestroy()
    {
        _client?.Disconnect();
    }

    // ── 설정 로드 ────────────────────────────────────────────────────

    private void LoadConfig()
    {
        var json = Resources.Load<TextAsset>("PLCConfig");
        if (json != null)
        {
            _config = JsonUtility.FromJson<PLCConfig>(json.text);
            Debug.Log($"[Network] 설정 로드: useMock={_config.useMock}, ip={_config.ip}");
        }
        else
        {
            _config = new PLCConfig();
            Debug.LogWarning("[Network] PLCConfig.json 없음 — 기본값(Mock) 사용");
        }

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

    private IEnumerator ConnectionLoop()
    {
        if (!_hasConnectedOnce)
        {
            SetStatus("연결 시도 중...", false);
            yield return ConnectCoroutine();
            _hasConnectedOnce = true;
        }

        while (true)
        {
            if (!IsConnected)
            {
                SetStatus("연결 시도 중...", false);
                yield return ConnectCoroutine();
            }
            else
            {
                yield return PollCoroutine();
            }
        }
    }

    private IEnumerator ConnectCoroutine()
    {
        var task = _client.ConnectAsync(_config.ip, _config.port, _config.timeoutMs);
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            SetStatus($"연결 실패 — 5초 후 재시도", false);
            yield return new WaitForSeconds(5f);
        }
        else
        {
            SetStatus("연결됨", true);
        }
    }

    // ── 폴링 루프 (100ms) ────────────────────────────────────────────

    /// <summary>
    /// 새 래더 구조 기반 폴링:
    /// - 4개 모드 상태: M10(비누), M20(물), M30(수동), M40(건조)
    /// - 8단계 시퀀스: M0~M7
    /// - 4개 출력: Y0C0(비누), Y0C1(물대기), Y0C2(물), Y0C3(건조)
    /// </summary>
    private IEnumerator PollCoroutine()
    {
        // ── 1. 모드 상태 읽기 (M10, M20, M30, M40) ──────────────────
        var modeTask = _client.ReadBitsAsync(_config.devices.soapMode, 31);
        yield return new WaitUntil(() => modeTask.IsCompleted);

        if (modeTask.IsFaulted)
        {
            SetStatus("통신 오류 — 재연결", false);
            _client.Disconnect();
            yield break;
        }

        bool[] modeBits = modeTask.Result;
        if (modeBits.Length >= 31)
        {
            // M10=bits[0], M20=bits[10], M30=bits[20], M40=bits[30]
            stationData.isSoapMode = modeBits[0];    // M10
            stationData.isWaterMode = modeBits[10];  // M20
            stationData.isManualMode = modeBits[20]; // M30
            stationData.isDryMode = modeBits[30];    // M40
        }

        // ── 2. 동작 상태 읽기 (M0~M7: 8단계 시퀀스) ─────────────────
        var seqTask = _client.ReadBitsAsync(_config.devices.soapRunning, 8);
        yield return new WaitUntil(() => seqTask.IsCompleted);

        bool soapOn = false, waterOn = false, airOn = false;
        int currentStep = 0;

        if (!seqTask.IsFaulted && seqTask.Result.Length >= 8)
        {
            bool[] seq = seqTask.Result;
            // M0=비누, M1=물대기, M2=물, M3=건조, M4=헹굼대기, M5=헹굼, M6=비누2, M7=건조2

            // 비누 동작: M0(비누) OR M6(비누2)
            soapOn = seq[0] || seq[6];

            // 물 동작: M1(물대기) OR M2(물) OR M4(헹굼대기) OR M5(헹굼)
            waterOn = seq[1] || seq[2] || seq[4] || seq[5];

            // 건조 동작: M3(건조) OR M7(건조2)
            airOn = seq[3] || seq[7];

            // 현재 단계 판별 (UI 표시용)
            if (seq[0]) currentStep = 1;      // 비누
            else if (seq[1]) currentStep = 2; // 물 대기
            else if (seq[2]) currentStep = 3; // 물
            else if (seq[3]) currentStep = 4; // 건조
            else if (seq[4]) currentStep = 5; // 헹굼 대기
            else if (seq[5]) currentStep = 6; // 헹굼
            else if (seq[6]) currentStep = 7; // 비누2
            else if (seq[7]) currentStep = 8; // 건조2
        }

        // ── 3. StationData 갱신 ────────────────────────────────────
        stationData.isSoapRunning = soapOn;
        stationData.isWaterRunning = waterOn;
        stationData.isAirRunning = airOn;
        stationData.currentStep = currentStep;

        // ── 4. 비누 사용 감지 (비누 출력 OFF → ON 엣지) ─────────────
        if (soapOn && !_prevSoapOutput)
        {
            float before = stationData.soapLevel;
            stationData.UseSoap();
            SoapUsageLogger.Instance?.LogSoap(before, stationData.soapLevel);
            FloorManager.Instance?.SyncRealFloorData();
        }
        _prevSoapOutput = soapOn;

        // ── 5. 시스템 상태 갱신 ────────────────────────────────────
        UpdateSystemStatus(stationData.soapLevel);

        yield return new WaitForSeconds(_config.pollIntervalMs / 1000f);
    }

    // ── HMI → PLC 쓰기 (모드 선택 버튼) ──────────────────────────────

    /// <summary>비누 모드 선택 버튼 (X0A7)</summary>
    public void WriteSoapSelectButton(bool value)
        => StartCoroutine(WriteBitCoroutine(_config.devices.soapSelectBtn, value));

    /// <summary>물 모드 선택 버튼 (X0A8)</summary>
    public void WriteWaterSelectButton(bool value)
        => StartCoroutine(WriteBitCoroutine(_config.devices.waterSelectBtn, value));

    /// <summary>수동 모드 선택 버튼 (X0A9)</summary>
    public void WriteManualSelectButton(bool value)
        => StartCoroutine(WriteBitCoroutine(_config.devices.manualSelectBtn, value));

    /// <summary>건조 모드 선택 버튼 (X0A6)</summary>
    public void WriteDrySelectButton(bool value)
        => StartCoroutine(WriteBitCoroutine(_config.devices.drySelectBtn, value));

    /// <summary>손 센서 트리거 (X0A0) - Mock 테스트용</summary>
    public void WriteHandSensor(bool value)
        => StartCoroutine(WriteBitCoroutine(_config.devices.handSensor, value));

    private IEnumerator WriteBitCoroutine(string device, bool value)
    {
        if (!IsConnected) yield break;

        var task = _client.WriteBitsAsync(device, new[] { value });
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
            Debug.LogWarning($"[Network] 비트 쓰기 실패: {device} = {value}");
        else
            Debug.Log($"[Network] 쓰기 완료: {device} = {value}");
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────

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

    private void SetStatus(string msg, bool connected)
    {
        StatusMessage = msg;
        OnStatusChanged?.Invoke(msg);
        OnConnectionChanged?.Invoke(connected);
        Debug.Log($"[Network] {msg}");
    }

    // ── Mock 전용: 시뮬레이션 ─────────────────────────────────────

    /// <summary>Mock: 손 센서 감지 → 8단계 시퀀스 시뮬레이션</summary>
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
        if (_client is MockPLCClient mock)
        {
            mock.SetBit("M0", true);  // 비누 동작 ON
            mock.SimulateSoapUse(50);
            StartCoroutine(ResetBitAfterDelay(mock, "M0", 0.5f));
        }
        else
        {
            Debug.LogWarning("Mock 모드가 아닙니다.");
        }
    }

    private IEnumerator ResetBitAfterDelay(MockPLCClient mock, string device, float delay)
    {
        yield return new WaitForSeconds(delay);
        mock.SetBit(device, false);
    }

    [ContextMenu("Mock: 비누 잔량 리셋 (100%)")]
    public void MockResetSoapLevel()
    {
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
