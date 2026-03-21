# STEP 9 — SLMP 네트워크 연동 (Claude Code 작업 지시서)

> **전제 조건:** STEP 4~8 구현 완료 상태  
> **PLC 없이 전체 구현 및 테스트 가능** — Mock 모드 + Python 가상 서버 활용  
> **PLC 도착 후 변경할 것:** `PLCConfig.json` 값 2~3개뿐, 코드 수정 없음

---

## 9-1. 폴더 및 파일 구조

아래 파일들을 생성해줘:

```
Assets/
  Scripts/
    Network/
      IPLCClient.cs          ← 인터페이스 (핵심)
      SLMPClient.cs          ← 실제 PLC 통신
      MockPLCClient.cs       ← 가상 PLC (테스트용)
      NetworkManager.cs      ← 폴링 루프, 연결 관리
      PLCDeviceMap.cs        ← 디바이스 주소 매핑 데이터 클래스
  Resources/
    PLCConfig.json           ← 런타임 설정 파일
```

---

## 9-2. PLCConfig.json

`Assets/Resources/PLCConfig.json` 을 아래 내용으로 생성해줘:

```json
{
  "useMock": true,
  "ip": "192.168.1.10",
  "port": 5007,
  "pollIntervalMs": 100,
  "timeoutMs": 2000,
  "devices": {
    "soapLevel":  "D0",
    "soapBtn":    "M0",
    "waterBtn":   "M1",
    "airBtn":     "M2",
    "soapAlarm":  "M10",
    "waterAlarm": "M11",
    "usageCount": "D10"
  }
}
```

> **PLC 도착 후:** `"useMock": false` 로 변경, `"ip"` 에 실제 PLC IP 입력

---

## 9-3. PLCDeviceMap.cs

`Assets/Scripts/Network/PLCDeviceMap.cs` 를 생성해줘:

```csharp
[System.Serializable]
public class PLCDeviceMap
{
    public string soapLevel  = "D0";
    public string soapBtn    = "M0";
    public string waterBtn   = "M1";
    public string airBtn     = "M2";
    public string soapAlarm  = "M10";
    public string waterAlarm = "M11";
    public string usageCount = "D10";
}

[System.Serializable]
public class PLCConfig
{
    public bool   useMock       = true;
    public string ip            = "192.168.1.10";
    public int    port          = 5007;
    public int    pollIntervalMs = 100;
    public int    timeoutMs     = 2000;
    public PLCDeviceMap devices = new PLCDeviceMap();
}
```

---

## 9-4. IPLCClient.cs — 인터페이스

`Assets/Scripts/Network/IPLCClient.cs` 를 생성해줘:

```csharp
using System.Threading.Tasks;

public interface IPLCClient
{
    bool IsConnected { get; }

    /// <summary>device 시작 주소에서 count개 워드 읽기 (D 디바이스용)</summary>
    Task<int[]> ReadWordsAsync(string device, int count);

    /// <summary>device 시작 주소에서 count개 비트 읽기 (M 디바이스용)</summary>
    Task<bool[]> ReadBitsAsync(string device, int count);

    /// <summary>워드 쓰기</summary>
    Task WriteWordsAsync(string device, int[] values);

    /// <summary>비트 쓰기</summary>
    Task WriteBitsAsync(string device, bool[] values);

    Task ConnectAsync(string ip, int port, int timeoutMs);
    void Disconnect();
}
```

---

## 9-5. MockPLCClient.cs — 가상 PLC

`Assets/Scripts/Network/MockPLCClient.cs` 를 생성해줘:

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// PLC 없이 동작하는 가상 클라이언트.
/// 내부 딕셔너리로 D/M 디바이스를 시뮬레이션한다.
/// </summary>
public class MockPLCClient : IPLCClient
{
    public bool IsConnected => true;

    // D 디바이스 (워드, 초기값 설정)
    private readonly Dictionary<string, int> _words = new Dictionary<string, int>
    {
        { "D0",  1000 },   // 비누 잔량 (0~1000 = 0~100%)
        { "D10", 0    },   // 누적 사용 횟수
    };

