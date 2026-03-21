using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// PLC 없이 동작하는 가상 클라이언트.
/// 내부 딕셔너리로 D/M 디바이스를 시뮬레이션한다.
/// </summary>
public class MockPLCClient : IPLCClient
{
    public bool IsConnected
    {
        get { return true; }
    }

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
            if (_words.ContainsKey(key))
            {
                result[i] = _words[key];
            }
            else
            {
                result[i] = 0;
            }
        }
        return Task.FromResult(result);
    }

    public Task<bool[]> ReadBitsAsync(string device, int count)
    {
        var result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            string key = IncrementDevice(device, i);
            if (_bits.ContainsKey(key) && _bits[key])
            {
                result[i] = true;
            }
            else
            {
                result[i] = false;
            }
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
            _bits[IncrementDevice(device, i)] = values[i];
        }
        return Task.CompletedTask;
    }

    /// <summary>비누 사용 시뮬레이션 (테스트용 외부 호출)</summary>
    public void SimulateSoapUse(int decreaseAmount = 50)
    {
        if (_words.ContainsKey("D0"))
        {
            _words["D0"] = Mathf.Max(0, _words["D0"] - decreaseAmount);
            _words["D10"]++;

            if (_words["D0"] <= 200)
            {
                _bits["M10"] = true;  // 비누 알람
            }
        }
    }

    /// <summary>비누 잔량 리셋 (테스트용)</summary>
    public void ResetSoapLevel(int value = 1000)
    {
        _words["D0"] = Mathf.Clamp(value, 0, 1000);
        _bits["M10"] = _words["D0"] <= 200;
    }

    /// <summary>버튼 비트 설정 (테스트용) - M0=비누, M1=물, M2=에어</summary>
    public void SetButtonBit(int index, bool value)
    {
        string key = $"M{index}";
        if (_bits.ContainsKey(key))
        {
            _bits[key] = value;
        }
    }

    // "D0" + offset → "D1", "D2" ...
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
