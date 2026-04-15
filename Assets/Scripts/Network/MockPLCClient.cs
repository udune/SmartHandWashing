using System.Collections.Generic;
using System.Threading.Tasks;
using Network;
using UnityEngine;

/// <summary>
/// PLC 없이 동작하는 가상 클라이언트.
///
/// M 릴레이 구조 (스펙 §2):
///   M0  수동 물 출수       M4  자동 세정제 전진
///   M1  수동 세정제 전진   M5  자동 세정제 후진
///   M2  수동 세정제 후진   M6  자동 물
///   M3  수동 바람          M7  자동 바람
///   M10 수동 물 모드       M20 수동 세정제 모드
///   M30 수동 바람 모드     M40 자동 모드
/// </summary>
public class MockPLCClient : IPLCClient
{
    public bool IsConnected
    {
        get { return true; }
    }

    // ── 시뮬레이션 시간 (ms) ─────────────────────────────────────────
    private const int T0_MS           = 2000;   // T0: 2초 대기
    private const int WaterDurationMS = 10000;  // T1/T3: 물 10초
    private const int AirDurationMS   = 10000;  // T2/T4: 바람 10초
    private const int CylinderTravelMS = 500;   // 실린더 이동 시간

    // ── D 디바이스 (워드) ─────────────────────────────────────────────
    private readonly Dictionary<string, int> _words = new Dictionary<string, int>
    {
        { "D0",  1000 },  // 비누 잔량 (0~1000 = 0.0~100.0%)
        { "D10", 0    },  // 누적 사용 횟수
    };

    // ── M/X/C 디바이스 (비트) ────────────────────────────────────────
    private readonly Dictionary<string, bool> _bits = new Dictionary<string, bool>
    {
        // X 입력 센서 / 버튼
        { "X0A0", true  },   // 세정제 후진 완료 센서 (초기 위치 = true)
        { "X0A1", false },   // 세정제 전진 완료 센서
        { "X0A6", false },   // 손 인식 센서
        { "X0A7", false },   // 물 스위치
        { "X0A8", false },   // 세정제 스위치
        { "X0A9", false },   // 바람 스위치

        // 수동 동작 릴레이
        { "M0",  false },    // 수동 물 출수
        { "M1",  false },    // 수동 세정제 전진
        { "M2",  false },    // 수동 세정제 후진
        { "M3",  false },    // 수동 바람

        // 자동 동작 릴레이
        { "M4",  false },    // 자동 세정제 전진
        { "M5",  false },    // 자동 세정제 후진
        { "M6",  false },    // 자동 물
        { "M7",  false },    // 자동 바람

        // 모드 선택 릴레이
        { "M10", false },    // 수동 물 모드
        { "M20", false },    // 수동 세정제 모드
        { "M30", false },    // 수동 바람 모드
        { "M40", false },    // 자동 모드

        // 카운터 접점
        { "C0",  false },    // 자동 세정제 왕복 완료 (2회)
    };

