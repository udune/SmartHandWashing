using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// PLC 연결 관리 + 100ms 폴링 루프.
/// StationData를 PLC 데이터로 갱신한다.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    [Header("References")]
    public StationData stationData;
    public StationController stationController;  // PLC → HMI 동작 연동

    // ── 설정 (PLCConfig.json에서 로드) ───────────────────────────────
    private PLCConfig _config;
    private IPLCClient _client;

    // ── 상태 ─────────────────────────────────────────────────────────
    private bool _hasConnectedOnce = false;  // 최초 연결 여부 추적
    private bool[] _prevBits = new bool[3];  // 이전 비트 상태 (에지 검출용)
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

    void Start()
    {
        // StationController 자동 연결 (같은 GameObject에 있는 경우)
        if (stationController == null)
        {
            stationController = GetComponent<StationController>();
        }

        LoadConfig();
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
        // 최초 연결 시도 (Mock 모드에서도 ConnectAsync 호출 보장)
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

    private IEnumerator PollCoroutine()
    {
        // 비누 잔량 읽기 (D0, 1워드)
        var readTask = _client.ReadWordsAsync(_config.devices.soapLevel, 1);
        yield return new WaitUntil(() => readTask.IsCompleted);

        if (readTask.IsFaulted)
        {
            SetStatus("통신 오류 — 재연결", false);
            _client.Disconnect();
            yield break;
        }

        int raw = readTask.Result[0];
        // D0: 0~1000 → 0.0~100.0%
        float pct = Mathf.Clamp(raw / 10f, 0f, 100f);
        if (Mathf.Abs(stationData.soapLevel - pct) > 0.1f)
        {
            stationData.soapLevel = pct;
            UpdateSystemStatus(pct);
        }

        // 비트 읽기 (M0부터 3개 — 비누/물/에어 버튼 상태)
        var bitTask = _client.ReadBitsAsync(_config.devices.soapBtn, 3);
        yield return new WaitUntil(() => bitTask.IsCompleted);

        if (!bitTask.IsFaulted)
        {
            bool[] bits = bitTask.Result;
            // PLC 신호로 버튼 상태 동기화 (PLC → HMI 방향)
            // 상승 에지 검출: OFF→ON 전환 시에만 동작 트리거
            if (stationController != null)
            {
                // M0: 비누 버튼 (상승 에지)
                if (bits[0] && !_prevBits[0])
                {
                    stationController.ActivateSoap();
                    Debug.Log("[Network] PLC 신호: 비누 활성화");
                }
                // M1: 물 버튼 (상승 에지)
                if (bits[1] && !_prevBits[1])
                {
                    stationController.ActivateWater();
                    Debug.Log("[Network] PLC 신호: 물 활성화");
                }
                // M2: 에어 버튼 (상승 에지)
                if (bits[2] && !_prevBits[2])
                {
                    stationController.ActivateAir();
                    Debug.Log("[Network] PLC 신호: 에어 활성화");
                }
            }
            // 현재 상태를 이전 상태로 저장
            _prevBits[0] = bits[0];
            _prevBits[1] = bits[1];
            _prevBits[2] = bits[2];
        }

        yield return new WaitForSeconds(_config.pollIntervalMs / 1000f);
    }

    // ── HMI → PLC 쓰기 (버튼 클릭 시 외부에서 호출) ─────────────────

    public void WriteSoapButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.soapBtn, value));
    }

    public void WriteWaterButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.waterBtn, value));
    }

    public void WriteAirButton(bool value)
    {
        StartCoroutine(WriteBitCoroutine(_config.devices.airBtn, value));
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

    // ── Mock 전용: 비누 사용 시뮬레이션 ─────────────────────────────

    /// <summary>Inspector 또는 테스트 버튼에서 호출 — Mock 모드 전용</summary>
    [ContextMenu("Mock: 비누 사용 시뮬레이션")]
    public void MockSimulateSoapUse()
    {
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
            Debug.LogWarning("Mock 모드가 아닙니다.");
    }

    private IEnumerator ResetButtonBitAfterDelay(MockPLCClient mock, int index, float delay)
    {
        yield return new WaitForSeconds(delay);
        mock.SetButtonBit(index, false);
    }

    /// <summary>비누 잔량 리셋 — Mock 모드 전용</summary>
    [ContextMenu("Mock: 비누 잔량 리셋 (100%)")]
    public void MockResetSoapLevel()
    {
        if (_client is MockPLCClient mock)
            mock.ResetSoapLevel(1000);
        else
            Debug.LogWarning("Mock 모드가 아닙니다.");
    }
}
