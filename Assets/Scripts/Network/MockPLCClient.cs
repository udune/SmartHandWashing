using System.Collections.Generic;
using System.Threading.Tasks;
using Network;
using UnityEngine;

/// <summary>
/// PLC 없이 동작하는 가상 클라이언트.
/// 새 래더 구조: 4개 모드 선택 + 8단계 시퀀스 시뮬레이션
/// </summary>
public class MockPLCClient : IPLCClient
{
    public bool IsConnected => true;

    // 시뮬레이션 동작 시간 (밀리초)
    private const int SoapDurationMS = 5000;      // 비누: 5초
    private const int WaterWaitDurationMS = 2000; // 물 대기: 2초
    private const int WaterDurationMS = 8000;     // 물: 8초
    private const int DryDurationMS = 8000;       // 건조: 8초
    private const int RinseWaitDurationMS = 2000; // 헹굼 대기: 2초
    private const int RinseDurationMS = 5000;     // 헹굼: 5초

    // D 디바이스 (워드)
    private readonly Dictionary<string, int> _words = new Dictionary<string, int>
    {
        { "D0", 1000 },   // 비누 잔량 (0~1000 = 0.0~100.0%)
        { "D10", 0   },   // 누적 사용 횟수
    };

    // M 디바이스 (비트) — 새 래더 구조
    private readonly Dictionary<string, bool> _bits = new Dictionary<string, bool>
    {
        // ── 동작 상태 (8단계 시퀀스) ──
        { "M0",  false },   // 1단계: 비누 동작
        { "M1",  false },   // 2단계: 물 대기
        { "M2",  false },   // 3단계: 물 동작
        { "M3",  false },   // 4단계: 건조 동작
        { "M4",  false },   // 5단계: 헹굼 대기
        { "M5",  false },   // 6단계: 헹굼 동작
        { "M6",  false },   // 7단계: 비누2 동작
        { "M7",  false },   // 8단계: 건조2 동작

        // ── 모드 상태 ──
        { "M10", false },   // 비누 모드
        { "M20", false },   // 물 모드
        { "M30", false },   // 수동 모드
        { "M40", false },   // 건조 모드

        // ── X 디바이스 (HMI → PLC 쓰기용) ──
        { "X0A0", false },  // 손 센서
        { "X0A1", false },  // 전진단 센서
        { "X0A6", false },  // 건조 선택 버튼
        { "X0A7", false },  // 비누 선택 버튼
        { "X0A8", false },  // 물 선택 버튼
        { "X0A9", false },  // 수동 선택 버튼

        // ── 카운터 (비트로 시뮬레이션) ──
        { "C0", false },    // 헹굼 카운터 완료
    };

    public Task ConnectAsync(string ip, int port, int timeoutMs)
    {
        Debug.Log("[MockPLC] 가상 PLC 연결됨 (새 래더 구조)");
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
        {
            _words[IncrementDevice(device, i)] = values[i];
        }
        return Task.CompletedTask;
    }