    // ── IPLCClient 구현 ───────────────────────────────────────────────

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
            result[i] = _words.TryGetValue(IncrementDevice(device, i), out int v) ? v : 0;
        }
        return Task.FromResult(result);
    }

    public Task<bool[]> ReadBitsAsync(string device, int count)
    {
        var result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            string key = IncrementDevice(device, i);
            result[i] = _bits.TryGetValue(key, out bool v) && v;
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
            string key   = IncrementDevice(device, i);
            bool   prev  = _bits.TryGetValue(key, out bool pv) && pv;
            bool   next  = values[i];

            _bits[key] = next;

            // 상승엣지: OFF → ON 전환 시 래더 로직 시뮬레이션 트리거
            if (next && !prev)
            {
                switch (key)
                {
                    case "X0A7":  // 물 스위치 → 수동 물 모드 (§Line 2: NOT M20, NOT M30, NOT M40)
                        if (!_bits["M10"] && !_bits["M20"] && !_bits["M30"] && !_bits["M40"])
                        {
                            _ = SimulateManualWater();
                        }
                        break;

                    case "X0A8":  // 세정제 스위치 → 수동 세정제 모드 (§Line 3: NOT M10, NOT M30, NOT M40, NOT X0A0)
                        if (!_bits["M10"] && !_bits["M20"] && !_bits["M30"] && !_bits["M40"] && !_bits["X0A0"])
                        {
                            _ = SimulateManualSoap();
                        }
                        break;

                    case "X0A9":  // 바람 스위치 → 수동 바람 모드 (§Line 4: NOT M10, NOT M20, NOT M40)
                        if (!_bits["M10"] && !_bits["M20"] && !_bits["M30"] && !_bits["M40"])
                        {
                            _ = SimulateManualAir();
                        }
                        break;

                    case "X0A6":  // 손 인식 센서 → 자동 모드 (§Line 5: NOT M10~M30, X0A0)
                        if (!_bits["M10"] && !_bits["M20"] && !_bits["M30"] && !_bits["M40"] && _bits["X0A0"])
                        {
                            _ = SimulateSensorSequence();
                        }
                        break;
                }
            }
        }
        return Task.CompletedTask;
    }

    // ── 수동 모드 시뮬레이션 ─────────────────────────────────────────

    /// <summary>수동 물 시퀀스: M10 SET → T0(2s) → M0 ON → T1(10s) → 리셋</summary>
    private async Task SimulateManualWater()
    {
        Debug.Log("[MockPLC] 수동 물 모드 시작 (M10)");
        _bits["M10"] = true;
        await Task.Delay(T0_MS);

        _bits["M0"] = true;
        Debug.Log("[MockPLC] 물 출수 시작 (M0)");
        await Task.Delay(WaterDurationMS);

        _bits["M0"]  = false;
        _bits["M10"] = false;   // M0 하강엣지 → M10 RST
        Debug.Log("[MockPLC] 수동 물 완료");
    }

    /// <summary>수동 세정제 시퀀스: M20 SET → T0(2s) → 전진/후진 1회 → 리셋</summary>
    private async Task SimulateManualSoap()
    {
        Debug.Log("[MockPLC] 수동 세정제 모드 시작 (M20)");
        _bits["M20"] = true;
        await Task.Delay(T0_MS);

        // 전진 (M1)
        _bits["X0A0"] = false;
        _bits["M1"]   = true;
        SimulateSoapUse();
        Debug.Log("[MockPLC] 세정제 전진 (M1)");
        await Task.Delay(CylinderTravelMS);
        _bits["X0A1"] = true;   // 전진 완료 센서

        // 후진 (M2)
        _bits["M1"]   = false;
        _bits["M2"]   = true;
        _bits["X0A1"] = false;
        Debug.Log("[MockPLC] 세정제 후진 (M2)");
        await Task.Delay(CylinderTravelMS);
        _bits["X0A0"] = true;   // 후진 완료 센서 (초기 위치 복귀)

        _bits["M2"]   = false;
        _bits["M20"]  = false;  // M2 하강엣지 → M20 RST
        Debug.Log("[MockPLC] 수동 세정제 완료");
    }

    /// <summary>수동 바람 시퀀스: M30 SET → T0(2s) → M3 ON → T2(10s) → 리셋</summary>
    private async Task SimulateManualAir()
    {
        Debug.Log("[MockPLC] 수동 바람 모드 시작 (M30)");
        _bits["M30"] = true;
        await Task.Delay(T0_MS);

        _bits["M3"] = true;
        Debug.Log("[MockPLC] 바람 출력 시작 (M3)");
        await Task.Delay(AirDurationMS);

        _bits["M3"]  = false;
        _bits["M30"] = false;  // M3 하강엣지 → M30 RST
        Debug.Log("[MockPLC] 수동 바람 완료");
    }

    // ── 자동 사이클 시뮬레이션 ───────────────────────────────────────

    /// <summary>
    /// 자동 사이클: M40 SET → T0(2s) → 세정제 2회 왕복 → 물 10s → 바람 10s → 전체 리셋
    /// (손 인식 센서 X0A6 상승엣지 트리거)
    /// </summary>
    public async Task SimulateSensorSequence()
    {
        Debug.Log("[MockPLC] 자동 모드 시작 (M40 SET)");
        _bits["M40"] = true;
        await Task.Delay(T0_MS);  // T0: 2초 대기

        // 세정제 2회 왕복
        for (int pass = 1; pass <= 2; pass++)
        {
            // 전진 (M4)
            _bits["X0A0"] = false;
            _bits["M4"]   = true;
            SimulateSoapUse();
            Debug.Log($"[MockPLC] 자동 세정제 {pass}회차 전진 (M4)");
            await Task.Delay(CylinderTravelMS);
            _bits["X0A1"] = true;   // 전진 완료

            // 후진 (M5)
            _bits["M4"]   = false;
            _bits["M5"]   = true;
            _bits["X0A1"] = false;
            Debug.Log($"[MockPLC] 자동 세정제 {pass}회차 후진 (M5)");
            await Task.Delay(CylinderTravelMS);
            _bits["X0A0"] = true;   // 후진 완료 (C0 카운트)

            _bits["M5"]   = false;
            if (pass == 2)
            {
                _bits["C0"] = true;  // C0 = K2 완료
            }
        }

        // 자동 물 (M6) — T3: 10초
        Debug.Log("[MockPLC] 자동 물 시작 (M6)");
        _bits["M6"] = true;
        await Task.Delay(WaterDurationMS);

        // 자동 바람 (M7) — T4: 10초
        _bits["M6"] = false;
        _bits["M7"] = true;
        Debug.Log("[MockPLC] 자동 바람 시작 (M7)");
        await Task.Delay(AirDurationMS);

        // 전체 리셋 (M7 하강엣지 → M40 RST + C0 RST)
        _bits["M7"]  = false;
        _bits["M40"] = false;
        _bits["C0"]  = false;
        Debug.Log("[MockPLC] 자동 사이클 완료 — M40/C0 리셋");
    }

    // ── 공개 유틸리티 ────────────────────────────────────────────────

    /// <summary>비트 직접 설정 (테스트/ContextMenu 용)</summary>
    public void SetBit(string key, bool value)
    {
        _bits[key] = value;
    }

    public bool GetBit(string key)
    {
        return _bits.TryGetValue(key, out bool v) && v;
    }

    /// <summary>세정제 잔량 감소 시뮬레이션</summary>
    public void SimulateSoapUse(int decreaseAmount = 50)
    {
        if (_words.ContainsKey("D0"))
        {
            _words["D0"] = Mathf.Max(0, _words["D0"] - decreaseAmount);
            _words["D10"]++;
            Debug.Log($"[MockPLC] 세정제 사용: 잔량 {_words["D0"] / 10f}%");
        }
    }

    public void ResetSoapLevel(int value = 1000)
    {
        _words["D0"] = Mathf.Clamp(value, 0, 1000);
        Debug.Log($"[MockPLC] 세정제 잔량 리셋: {_words["D0"] / 10f}%");
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────

    /// <summary>디바이스 주소 증가 (M0+1=M1, X0A0+1=X0A1)</summary>
    private string IncrementDevice(string device, int offset)
    {
        if (offset == 0)
        {
            return device;
        }

        // X/Y 계열: 16진수 주소
        if (device.Length > 0 && (device[0] == 'X' || device[0] == 'Y'))
        {
            string hex = device.Substring(1);
            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int num))
            {
                return $"{device[0]}{(num + offset):X3}";
            }
        }

        // M/D/C/T 계열: 10진수 주소
        int split = 0;
        while (split < device.Length && char.IsLetter(device[split]))
        {
            split++;
        }
        if (split < device.Length && int.TryParse(device.Substring(split), out int baseNum))
        {
            return $"{device.Substring(0, split)}{baseNum + offset}";
        }

        return device;
    }
}
