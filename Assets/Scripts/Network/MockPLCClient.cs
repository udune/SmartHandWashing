// System.Collections.Generic: Dictionary<TKey, TValue> 사용
using System.Collections.Generic;
// System.Threading.Tasks: Task, Task.FromResult, Task.CompletedTask 사용
using System.Threading.Tasks;
// UnityEngine: Debug.Log, Mathf.Max, Mathf.Clamp 사용
using UnityEngine;

/// <summary>
/// PLC 없이 동작하는 가상 클라이언트.
/// 내부 딕셔너리로 D/M 디바이스를 시뮬레이션한다.
/// </summary>
// IPLCClient 구현: 실제 SLMPClient와 교체 가능한 Mock 객체
public class MockPLCClient : IPLCClient
{
    // Mock이므로 항상 연결된 상태로 반환
    public bool IsConnected
    {
        get { return true; }
    }

    // D 디바이스 (워드, 초기값 설정) - readonly: 참조 변경 불가, 내부 값은 변경 가능
    // Dictionary 컬렉션 이니셜라이저로 시뮬레이션용 초기값 설정
    private readonly Dictionary<string, int> _words = new Dictionary<string, int>
    {
        { "D0",  1000 },   // 비누 잔량 (0~1000 = 0~100%)
        { "D10", 0    },   // 누적 사용 횟수
    };

    // M 디바이스 (비트) - PLC 릴레이 접점 시뮬레이션
    private readonly Dictionary<string, bool> _bits = new Dictionary<string, bool>
    {
        { "M0",  false },  // 비누 버튼
        { "M1",  false },  // 물 버튼
        { "M2",  false },  // 에어 버튼
        { "M10", false },  // 비누 알람
        { "M11", false },  // 물 알람
    };

    // ConnectAsync: Mock이므로 실제 네트워크 연결 없이 즉시 완료
    // Task.CompletedTask: 이미 완료된 Task 반환 (await 시 즉시 통과)
    public Task ConnectAsync(string ip, int port, int timeoutMs)
    {
        Debug.Log("[MockPLC] 가상 PLC 연결됨");
        return Task.CompletedTask;
    }

    // Disconnect: 정리할 리소스 없으므로 로그만 출력
    public void Disconnect()
    {
        Debug.Log("[MockPLC] 연결 해제");
    }

    // ReadWordsAsync: device 시작 주소부터 count개 워드를 Dictionary에서 조회
    // 존재하지 않는 키는 0으로 반환
    public Task<int[]> ReadWordsAsync(string device, int count)
    {
        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            // IncrementDevice: "D0" + 1 → "D1" 주소 계산
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
        // Task.FromResult: 동기 결과를 Task<T>로 래핑 (비동기 시그니처 충족)
        return Task.FromResult(result);
    }

    // ReadBitsAsync: device 시작 주소부터 count개 비트를 Dictionary에서 조회
    // 존재하지 않는 키는 false로 반환
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

    // WriteWordsAsync: Dictionary에 값 직접 대입 (키가 없으면 자동 추가)
    public Task WriteWordsAsync(string device, int[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            _words[IncrementDevice(device, i)] = values[i];
        }
        return Task.CompletedTask;
    }

    // WriteBitsAsync: 비트 쓰기 + 상승 에지 감지로 PLC 동작 시뮬레이션
    public Task WriteBitsAsync(string device, bool[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            string key = IncrementDevice(device, i);
            // prevValue: 상승 에지 감지를 위해 이전 상태 저장
            bool prevValue = _bits.ContainsKey(key) && _bits[key];
            _bits[key] = values[i];

            // 상승 에지 감지: OFF → ON 전환 시 PLC 동작 시뮬레이션
            // values[i] && !prevValue: 새 값이 true이고 이전 값이 false일 때
            if (values[i] && !prevValue)
            {
                switch (key)
                {
                    case "M0":  // 비누 버튼 ON → 비누 사용
                        SimulateSoapUse(50);
                        // 원샷 펄스: 자동 리셋으로 다음 상승 에지 감지 가능하게 함
                        _bits["M0"] = false;
                        Debug.Log("[MockPLC] M0 상승 에지 → 비누 사용 시뮬레이션");
                        break;
                    // M1, M2는 잔량 개념 없으므로 별도 처리 불필요
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>비누 사용 시뮬레이션 (테스트용 외부 호출)</summary>
    // decreaseAmount: 감소량 (기본 50 = 5%), Mathf.Max로 음수 방지
    public void SimulateSoapUse(int decreaseAmount = 50)
    {
        if (_words.ContainsKey("D0"))
        {
            // Mathf.Max(0, ...): 비누 잔량이 음수가 되지 않도록 보장
            _words["D0"] = Mathf.Max(0, _words["D0"] - decreaseAmount);
            // D10: 누적 사용 횟수 증가
            _words["D10"]++;

            // 잔량 20% 이하 시 알람 자동 활성화
            if (_words["D0"] <= 200)
            {
                _bits["M10"] = true;  // 비누 알람
            }
        }
    }

    /// <summary>비누 잔량 리셋 (테스트용)</summary>
    // Mathf.Clamp: 0~1000 범위로 값 제한
    public void ResetSoapLevel(int value = 1000)
    {
        _words["D0"] = Mathf.Clamp(value, 0, 1000);
        // 알람 상태도 잔량에 따라 동기화
        _bits["M10"] = _words["D0"] <= 200;
    }

    /// <summary>버튼 비트 설정 (테스트용) - M0=비누, M1=물, M2=에어</summary>
    // 문자열 보간($"M{index}")으로 디바이스 주소 생성
    public void SetButtonBit(int index, bool value)
    {
        string key = $"M{index}";
        if (_bits.ContainsKey(key))
        {
            _bits[key] = value;
        }
    }

    // IncrementDevice: PLC 주소 문자열을 파싱하여 offset만큼 증가
    // 예: "D0" + offset 2 → "D2", "M100" + offset 5 → "M105"
    private string IncrementDevice(string device, int offset)
    {
        // offset이 0이면 원본 주소 그대로 반환 (최적화)
        if (offset == 0)
        {
            return device;
        }

        string prefix = "";  // 디바이스 타입 (D, M, X, Y 등)
        int number = 0;      // 주소 번호
        // 문자열 파싱: 문자는 prefix, 숫자는 number에 누적
        foreach (char c in device)
        {
            // char.IsLetter: 알파벳 문자 판별
            if (char.IsLetter(c))
            {
                prefix += c;
            }
            else
            {
                // 자릿수 계산: "100" → 1*10+0=10 → 10*10+0=100
                number = number * 10 + (c - '0');
            }
        }
        // 문자열 보간으로 새 주소 생성
        return $"{prefix}{number + offset}";
    }
}