    public Task WriteBitsAsync(string device, bool[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            string key = IncrementDevice(device, i);
            bool prevValue = _bits.ContainsKey(key) && _bits[key];

            // 상승 에지: OFF → ON 전환 시 모드 선택 처리
            if (values[i] && !prevValue)
            {
                switch (key)
                {
                    case "X0A7":  // 비누 모드 선택
                        SetModeExclusive("M10");
                        break;
                    case "X0A8":  // 물 모드 선택
                        SetModeExclusive("M20");
                        break;
                    case "X0A9":  // 수동 모드 선택
                        SetModeExclusive("M30");
                        break;
                    case "X0A6":  // 건조 모드 선택
                        SetModeExclusive("M40");
                        break;
                    case "X0A0":  // 손 센서 트리거
                        _ = SimulateSensorSequence();
                        break;
                    default:
                        _bits[key] = values[i];
                        break;
                }
            }
            else
            {
                if (_bits.ContainsKey(key))
                    _bits[key] = values[i];
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>모드 배타적 선택 (하나만 ON)</summary>
    private void SetModeExclusive(string modeKey)
    {
        _bits["M10"] = modeKey == "M10";
        _bits["M20"] = modeKey == "M20";
        _bits["M30"] = modeKey == "M30";
        _bits["M40"] = modeKey == "M40";
        Debug.Log($"[MockPLC] 모드 변경: {modeKey}");
    }

    /// <summary>외부에서 비트 직접 설정 (테스트용)</summary>
    public void SetBit(string key, bool value)
    {
        if (_bits.ContainsKey(key))
        {
            _bits[key] = value;
        }
        else
        {
            _bits[key] = value;
        }
    }

    /// <summary>비트 값 조회 (테스트용)</summary>
    public bool GetBit(string key)
    {
        return _bits.ContainsKey(key) && _bits[key];
    }

    /// <summary>8단계 시퀀스 시뮬레이션 (손 센서 감지 시)</summary>
    public async Task SimulateSensorSequence()
    {
        Debug.Log("[MockPLC] 손 센서 감지! 8단계 시퀀스 시작...");

        // 1단계: 비누
        _bits["M0"] = true;
        SimulateSoapUse();
        Debug.Log("[MockPLC] 1단계: 비누 동작");
        await Task.Delay(SoapDurationMS);
        _bits["M0"] = false;

        // 2단계: 물 대기
        _bits["M1"] = true;
        Debug.Log("[MockPLC] 2단계: 물 대기");
        await Task.Delay(WaterWaitDurationMS);
        _bits["M1"] = false;

        // 3단계: 물
        _bits["M2"] = true;
        Debug.Log("[MockPLC] 3단계: 물 동작");
        await Task.Delay(WaterDurationMS);
        _bits["M2"] = false;

        // 4단계: 건조
        _bits["M3"] = true;
        Debug.Log("[MockPLC] 4단계: 건조 동작");
        await Task.Delay(DryDurationMS);
        _bits["M3"] = false;

        // 5단계: 헹굼 대기
        _bits["M4"] = true;
        Debug.Log("[MockPLC] 5단계: 헹굼 대기");
        await Task.Delay(RinseWaitDurationMS);
        _bits["M4"] = false;

        // 6단계: 헹굼
        _bits["M5"] = true;
        Debug.Log("[MockPLC] 6단계: 헹굼 동작");
        await Task.Delay(RinseDurationMS);
        _bits["M5"] = false;

        // 7단계: 비누2
        _bits["M6"] = true;
        SimulateSoapUse();
        Debug.Log("[MockPLC] 7단계: 비누2 동작");
        await Task.Delay(SoapDurationMS);
        _bits["M6"] = false;

        // 8단계: 건조2
        _bits["M7"] = true;
        Debug.Log("[MockPLC] 8단계: 건조2 동작");
        await Task.Delay(DryDurationMS);
        _bits["M7"] = false;

        Debug.Log("[MockPLC] 8단계 시퀀스 완료!");
    }

    /// <summary>비누 사용 시뮬레이션 (잔량 감소)</summary>
    public void SimulateSoapUse(int decreaseAmount = 50)
    {
        if (_words.ContainsKey("D0"))
        {
            _words["D0"] = Mathf.Max(0, _words["D0"] - decreaseAmount);
            _words["D10"]++;
            Debug.Log($"[MockPLC] 비누 사용: 잔량 {_words["D0"] / 10f}%");
        }
    }

    /// <summary>비누 잔량 리셋</summary>
    public void ResetSoapLevel(int value = 1000)
    {
        _words["D0"] = Mathf.Clamp(value, 0, 1000);
        Debug.Log($"[MockPLC] 비누 잔량 리셋: {_words["D0"] / 10f}%");
    }

    /// <summary>모든 동작 신호 리셋</summary>
    public void ResetAllSequence()
    {
        for (int i = 0; i <= 7; i++)
        {
            _bits[$"M{i}"] = false;
        }
        Debug.Log("[MockPLC] 모든 시퀀스 신호 리셋");
    }

    /// <summary>디바이스 주소 증가 (M0 + 1 = M1)</summary>
    private string IncrementDevice(string device, int offset)
    {
        if (offset == 0)
        {
            return device;
        }

        // X0A0 같은 16진수 주소 처리
        if (device.StartsWith("X") || device.StartsWith("Y"))
        {
            string prefix = device.Substring(0, 1);
            string hexPart = device.Substring(1);
            if (int.TryParse(hexPart, System.Globalization.NumberStyles.HexNumber, null, out int num))
            {
                return $"{prefix}{(num + offset):X}";
            }
        }

        // M0, D0 같은 10진수 주소 처리
        string letterPrefix = "";
        int number = 0;
        foreach (char c in device)
        {
            if (char.IsLetter(c))
            {
                letterPrefix += c;
            }
            else
            {
                number = number * 10 + (c - '0');
            }
        }
        return $"{letterPrefix}{number + offset}";
    }
}
