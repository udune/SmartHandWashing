# MockPLCClient.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Network/MockPLCClient.cs`
- **목적**: 실제 PLC 하드웨어 없이 개발/테스트를 가능하게 하는 가상 PLC 클라이언트. 내부 Dictionary를 사용해 D/M 디바이스를 시뮬레이션하며, 비누 사용량 감소 및 알람 발생 로직을 포함.

## 설계 패턴
- **Mock Object Pattern**: 테스트 더블(Test Double)로서 실제 의존성을 대체
- **Strategy Pattern**: `IPLCClient` 인터페이스 구현으로 런타임 교체 가능

## 시뮬레이션 데이터 구조

### D 디바이스 (워드 레지스터)
| 주소 | 초기값 | 용도 |
|------|--------|------|
| D0 | 1000 | 비누 잔량 (0~1000 = 0~100%) |
| D10 | 0 | 누적 사용 횟수 |

### M 디바이스 (비트 릴레이)
| 주소 | 초기값 | 용도 |
|------|--------|------|
| M0 | false | 비누 버튼 |
| M1 | false | 물 버튼 |
| M2 | false | 에어 버튼 |
| M10 | false | 비누 알람 (잔량 ≤ 20%) |
| M11 | false | 물 알람 |

## 코드 분석

### Line 1-3: using 문
```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
```
- `System.Collections.Generic`: `Dictionary<TKey, TValue>` 컬렉션 사용
- `System.Threading.Tasks`: `Task`, `Task.FromResult()`, `Task.CompletedTask` 사용
- `UnityEngine`: `Debug.Log()`, `Mathf.Max()`, `Mathf.Clamp()` 사용

### Line 5-9: 클래스 선언
```csharp
public class MockPLCClient : IPLCClient
```
- `IPLCClient` 인터페이스 구현
- `public`: 다른 클래스에서 인스턴스 생성 가능

### Line 11-14: IsConnected 프로퍼티
```csharp
public bool IsConnected
{
    get { return true; }
}
```
- Mock이므로 항상 `true` 반환
- 연결 실패 시나리오 테스트 시 `false` 반환하도록 수정 가능

### Line 16-21: D 디바이스 Dictionary
```csharp
private readonly Dictionary<string, int> _words = new Dictionary<string, int>
{
    { "D0",  1000 },   // 비누 잔량
    { "D10", 0    },   // 누적 사용 횟수
};
```
- `readonly`: 생성자 이후 참조 변경 불가 (내부 값은 변경 가능)
- 컬렉션 이니셜라이저로 초기값 설정
- 워드 단위(16비트 정수) 저장

### Line 23-31: M 디바이스 Dictionary
```csharp
private readonly Dictionary<string, bool> _bits = new Dictionary<string, bool>
{
    { "M0",  false },  // 비누 버튼
    { "M1",  false },  // 물 버튼
    { "M2",  false },  // 에어 버튼
    { "M10", false },  // 비누 알람
    { "M11", false },  // 물 알람
};
```
- 비트 단위(ON/OFF) 저장
- 버튼 입력 및 알람 상태 관리

### Line 33-37: ConnectAsync 메서드
```csharp
public Task ConnectAsync(string ip, int port, int timeoutMs)
{
    Debug.Log("[MockPLC] 가상 PLC 연결됨");
    return Task.CompletedTask;
}
```
- `Task.CompletedTask`: 이미 완료된 Task 반환 (await 시 즉시 통과)
- 실제 네트워크 연결 없이 즉시 성공
- 디버그 로그로 호출 추적 가능

### Line 39-42: Disconnect 메서드
```csharp
public void Disconnect()
{
    Debug.Log("[MockPLC] 연결 해제");
}
```
- 정리할 리소스 없으므로 로그만 출력

### Line 44-60: ReadWordsAsync 메서드
```csharp
public Task<int[]> ReadWordsAsync(string device, int count)
{
    var result = new int[count];
    for (int i = 0; i < count; i++)
    {
        string key = IncrementDevice(device, i);
        if (_words.ContainsKey(key))
            result[i] = _words[key];
        else
            result[i] = 0;
    }
    return Task.FromResult(result);
}
```
- `device`: 시작 주소 (예: "D0")
- `count`: 연속으로 읽을 워드 개수
- `IncrementDevice()`: "D0" + 1 → "D1" 주소 계산
- 존재하지 않는 키는 0으로 반환
- `Task.FromResult()`: 동기 결과를 Task로 래핑

### Line 62-78: ReadBitsAsync 메서드
```csharp
public Task<bool[]> ReadBitsAsync(string device, int count)
```
- 워드 읽기와 동일한 패턴
- 존재하지 않는 키는 `false`로 반환

