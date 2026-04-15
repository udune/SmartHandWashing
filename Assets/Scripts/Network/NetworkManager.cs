using System;
using System.Collections;
using Network;
using UnityEngine;

/// <summary>
/// PLC 연결 관리 + 100ms 폴링 루프.
///
/// 폴링 구조 (스펙 기준):
///   - M10/M20/M30/M40: 모드 선택 릴레이
///   - M0~M7: 수동/자동 동작 릴레이
///   - Y0C0~Y0C3: 출력 (물/세정제전진/후진/바람)
/// </summary>
public class NetworkManager : MonoBehaviour
{
    // ── 싱글톤 ──────────────────────────────────────────────────────
    public static NetworkManager Instance { get; private set; }

    [Header("References")]
    public StationData stationData;
    public StationController stationController;

    private PLCConfig  _config;
    private IPLCClient _client;

    private bool _prevSoapFwd = false;   // 세정제 전진 상승엣지 감지 (M1||M4)

    public bool IsMockMode
    {
        get { return _config?.useMock ?? true; }
    }

    public bool IsConnected
    {
        get { return _client != null && _client.IsConnected; }
    }

    public string StatusMessage { get; private set; } = "초기화 중...";

    public event Action<bool>   OnConnectionChanged;
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

    // ── 설정 로드 ─────────────────────────────────────────────────────

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

        _client = _config.useMock ? (IPLCClient)new MockPLCClient() : new SLMPClient();
    }

    // ── 연결 루프 ─────────────────────────────────────────────────────

    private IEnumerator ConnectionLoop()
    {
        SetStatus("연결 시도 중...", false);
        yield return ConnectCoroutine();

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
            SetStatus("연결 실패 — 5초 후 재시도", false);
            yield return new WaitForSeconds(5f);
        }
        else
        {
            SetStatus("연결됨", true);
        }
    }

    // ── 폴링 루프 (100ms) ─────────────────────────────────────────────

    private IEnumerator PollCoroutine()
    {
        // ── 1. 모드 릴레이 읽기 (M10~M40: 31비트) ────────────────────
        var modeTask = _client.ReadBitsAsync(_config.devices.manualWaterMode, 31);
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
            stationData.isManualWaterMode = modeBits[0];    // M10
            stationData.isManualSoapMode  = modeBits[10];   // M20
            stationData.isManualAirMode   = modeBits[20];   // M30
            stationData.isAutoMode        = modeBits[30];   // M40
        }

        // ── 2. 동작 릴레이 읽기 (M0~M7: 8비트) ──────────────────────
        var actionTask = _client.ReadBitsAsync(_config.devices.manualWater, 8);
        yield return new WaitUntil(() => actionTask.IsCompleted);

        if (actionTask.IsFaulted)
        {
            SetStatus("통신 오류 — 재연결", false);
            _client.Disconnect();
            yield break;
        }

        bool[] m = actionTask.Result;
        // m[0]=M0(수동물)  m[1]=M1(수동세정제전진)  m[2]=M2(수동세정제후진)  m[3]=M3(수동바람)
        // m[4]=M4(자동전진) m[5]=M5(자동후진)        m[6]=M6(자동물)          m[7]=M7(자동바람)

        bool waterOn  = m.Length >= 8 && (m[0] || m[6]);   // Y0C0 = M0 OR M6
        bool soapFwd  = m.Length >= 8 && (m[1] || m[4]);   // Y0C1 = M1 OR M4
        bool soapBwd  = m.Length >= 8 && (m[2] || m[5]);   // Y0C2 = M2 OR M5
        bool airOn    = m.Length >= 8 && (m[3] || m[7]);   // Y0C3 = M3 OR M7

        // ── 3. StationData 갱신 ───────────────────────────────────────
        stationData.isSoapRunning  = soapFwd || soapBwd;
        stationData.isWaterRunning = waterOn;
        stationData.isAirRunning   = airOn;
        stationData.isSoapForward  = soapFwd;
        stationData.isSoapBackward = soapBwd;
        bool anyMode = stationData.isManualWaterMode || stationData.isManualSoapMode || stationData.isManualAirMode || stationData.isAutoMode;
        stationData.currentStep = StationData.ComputeStep(anyMode, soapFwd, soapBwd, waterOn, airOn);

        // ── 4. 세정제 사용 감지 (전진 시작 상승엣지 = 실제 분사 순간) ─
        if (soapFwd && !_prevSoapFwd)
        {
            float before = stationData.soapLevel;
            stationData.UseSoap();
            SoapUsageLogger.Instance?.LogSoap(before, stationData.soapLevel);
            FloorManager.Instance?.SyncRealFloorData();
        }
        _prevSoapFwd = soapFwd;

        // ── 5. 시스템 상태 갱신 ───────────────────────────────────────
        UpdateSystemStatus(stationData.soapLevel);

        yield return new WaitForSeconds(_config.pollIntervalMs / 1000f);
    }

    // ── HMI → PLC 쓰기 ────────────────────────────────────────────────
    // ⚠️ X 디바이스(X0A6~X0A9)는 PLC 물리 입력 레지스터입니다.
    //    SLMP Write가 반영되려면 GX Works3에서 "디바이스 강제(Force)"가 허용되어야 합니다.
    //    허용되지 않으면 PLC가 오류 코드를 반환하고 WriteBitCoroutine이 경고를 출력합니다.

    /// <summary>세정제 스위치 (X0A8) — 수동 세정제 모드 트리거</summary>
    public void WriteSoapButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.soapBtn, value));
    }

    /// <summary>물 스위치 (X0A7) — 수동 물 모드 트리거</summary>
    public void WriteWaterButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.waterBtn, value));
    }

    /// <summary>바람 스위치 (X0A9) — 수동 바람 모드 트리거</summary>
    public void WriteAirButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.airBtn, value));
    }

    /// <summary>손 인식 센서 (X0A6) — 자동 모드 트리거</summary>
    public void WriteHandSensor(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.handSensor, value));
    }

    private IEnumerator WriteBitCoroutine(string device, bool value)
    {
        if (!IsConnected)
        {
            yield break;
        }
        var task = _client.WriteBitsAsync(device, new[] { value });
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.IsFaulted)
        {
            Debug.LogWarning($"[Network] 비트 쓰기 실패: {device} = {value}");
        }
        else
        {
            Debug.Log($"[Network] 쓰기 완료: {device} = {value}");
        }
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────────

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

    // ── Mock 전용 컨텍스트 메뉴 ───────────────────────────────────────

    [ContextMenu("Mock: 손 센서 → 자동 사이클 시뮬레이션")]
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

    [ContextMenu("Mock: 세정제 사용 시뮬레이션")]
    public void MockSimulateSoapUse()
    {
        if (_client is MockPLCClient mock)
        {
            mock.SetBit("M1", true);          // 수동 세정제 전진 릴레이 ON
            mock.SimulateSoapUse(50);
            StartCoroutine(ResetBitAfterDelay(mock, "M1", 0.5f));
        }
        else
        {
            Debug.LogWarning("Mock 모드가 아닙니다.");
        }
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

    private IEnumerator ResetBitAfterDelay(MockPLCClient mock, string device, float delay)
    {
        yield return new WaitForSeconds(delay);
        mock.SetBit(device, false);
    }
}