    // M 디바이스 (비트)
    private readonly Dictionary<string, bool> _bits = new Dictionary<string, bool>
    {
        { "M0",  false },  // 비누 버튼
        { "M1",  false },  // 물 버튼
        { "M2",  false },  // 에어 버튼
        { "M10", false },  // 비누 알람
        { "M11", false },  // 물 알람
    };

    public Task ConnectAsync(string ip, int port, int timeoutMs)
    {
        Debug.Log("[MockPLC] 가상 PLC 연결됨");
        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        Debug.Log("[MockPLC] 연결 해제");
    }

    public Task<int[]> ReadWordsAsync(string device, int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            string key = IncrementDevice(device, i);
            result[i] = _words.ContainsKey(key) ? _words[key] : 0;
        }
        return Task.FromResult(result);
    }

    public Task<bool[]> ReadBitsAsync(string device, int count)
    {
        var result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            string key = IncrementDevice(device, i);
            result[i] = _bits.ContainsKey(key) && _bits[key];
        }
        return Task.FromResult(result);
    }

    public Task WriteWordsAsync(string device, int[] values)
    {
        for (int i = 0; i < values.Length; i++)
            _words[IncrementDevice(device, i)] = values[i];
        return Task.CompletedTask;
    }

    public Task WriteBitsAsync(string device, bool[] values)
    {
        for (int i = 0; i < values.Length; i++)
            _bits[IncrementDevice(device, i)] = values[i];
        return Task.CompletedTask;
    }

    /// <summary>비누 사용 시뮬레이션 (테스트용 외부 호출)</summary>
    public void SimulateSoapUse(int decreaseAmount = 50)
    {
        if (_words.ContainsKey("D0"))
        {
            _words["D0"] = Mathf.Max(0, _words["D0"] - decreaseAmount);
            _words["D10"]++;
            if (_words["D0"] <= 200) _bits["M10"] = true;  // 비누 알람
        }
    }

    // "D0" + offset → "D1", "D2" ...
    private string IncrementDevice(string device, int offset)
    {
        if (offset == 0) return device;
        string prefix = "";
        int number = 0;
        foreach (char c in device)
        {
            if (char.IsLetter(c)) prefix += c;
            else number = number * 10 + (c - '0');
        }
        return $"{prefix}{number + offset}";
    }
}
```

---

## 9-6. SLMPClient.cs — 실제 SLMP 통신

`Assets/Scripts/Network/SLMPClient.cs` 를 생성해줘:

```csharp
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 미쓰비시 SLMP 3E Frame (Binary) TCP 클라이언트.
/// iQ-R / iQ-F / Q / L 시리즈 대응.
/// </summary>
public class SLMPClient : IPLCClient
{
    private TcpClient  _tcp;
    private NetworkStream _stream;
    public bool IsConnected => _tcp != null && _tcp.Connected;

