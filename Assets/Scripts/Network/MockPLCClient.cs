using System.Collections.Generic;
using System.Threading.Tasks;
using Network;
using UnityEngine;

/// <summary>
/// PLC 없이 동작하는 가상 클라이언트.
/// 내부 딕셔너리로 D/M 디바이스를 시뮬레이션한다.
///
/// PLC 동작 시뮬레이션:
///   - HMI에서 버튼 신호(M0~M2) 수신 → 상승 에지 감지
///   - 해당 디바이스를 ON 유지 (설정된 시간 동안)
///   - 시간 종료 후 자동 OFF
/// </summary>
public class MockPLCClient : IPLCClient
{
    public bool IsConnected => true;

    // 시뮬레이션 동작 시간 (밀리초)
    private const int SoapDurationMS = 5000;   // 비누: 5초
    private const int WaterDurationMS = 8000;  // 물: 8초
    private const int AirDurationMS = 8000;    // 에어: 8초

    // D 디바이스 (워드)
    private readonly Dictionary<string, int> _words = new Dictionary<string, int>
    {
        { "D0", 1000 },   // 비누 잔량 (0~1000 = 0.0~100.0%)
        { "D1", 400  },   // 비누 질하는 대기시간
        { "D2", 1000 },   // 물 나오는 시간
        { "D3", 1000 },   // 건조 시간
        { "D4", 200  },   // 손 센서 대기 시간
        { "D10", 0   },   // 누적 사용 횟수
    };

    // M 디바이스 (비트) — 자동 모드
    private readonly Dictionary<string, bool> _bits = new Dictionary<string, bool>
    {
        // 자동 모드 상태
        { "M0",  false },   // 비누 실린더 전진
        { "M4",  false },   // 물 모터
        { "M5",  false },   // 건조 모터
        { "M6",  false },   // 자동 종료
        { "M13", false },   // 자동 모드

        // 수동 모드 상태
        { "M7",  false },   // 비누 (수동)
        { "M10", false },   // 수동 모드
        { "M11", false },   // 물 (수동)
        { "M12", false },   // 건조 (수동)

        // X 디바이스 (HMI → PLC 쓰기용)
        { "X0A0", false },  // 손 센서
        { "X0A3", false },  // 비누 PB
        { "X0A4", false },  // 물 PB
        { "X0A5", false },  // 건조 PB
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

            // 상승 에지: OFF → ON 전환 시 PLC 동작 시뮬레이션
            if (values[i] && !prevValue)
            {
                // 인터록: M0/M1/M2 중 하나라도 동작 중이면 무시 (실제 PLC 래더 로직 시뮬레이션)
                if (key == "M0" || key == "M1" || key == "M2")
                {
                    if (IsAnyDispenserRunning())
                    {
                        Debug.Log($"[MockPLC] {key} 무시 (인터록: 다른 디스펜서 동작 중)");
                        continue;
                    }
                }

                switch (key)
                {
                    case "M0":
                        // 비누: ON 유지 + 잔량 감소 + 일정 시간 후 OFF
                        _bits["M0"] = true;
                        SimulateSoapUse();
                        _ = DelayedReset("M0", SoapDurationMS);
                        Debug.Log($"[MockPLC] M0 ON → {SoapDurationMS}ms 후 OFF");
                        break;

                    case "M1":
                        // 물: ON 유지 + 일정 시간 후 OFF
                        _bits["M1"] = true;
                        _ = DelayedReset("M1", WaterDurationMS);
                        Debug.Log($"[MockPLC] M1 ON → {WaterDurationMS}ms 후 OFF");
                        break;

                    case "M2":
                        // 에어: ON 유지 + 일정 시간 후 OFF
                        _bits["M2"] = true;
                        _ = DelayedReset("M2", AirDurationMS);
                        Debug.Log($"[MockPLC] M2 ON → {AirDurationMS}ms 후 OFF");
                        break;

                    default:
                        _bits[key] = values[i];
                        break;
                }
            }
            else if (!values[i])
            {
                // OFF 신호는 그대로 반영 (수동 중단 시)
                _bits[key] = false;
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>디스펜서(M0/M1/M2) 중 하나라도 동작 중인지 확인</summary>
    private bool IsAnyDispenserRunning()
    {
        return (_bits.ContainsKey("M0") && _bits["M0"]) ||
               (_bits.ContainsKey("M1") && _bits["M1"]) ||
               (_bits.ContainsKey("M2") && _bits["M2"]);
    }

    /// <summary>지정된 시간 후 비트를 OFF로 설정 (PLC 타이머 시뮬레이션)</summary>
    private async Task DelayedReset(string key, int delayMs)
    {
        await Task.Delay(delayMs);
        if (_bits.ContainsKey(key))
        {
            _bits[key] = false;
            Debug.Log($"[MockPLC] {key} OFF (타이머 종료)");
        }
    }

    /// <summary>Mock 테스트용 — 자동 모드 비누 사용 시뮬레이션</summary>
    public void SimulateSoapUse(int decreaseAmount = 50)
    {
        if (_words.ContainsKey("D0"))
        {
            _words["D0"] = Mathf.Max(0, _words["D0"] - decreaseAmount);
            _words["D10"]++;

            // 비누 동작 신호 잠깐 ON (자동 모드 기준 M0)
            _bits["M13"] = true;   // 자동 모드
            _bits["M0"]  = true;   // 비누 동작
        }
    }

    /// <summary>Mock 테스트용 — 신호 리셋</summary>
    public void SimulateSignalReset()
    {
        _bits["M0"]  = false;
        _bits["M4"]  = false;
        _bits["M5"]  = false;
        _bits["M6"]  = false;
    }

    public void ResetSoapLevel(int value = 1000)
    {
        _words["D0"] = Mathf.Clamp(value, 0, 1000);
        _bits["M10"] = _words["D0"] <= 200;
    }

    public void SetButtonBit(int index, bool value)
    {
        string key = $"M{index}";
        if (_bits.ContainsKey(key))
        {
            _bits[key] = value;
        }
    }

    /// <summary>센서 동작 시뮬레이션 (수동 트리거용)</summary>
    public async Task SimulateSensorSequence()
    {
        Debug.Log("[MockPLC] 센서 감지! 순차 동작 시작...");
        
        // 1. 비누 동작
        await WriteBitsAsync("M0", new[] { true });
        
        // 비누가 끝날 때까지 대기
        while (_bits.ContainsKey("M0") && _bits["M0"])
        {
            await Task.Delay(100);
        }
        await Task.Delay(500); // 다음 동작 전 짧은 대기 시간
        
        // 2. 물 동작
        await WriteBitsAsync("M1", new[] { true });
        
        while (_bits.ContainsKey("M1") && _bits["M1"])
        {
            await Task.Delay(100);
        }
        await Task.Delay(500);
        
        // 3. 에어 동작
        await WriteBitsAsync("M2", new[] { true });
        
        while (_bits.ContainsKey("M2") && _bits["M2"])
        {
            await Task.Delay(100);
        }
        
        Debug.Log("[MockPLC] 순차 동작 시뮬레이션 완료.");
    }

    private string IncrementDevice(string device, int offset)
    {
        if (offset == 0)
        {
            return device;
        }

        string prefix = "";
        int number = 0;
        foreach (char c in device)
        {
            if (char.IsLetter(c))
            {
                prefix += c;
            }
            else
            {
                number = number * 10 + (c - '0');
            }
        }
        return $"{prefix}{number + offset}";
    }
}