### Line 80-87: WriteWordsAsync 메서드
```csharp
public Task WriteWordsAsync(string device, int[] values)
{
    for (int i = 0; i < values.Length; i++)
    {
        _words[IncrementDevice(device, i)] = values[i];
    }
    return Task.CompletedTask;
}
```
- Dictionary에 값 직접 대입 (키가 없으면 자동 추가)
- `Task.CompletedTask`: 반환값 없는 완료된 Task

### Line 89-112: WriteBitsAsync 메서드 (핵심 로직)
```csharp
public Task WriteBitsAsync(string device, bool[] values)
{
    for (int i = 0; i < values.Length; i++)
    {
        string key = IncrementDevice(device, i);
        bool prevValue = _bits.ContainsKey(key) && _bits[key];
        _bits[key] = values[i];

        // 상승 에지 감지: OFF → ON 전환 시
        if (values[i] && !prevValue)
        {
            switch (key)
            {
                case "M0":  // 비누 버튼
                    SimulateSoapUse(50);
                    _bits["M0"] = false;  // 자동 리셋
                    break;
            }
        }
    }
    return Task.CompletedTask;
}
```
**상승 에지(Rising Edge) 감지:**
- `prevValue`: 이전 상태 저장
- `values[i] && !prevValue`: OFF→ON 전환 감지
- PLC의 펄스 입력 처리 시뮬레이션

**M0 비누 버튼 처리:**
1. `SimulateSoapUse(50)`: 비누 50 감소 (5%)
2. 자동으로 `M0 = false` 리셋 (원샷 펄스)

### Line 114-127: SimulateSoapUse 메서드
```csharp
public void SimulateSoapUse(int decreaseAmount = 50)
{
    if (_words.ContainsKey("D0"))
    {
        _words["D0"] = Mathf.Max(0, _words["D0"] - decreaseAmount);
        _words["D10"]++;  // 사용 횟수 증가

        if (_words["D0"] <= 200)  // 20% 이하
        {
            _bits["M10"] = true;  // 알람 ON
        }
    }
}
```
- `Mathf.Max(0, ...)`: 음수 방지
- D0 ≤ 200 (20%): M10 알람 자동 활성화
- `public`: 테스트 코드에서 직접 호출 가능

### Line 129-134: ResetSoapLevel 메서드
```csharp
public void ResetSoapLevel(int value = 1000)
{
    _words["D0"] = Mathf.Clamp(value, 0, 1000);
    _bits["M10"] = _words["D0"] <= 200;
}
```
- `Mathf.Clamp()`: 0~1000 범위로 제한
- 알람 상태도 동기화

### Line 136-144: SetButtonBit 메서드
```csharp
public void SetButtonBit(int index, bool value)
{
    string key = $"M{index}";
    if (_bits.ContainsKey(key))
    {
        _bits[key] = value;
    }
}
```
- 문자열 보간(`$"M{index}"`)으로 키 생성
- 테스트 시 버튼 상태 직접 설정

### Line 146-168: IncrementDevice 메서드
```csharp
private string IncrementDevice(string device, int offset)
{
    if (offset == 0) return device;

    string prefix = "";
    int number = 0;
    foreach (char c in device)
    {
        if (char.IsLetter(c))
            prefix += c;
        else
            number = number * 10 + (c - '0');
    }
    return $"{prefix}{number + offset}";
}
```
**주소 파싱 알고리즘:**
1. 문자(D, M) → `prefix`에 누적
2. 숫자 → `number`에 자릿수 계산하여 누적
3. `"D100"` + offset 2 → `"D102"`

## 사용 예시

### 기본 사용
```csharp
IPLCClient plc = new MockPLCClient();
await plc.ConnectAsync("", 0, 0);  // Mock이므로 파라미터 무시

int[] data = await plc.ReadWordsAsync("D0", 2);
// data[0] = 1000 (비누 잔량)
// data[1] = 0 (D1, 미정의이므로 0)
```

### 비누 사용 시뮬레이션
```csharp
var mock = new MockPLCClient();
mock.SimulateSoapUse(100);  // 10% 감소
// D0: 1000 → 900
// D10: 0 → 1 (사용 횟수)
```

### 상승 에지 테스트
```csharp
await mock.WriteBitsAsync("M0", new[] { true });
// 내부적으로:
// 1. M0: false → true (상승 에지 감지)
// 2. SimulateSoapUse(50) 호출
// 3. M0 = false 자동 리셋
```

## 테스트 시나리오

| 시나리오 | 메서드 호출 | 예상 결과 |
|---------|------------|----------|
| 비누 잔량 읽기 | `ReadWordsAsync("D0", 1)` | `[1000]` |
| 비누 20회 사용 | `SimulateSoapUse(50)` × 20 | D0=0, M10=true |
| 비누 리필 | `ResetSoapLevel(1000)` | D0=1000, M10=false |
| 버튼 클릭 | `WriteBitsAsync("M0", [true])` | 상승 에지 → 비누 감소 |