    // ── 연결 ──────────────────────────────────────────────────────────
    public async Task ConnectAsync(string ip, int port, int timeoutMs)
    {
        try
        {
            _tcp = new TcpClient();
            var connectTask = _tcp.ConnectAsync(ip, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
                throw new TimeoutException($"SLMP 연결 타임아웃: {ip}:{port}");

            _stream = _tcp.GetStream();
            Debug.Log($"[SLMP] 연결 성공: {ip}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SLMP] 연결 실패: {e.Message}");
            Disconnect();
            throw;
        }
    }

    public void Disconnect()
    {
        _stream?.Close();
        _tcp?.Close();
        _stream = null;
        _tcp    = null;
    }

    // ── 워드 읽기 (D 디바이스) ────────────────────────────────────────
    public async Task<int[]> ReadWordsAsync(string device, int count)
    {
        // 명령: 0x0401 (배치 읽기), 서브명령: 0x0000 (워드 단위)
        byte[] req = BuildReadRequest(device, count, 0x0401, 0x0000);
        byte[] res = await SendReceiveAsync(req);
        return ParseWordResponse(res, count);
    }

    // ── 비트 읽기 (M 디바이스) ────────────────────────────────────────
    public async Task<bool[]> ReadBitsAsync(string device, int count)
    {
        // 명령: 0x0401, 서브명령: 0x0001 (비트 단위)
        byte[] req = BuildReadRequest(device, count, 0x0401, 0x0001);
        byte[] res = await SendReceiveAsync(req);
        return ParseBitResponse(res, count);
    }

    // ── 워드 쓰기 ────────────────────────────────────────────────────
    public async Task WriteWordsAsync(string device, int[] values)
    {
        byte[] req = BuildWriteWordRequest(device, values);
        await SendReceiveAsync(req);
    }

    // ── 비트 쓰기 ────────────────────────────────────────────────────
    public async Task WriteBitsAsync(string device, bool[] values)
    {
        byte[] req = BuildWriteBitRequest(device, values);
        await SendReceiveAsync(req);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SLMP 3E Frame 패킷 빌더
    // ═══════════════════════════════════════════════════════════════════

    private byte[] BuildReadRequest(string device, int count, ushort cmd, ushort subCmd)
    {
        // 데이터부: 명령(2) + 서브명령(2) + 디바이스번호(3) + 디바이스코드(1) + 점수(2)
        byte[] data = new byte[10];
        data[0] = (byte)(cmd    & 0xFF); data[1] = (byte)(cmd    >> 8);
        data[2] = (byte)(subCmd & 0xFF); data[3] = (byte)(subCmd >> 8);

        int devNum = ParseDeviceNumber(device);
        data[4] = (byte)(devNum        & 0xFF);
        data[5] = (byte)((devNum >> 8) & 0xFF);
        data[6] = (byte)((devNum >>16) & 0xFF);
        data[7] = GetDeviceCode(device);
        data[8] = (byte)(count & 0xFF);
        data[9] = (byte)(count >> 8);

        return Wrap3EFrame(data);
    }

    private byte[] BuildWriteWordRequest(string device, int[] values)
    {
        int    dataLen = 10 + values.Length * 2;
        byte[] data    = new byte[dataLen];
        // 명령 0x1401, 서브명령 0x0000
        data[0] = 0x01; data[1] = 0x14;
        data[2] = 0x00; data[3] = 0x00;

        int devNum = ParseDeviceNumber(device);
        data[4] = (byte)(devNum        & 0xFF);
        data[5] = (byte)((devNum >> 8) & 0xFF);
        data[6] = (byte)((devNum >>16) & 0xFF);
        data[7] = GetDeviceCode(device);
        data[8] = (byte)(values.Length & 0xFF);
        data[9] = (byte)(values.Length >> 8);

        for (int i = 0; i < values.Length; i++)
        {
            data[10 + i*2]   = (byte)(values[i] & 0xFF);
            data[10 + i*2+1] = (byte)(values[i] >> 8);
        }
        return Wrap3EFrame(data);
    }

    private byte[] BuildWriteBitRequest(string device, bool[] values)
    {
        // 비트는 니블(4bit) 단위 — 2비트씩 1바이트로 패킹
        int    packed  = (values.Length + 1) / 2;
        byte[] data    = new byte[10 + packed];
        data[0] = 0x01; data[1] = 0x14;
        data[2] = 0x01; data[3] = 0x00;   // 서브명령 0x0001 (비트 단위)

        int devNum = ParseDeviceNumber(device);
        data[4] = (byte)(devNum        & 0xFF);
        data[5] = (byte)((devNum >> 8) & 0xFF);
        data[6] = (byte)((devNum >>16) & 0xFF);
        data[7] = GetDeviceCode(device);
        data[8] = (byte)(values.Length & 0xFF);
        data[9] = (byte)(values.Length >> 8);

        for (int i = 0; i < values.Length; i++)
        {
            int   byteIdx  = 10 + i / 2;
            int   nibble   = values[i] ? 0x10 : 0x00;
            if (i % 2 == 0) data[byteIdx] = (byte)(nibble);
            else            data[byteIdx] |= (byte)(nibble >> 4);
        }
        return Wrap3EFrame(data);
    }

    /// <summary>SLMP 3E Frame 헤더 래핑</summary>
    private byte[] Wrap3EFrame(byte[] dataBody)
    {
        // 헤더: 서브헤더(2) + 네트워크번호(1) + PC번호(1) + 요청처I/O(2) + 스테이션(1) + 데이터길이(2) + CPU타이머(2)
        ushort dataLen = (ushort)(dataBody.Length + 2); // CPU타이머 포함
        byte[] frame   = new byte[9 + dataBody.Length];

        frame[0] = 0x50; frame[1] = 0x00;   // 서브헤더 (3E)
        frame[2] = 0x00;                      // 네트워크 번호
        frame[3] = 0xFF;                      // PC 번호
        frame[4] = 0xFF; frame[5] = 0x03;    // 요청처 모듈 I/O
        frame[6] = 0x00;                      // 요청처 스테이션
        frame[7] = (byte)(dataLen & 0xFF);
        frame[8] = (byte)(dataLen >> 8);
        // CPU 타이머는 dataBody 첫 2바이트로 넣지 않고 별도 추가
        // 실제로는 frame[9~10] = CPU 타이머(0x000A), frame[11~] = 실제 데이터
        // 아래 재구성 버전 사용:
        byte[] full = new byte[11 + dataBody.Length];
        Array.Copy(frame, 0, full, 0, 9);
        full[9]  = 0x0A; full[10] = 0x00;   // CPU 타이머 10 (1초)
        Array.Copy(dataBody, 0, full, 11, dataBody.Length);
        return full;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 응답 파서
    // ═══════════════════════════════════════════════════════════════════

    private int[] ParseWordResponse(byte[] res, int count)
    {
        // 응답: 헤더(9) + 종료코드(2) + 데이터(count*2)
        var result = new int[count];
        int offset = 11; // 헤더(9) + 종료코드(2)
        for (int i = 0; i < count; i++)
            result[i] = res[offset + i*2] | (res[offset + i*2 + 1] << 8);
        return result;
    }

    private bool[] ParseBitResponse(byte[] res, int count)
    {
        // 비트 응답: 니블 단위 (1바이트에 2비트)
        var result = new bool[count];
        int offset = 11;
        for (int i = 0; i < count; i++)
        {
            int  byteVal = res[offset + i / 2];
            bool bit     = (i % 2 == 0) ? (byteVal & 0x10) != 0
                                         : (byteVal & 0x01) != 0;
            result[i] = bit;
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    // TCP 송수신
    // ═══════════════════════════════════════════════════════════════════

    private async Task<byte[]> SendReceiveAsync(byte[] request)
    {
        if (!IsConnected) throw new InvalidOperationException("SLMP 미연결 상태");

        await _stream.WriteAsync(request, 0, request.Length);

        byte[] buf = new byte[512];
        int    len = await _stream.ReadAsync(buf, 0, buf.Length);

        if (len < 11)
            throw new Exception($"SLMP 응답 너무 짧음: {len} bytes");

        // 종료코드 확인 (0x0000 = 정상)
        int endCode = buf[9] | (buf[10] << 8);
        if (endCode != 0x0000)
            throw new Exception($"SLMP 오류 코드: 0x{endCode:X4}");

        return buf;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 헬퍼
    // ═══════════════════════════════════════════════════════════════════

    private int ParseDeviceNumber(string device)
    {
        // "D0" → 0,  "M10" → 10,  "D100" → 100
        string numStr = "";
        foreach (char c in device)
            if (char.IsDigit(c)) numStr += c;
        return numStr.Length > 0 ? int.Parse(numStr) : 0;
    }

    private byte GetDeviceCode(string device)
    {
        // SLMP Binary 디바이스 코드표 (주요 디바이스)
        char prefix = char.ToUpper(device[0]);
        return prefix switch
        {
            'D' => 0xA8,   // 데이터 레지스터
            'M' => 0x90,   // 내부 릴레이
            'X' => 0x9C,   // 입력
            'Y' => 0x9D,   // 출력
            'R' => 0xAF,   // 파일 레지스터
            'W' => 0xB4,   // 링크 레지스터
            _   => 0xA8,
        };
    }
}
```

---

## 9-7. NetworkManager.cs — 폴링 루프 및 연결 관리

`Assets/Scripts/Network/NetworkManager.cs` 를 생성해줘:

```csharp
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

    // ── 설정 (PLCConfig.json에서 로드) ───────────────────────────────
    private PLCConfig   _config;
    private IPLCClient  _client;

    // ── 상태 ─────────────────────────────────────────────────────────
    public bool  IsConnected   => _client != null && _client.IsConnected;
    public string StatusMessage { get; private set; } = "초기화 중...";

    public event Action<bool>   OnConnectionChanged;
    public event Action<string> OnStatusChanged;

    // ── 생명주기 ─────────────────────────────────────────────────────

    void Start()
    {
        LoadConfig();
        StartCoroutine(ConnectionLoop());
    }

    void OnDestroy() => _client?.Disconnect();

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

        _client = _config.useMock
            ? (IPLCClient)new MockPLCClient()
            : (IPLCClient)new SLMPClient();
    }

    // ── 연결 루프 ────────────────────────────────────────────────────

    private IEnumerator ConnectionLoop()
    {
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
            // StationController와 이벤트로 연동 (필요 시 확장)
            _ = bits; // 현재는 읽기만, 추후 확장 지점
        }

        yield return new WaitForSeconds(_config.pollIntervalMs / 1000f);
    }

    // ── HMI → PLC 쓰기 (버튼 클릭 시 외부에서 호출) ─────────────────

    public void WriteSoapButton(bool value)  => StartCoroutine(WritebitCoroutine(_config.devices.soapBtn,  value));
    public void WriteWaterButton(bool value) => StartCoroutine(WritebitCoroutine(_config.devices.waterBtn, value));
    public void WriteAirButton(bool value)   => StartCoroutine(WritebitCoroutine(_config.devices.airBtn,   value));

    private IEnumerator WritebitCoroutine(string device, bool value)
    {
        if (!IsConnected) yield break;
        var task = _client.WriteBitsAsync(device, new[] { value });
        yield return new WaitUntil(() => task.IsCompleted);
        if (task.IsFaulted)
            Debug.LogWarning($"[Network] 비트 쓰기 실패: {device} = {value}");
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────

    private void UpdateSystemStatus(float soapPct)
    {
        if      (soapPct <= 0f)  stationData.systemStatus = StationData.SystemStatus.Error;
        else if (soapPct <= 20f) stationData.systemStatus = StationData.SystemStatus.Warning;
        else                     stationData.systemStatus = StationData.SystemStatus.Normal;
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
            mock.SimulateSoapUse(50);
        else
            Debug.LogWarning("Mock 모드가 아닙니다.");
    }
}
```

---

## 9-8. HMIUIController.cs 수정 — NetworkManager 연동 추가

기존 `HMIUIController.cs` 에 아래 내용을 추가해줘:

### 필드 추가 (기존 필드 선언부 아래에)

```csharp
[Header("Network")]
public NetworkManager networkManager;   // Inspector에서 연결

private VisualElement _networkLed;      // 헤더 네트워크 상태 표시용 (선택)
```

### Start() 수정 — 이벤트 구독 추가

```csharp
// 기존 Start() 마지막에 추가
if (networkManager != null)
{
    networkManager.OnConnectionChanged += OnNetworkConnectionChanged;
    networkManager.OnStatusChanged     += OnNetworkStatusChanged;
}
```

### 콜백 메서드 추가

```csharp
private void OnNetworkConnectionChanged(bool connected)
{
    // 연결 상태를 헤더 시스템 LED에 반영
    // connected=false 이면 통신 오류로 처리
    if (!connected)
    {
        _statusLed.AddToClassList("warning");
        _statusText.text = "시스템 상태: 통신 오류";
    }
    else
    {
        UpdateStatusLED(stationController.stationData.systemStatus);
    }
}

private void OnNetworkStatusChanged(string msg)
{
    // 필요 시 화면 하단 등에 상태 메시지 표시
    Debug.Log($"[HMI] 네트워크 상태: {msg}");
}
```

### StationController 버튼 이벤트에 PLC 쓰기 추가

기존 버튼 콜백을 아래처럼 수정해줘:

```csharp
_btnSoap.RegisterCallback<ClickEvent>(_ =>
{
    stationController.ActivateSoap();
    networkManager?.WriteSoapButton(true);
    // 3초 후 자동 OFF — StationController 코루틴 종료 시 이미 처리됨
});

_btnWater.RegisterCallback<ClickEvent>(_ =>
{
    stationController.ActivateWater();
    networkManager?.WriteWaterButton(true);
});

_btnAir.RegisterCallback<ClickEvent>(_ =>
{
    stationController.ActivateAir();
    networkManager?.WriteAirButton(true);
});
```

### OnDestroy() 수정 — 구독 해제 추가

```csharp
// 기존 OnDestroy() 에 추가
if (networkManager != null)
{
    networkManager.OnConnectionChanged -= OnNetworkConnectionChanged;
    networkManager.OnStatusChanged     -= OnNetworkStatusChanged;
}
```

---

## 9-9. 씬 조립

1. `StationManager` GameObject에 `NetworkManager.cs` 컴포넌트 추가
2. `NetworkManager` Inspector 에서:
   - `Station Data` → `StationDataInstance.asset` 연결
3. `UIRoot` GameObject의 `HMIUIController` Inspector 에서:
   - `Network Manager` → `StationManager` 연결

---

## 9-10. 동작 테스트 — Mock 모드

`PLCConfig.json` 의 `"useMock": true` 상태에서:

1. Unity Play 모드 진입
2. `StationManager` 선택 → Inspector 우클릭 → **"Mock: 비누 사용 시뮬레이션"** 클릭
3. 비누 게이지가 5% 감소, 20% 이하 시 헤더 LED 주황색 전환 확인
4. Console 에서 `[Network] 가상 PLC 연결됨` 로그 확인

---

## 9-11. Python 가상 PLC 서버 — 실제 TCP 통신 검증

PLC 없이 진짜 TCP 소켓 통신까지 검증하려면 아래 Python 스크립트를 PC에서 실행해줘.  
Unity와 **같은 PC**에서 실행하면 `127.0.0.1:5007` 로 연결된다.

`fake_plc_server.py` 파일을 프로젝트 루트(Assets 밖)에 생성해줘:

```python
import socket
import struct

HOST = '127.0.0.1'
PORT = 5007

# 가상 PLC 메모리
D = {0: 1000, 10: 0}   # D0=비누잔량(1000=100%), D10=사용횟수
M = {0: 0, 1: 0, 2: 0, 10: 0, 11: 0}

def make_response(data_bytes):
    """SLMP 3E Frame 응답 래핑"""
    data_len = len(data_bytes) + 2   # 종료코드 포함
    header = bytes([
        0xD0, 0x00,              # 서브헤더 (응답)
        0x00,                    # 네트워크 번호
        0xFF,                    # PC 번호
        0xFF, 0x03,              # 응답처 I/O
        0x00,                    # 스테이션
        data_len & 0xFF,
        (data_len >> 8) & 0xFF,
        0x00, 0x00,              # 종료코드 (정상)
    ])
    return header + data_bytes

def handle_read_word(dev_num, count):
    """워드 읽기 응답"""
    data = b''
    for i in range(count):
        val = D.get(dev_num + i, 0)
        data += struct.pack('<H', val)
    return make_response(data)

def handle_read_bit(dev_num, count):
    """비트 읽기 응답 (니블 패킹)"""
    data = b''
    for i in range(0, count, 2):
        b0 = 0x10 if M.get(dev_num + i, 0) else 0x00
        b1 = 0x01 if M.get(dev_num + i + 1, 0) else 0x00
        data += bytes([b0 | b1])
    return make_response(data)

server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
server.bind((HOST, PORT))
server.listen(1)
print(f"가상 PLC 서버 대기 중: {HOST}:{PORT}")

while True:
    conn, addr = server.accept()
    print(f"연결됨: {addr}")
    try:
        while True:
            data = conn.recv(1024)
            if not data:
                break

            # 명령어 파싱 (바이트 11~12: 명령, 13~14: 서브명령)
            if len(data) < 15:
                continue

            cmd     = data[11] | (data[12] << 8)
            sub_cmd = data[13] | (data[14] << 8)
            dev_num = data[15] | (data[16] << 8) | (data[17] << 16)
            count   = data[19] | (data[20] << 8)

            if cmd == 0x0401 and sub_cmd == 0x0000:
                # 워드 읽기
                resp = handle_read_word(dev_num, count)
                print(f"  워드 읽기 D{dev_num} x{count} → {[D.get(dev_num+i,0) for i in range(count)]}")
            elif cmd == 0x0401 and sub_cmd == 0x0001:
                # 비트 읽기
                resp = handle_read_bit(dev_num, count)
                print(f"  비트 읽기 M{dev_num} x{count}")
            elif cmd == 0x1401:
                # 쓰기 (응답만 반환)
                resp = make_response(b'')
                print(f"  쓰기 명령 수신 D/M{dev_num}")
            else:
                resp = make_response(b'')

            conn.send(resp)
    except Exception as e:
        print(f"오류: {e}")
    finally:
        conn.close()
        print("클라이언트 연결 종료")
```

### Python 서버 사용법

```bash
# 터미널에서 실행
python fake_plc_server.py

# 출력 예시:
# 가상 PLC 서버 대기 중: 127.0.0.1:5007
# 연결됨: ('127.0.0.1', 54231)
#   워드 읽기 D0 x1 → [1000]
#   비트 읽기 M0 x3
```

### PLCConfig.json 수정 (Python 서버 테스트용)

```json
{
  "useMock": false,
  "ip": "127.0.0.1",
  "port": 5007,
  ...
}
```

이 상태로 Unity Play → 실제 TCP 패킷이 Python 서버로 전달되고 응답 파싱까지 전부 검증된다.

---

## 9-12. PLC 실물 도착 후 전환 절차

코드 수정 없이 아래 순서만 따르면 된다:

```
1. GX Works3 설정
   CPU 파라미터 → Built-in Ethernet Port
   → SLMP 통신: 활성화
   → 포트: 5007
   → 통신 데이터 코드: Binary

2. PLCConfig.json 수정
   "useMock": false
   "ip": "192.168.x.x"   ← PLC 실제 IP

3. 디바이스 주소 확인 후 devices 항목 수정
   (전장 설계 완료 후 GX Works3 심볼 테이블 참고)

4. Unity Play → Console에서 "[SLMP] 연결 성공" 확인
```

---

## 동작 확인 체크리스트

| 단계 | 확인 항목 |
|------|-----------|
| Mock 모드 | Console: `[Network] 가상 PLC 연결됨` |
| Mock 시뮬레이션 | ContextMenu 클릭 → 게이지 감소 확인 |
| Python 서버 | Console: `[SLMP] 연결 성공: 127.0.0.1:5007` |
| Python 서버 | Python 터미널에서 읽기/쓰기 로그 출력 확인 |
| 비누 20% 이하 | 헤더 LED 주황색 전환 |
| 비누 0% | 헤더 LED 빨간색, 비누 버튼 비활성 |
| 통신 오류 시 | 헤더 LED 주황색, "시스템 상태: 통신 오류" 표시 |
| PLC 실물 연결 | `PLCConfig.json` 수정만으로 전환, 코드 변경 없음 |
