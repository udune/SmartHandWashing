# NetworkManager.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Network/NetworkManager.cs`
- **목적**: PLC와의 연결 관리 및 100ms 주기 폴링 루프 실행. StationData를 PLC 데이터로 갱신하고, HMI 버튼 입력을 PLC에 전송하는 양방향 통신 허브.

## 아키텍처 역할

```
┌─────────────────────────────────────────────────────────────┐
│                      NetworkManager                          │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐   │
│  │ ConnectionLoop│───→│ PollCoroutine│───→│ StationData  │   │
│  │ (자동 재연결) │    │ (100ms 주기) │    │ (데이터 갱신)│   │
│  └──────────────┘    └──────────────┘    └──────────────┘   │
│         │                   │                    │           │
│         ▼                   ▼                    ▼           │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐   │
│  │ IPLCClient   │    │ StationCtrl  │    │ HMIUICtrl    │   │
│  │ (Mock/SLMP)  │    │ (파티클 제어)│    │ (UI 갱신)    │   │
│  └──────────────┘    └──────────────┘    └──────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## 데이터 흐름

### PLC → Unity (폴링)
1. `PollCoroutine`: 100ms마다 D0(비누 잔량), M0-M2(버튼 상태) 읽기
2. `StationData.soapLevel` 갱신
3. 상승 에지 감지 시 `StationController.ActivateSoap/Water/Air()` 호출

### Unity → PLC (쓰기)
1. HMI 버튼 클릭 → `WriteSoapButton()` 등 호출
2. `WriteBitCoroutine`: 해당 M 디바이스에 비트 쓰기

## 코드 분석

### Line 1-3: using 문
```csharp
using System;
using System.Collections;
using UnityEngine;
```
- `System`: `Action<T>` 델리게이트 타입 사용
- `System.Collections`: `IEnumerator` 코루틴 반환 타입
- `UnityEngine`: MonoBehaviour, 코루틴, Debug 등

### Line 5-9: 클래스 선언
```csharp
public class NetworkManager : MonoBehaviour
```
- `MonoBehaviour` 상속으로 Unity 생명주기 메서드 사용
- GameObject에 컴포넌트로 부착하여 사용

### Line 11-13: Inspector 참조
```csharp
[Header("References")]
public StationData stationData;
public StationController stationController;
```
- `[Header]`: Inspector에서 섹션 구분용 라벨
- `public`: Inspector에서 드래그&드롭으로 연결 가능
- ScriptableObject와 컨트롤러 참조

### Line 15-17: 내부 상태
```csharp
private PLCConfig _config;
private IPLCClient _client;
```
- `_config`: JSON에서 로드한 PLC 설정
- `_client`: 인터페이스 타입으로 Mock/SLMP 교체 가능

### Line 19-21: 에지 검출용 상태
```csharp
private bool _hasConnectedOnce = false;
private bool[] _prevBits = new bool[3];
```
- `_hasConnectedOnce`: 최초 연결 시도 추적 (Mock 모드 보장)
- `_prevBits`: 이전 프레임 비트 상태 (상승 에지 감지용)

### Line 22-32: IsConnected 프로퍼티
```csharp
public bool IsConnected
{
    get
    {
        if (_client != null && _client.IsConnected)
            return true;
        return false;
    }
}
```
- null 체크 후 클라이언트 연결 상태 반환
- 외부에서 연결 상태 확인 가능

### Line 33-36: 상태 및 이벤트
```csharp
public string StatusMessage { get; private set; } = "초기화 중...";
public event Action<bool> OnConnectionChanged;
public event Action<string> OnStatusChanged;
```
- `StatusMessage`: 현재 연결 상태 메시지
- 이벤트: UI에서 구독하여 상태 변화 시 갱신

### Line 40-50: Start 메서드
```csharp
void Start()
{
    if (stationController == null)
        stationController = GetComponent<StationController>();
    LoadConfig();
    StartCoroutine(ConnectionLoop());
}
```
- `GetComponent<T>()`: 같은 GameObject의 컴포넌트 자동 획득
- `LoadConfig()`: JSON 설정 로드 및 클라이언트 생성
- `StartCoroutine()`: 연결 루프 백그라운드 실행

### Line 52-55: OnDestroy 메서드
```csharp
void OnDestroy()
{
    _client?.Disconnect();
}
```
- `?.` (null 조건부 연산자): null이면 호출 스킵
- GameObject 파괴 시 PLC 연결 정리

### Line 59-81: LoadConfig 메서드
```csharp
private void LoadConfig()
{
    var json = Resources.Load<TextAsset>("PLCConfig");
    if (json != null)
    {
        _config = JsonUtility.FromJson<PLCConfig>(json.text);
    }
    else
    {
        _config = new PLCConfig();  // 기본값 사용
    }

    if (_config.useMock)
        _client = new MockPLCClient();
    else
        _client = new SLMPClient();
}
```
- `Resources.Load<T>()`: Resources 폴더에서 에셋 로드
- `JsonUtility.FromJson<T>()`: JSON → C# 객체 역직렬화
- 설정에 따라 Mock 또는 실제 SLMP 클라이언트 생성

### Line 85-107: ConnectionLoop 코루틴
```csharp
private IEnumerator ConnectionLoop()
{
    // 최초 연결 (Mock 모드에서도 보장)
    if (!_hasConnectedOnce)
    {
        SetStatus("연결 시도 중...", false);
        yield return ConnectCoroutine();
        _hasConnectedOnce = true;
    }

    while (true)  // 무한 루프
    {
        if (!IsConnected)
            yield return ConnectCoroutine();  // 재연결
        else
            yield return PollCoroutine();     // 폴링
    }
}
```
- `yield return Coroutine`: 다른 코루틴 완료까지 대기
- `while (true)`: Unity 프레임과 독립적인 영구 루프
- 연결 끊김 시 자동 재연결 시도

### Line 109-123: ConnectCoroutine 메서드
```csharp
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
```
- `Task` → 코루틴 변환: `WaitUntil(() => task.IsCompleted)`
- `task.IsFaulted`: 예외 발생 여부 확인
- 실패 시 5초 후 재시도 (ConnectionLoop에서 다시 호출)

### Line 127-186: PollCoroutine 메서드 (핵심)
```csharp
private IEnumerator PollCoroutine()
{
    // 1. 비누 잔량 읽기 (D0)
    var readTask = _client.ReadWordsAsync(_config.devices.soapLevel, 1);
    yield return new WaitUntil(() => readTask.IsCompleted);

    if (readTask.IsFaulted)
    {
        SetStatus("통신 오류 — 재연결", false);
        _client.Disconnect();
        yield break;  // 코루틴 종료 → ConnectionLoop에서 재연결
    }

    // 0~1000 → 0.0~100.0% 변환
    int raw = readTask.Result[0];
    float pct = Mathf.Clamp(raw / 10f, 0f, 100f);
    if (Mathf.Abs(stationData.soapLevel - pct) > 0.1f)
    {
        stationData.soapLevel = pct;
        UpdateSystemStatus(pct);
    }

    // 2. 버튼 비트 읽기 (M0-M2)
    var bitTask = _client.ReadBitsAsync(_config.devices.soapBtn, 3);
    yield return new WaitUntil(() => bitTask.IsCompleted);

    if (!bitTask.IsFaulted)
    {
        bool[] bits = bitTask.Result;
        // 상승 에지 감지: OFF→ON 전환 시에만 동작
        if (bits[0] && !_prevBits[0])
            stationController.ActivateSoap();
        if (bits[1] && !_prevBits[1])
            stationController.ActivateWater();
        if (bits[2] && !_prevBits[2])
            stationController.ActivateAir();

        // 현재 상태 저장 (다음 프레임 비교용)
        _prevBits[0] = bits[0];
        _prevBits[1] = bits[1];
        _prevBits[2] = bits[2];
    }

    // 3. 폴링 간격 대기 (기본 100ms)
    yield return new WaitForSeconds(_config.pollIntervalMs / 1000f);
}
```
**주요 로직:**
1. D0 레지스터에서 비누 잔량 읽기
2. M0-M2 비트에서 버튼 상태 읽기
3. 상승 에지 감지로 파티클 시스템 활성화
4. 설정된 간격(100ms)만큼 대기

### Line 190-219: 쓰기 메서드
```csharp
public void WriteSoapButton(bool value)
{
    StartCoroutine(WriteBitCoroutine(_config.devices.soapBtn, value));
}

private IEnumerator WriteBitCoroutine(string device, bool value)
{
    if (!IsConnected) yield break;

    var task = _client.WriteBitsAsync(device, new[] { value });
    yield return new WaitUntil(() => task.IsCompleted);

    if (task.IsFaulted)
        Debug.LogWarning($"[Network] 비트 쓰기 실패: {device} = {value}");
}
```
- HMI 버튼 클릭 시 호출
- PLC에 해당 비트 쓰기
- 비동기 작업을 코루틴으로 래핑

### Line 223-237: UpdateSystemStatus 메서드
```csharp
private void UpdateSystemStatus(float soapPct)
{
    if (soapPct <= 0f)
        stationData.systemStatus = StationData.SystemStatus.Error;
    else if (soapPct <= 20f)
        stationData.systemStatus = StationData.SystemStatus.Warning;
    else
        stationData.systemStatus = StationData.SystemStatus.Normal;
}
```
- 비누 잔량에 따른 시스템 상태 자동 판단
- 0%: 오류, 1-20%: 경고, 21-100%: 정상

### Line 239-245: SetStatus 헬퍼
```csharp
private void SetStatus(string msg, bool connected)
{
    StatusMessage = msg;
    OnStatusChanged?.Invoke(msg);
    OnConnectionChanged?.Invoke(connected);
    Debug.Log($"[Network] {msg}");
}
```
- 상태 메시지 갱신 + 이벤트 발행
- `?.Invoke()`: 구독자 없으면 호출 스킵

### Line 249-280: Mock 전용 메서드
```csharp
[ContextMenu("Mock: 비누 사용 시뮬레이션")]
public void MockSimulateSoapUse()
{
    if (_client is MockPLCClient mock)
    {
        mock.SetButtonBit(0, true);
        mock.SimulateSoapUse(50);
        StartCoroutine(ResetButtonBitAfterDelay(mock, 0, 0.5f));
    }
}
```
- `[ContextMenu]`: Inspector 컴포넌트 우클릭 메뉴에 추가
- `is 패턴 매칭`: 타입 확인 + 캐스팅 동시 수행
- 에디터에서 Mock PLC 동작 테스트 가능

## PLCConfig 구조 (예상)

```json
{
  "useMock": true,
  "ip": "192.168.1.10",
  "port": 5000,
  "timeoutMs": 3000,
  "pollIntervalMs": 100,
  "devices": {
    "soapLevel": "D0",
    "soapBtn": "M0",
    "waterBtn": "M1",
    "airBtn": "M2"
  }
}
```

## 상승 에지 감지 상세

```
시간 →
PLC M0:   OFF ─── ON ─── ON ─── ON ─── OFF ─── ON
_prevBits[0]: OFF ─── OFF ─── ON ─── ON ─── ON ─── OFF
검출 결과:    X     ✓상승   X     X     X     ✓상승
              에지              에지
```
- `bits[0] && !_prevBits[0]`: 현재 ON이고 이전이 OFF일 때만 true
- 버튼을 누르고 있어도 최초 1회만 동작 트리거

## 에러 처리 흐름

```
PollCoroutine
    ↓
ReadWordsAsync 실패
    ↓
yield break (코루틴 종료)
    ↓
ConnectionLoop의 while(true) 계속
    ↓
IsConnected == false (Disconnect 호출됨)
    ↓
ConnectCoroutine 실행 (재연결 시도)
```

## 사용 예시

### Inspector 설정
1. GameObject에 NetworkManager 컴포넌트 추가
2. StationData 에셋 연결
3. StationController 컴포넌트 연결 (또는 자동 획득)
4. Resources/PLCConfig.json 생성

### 외부에서 버튼 쓰기
```csharp
// HMIUIController에서
networkManager.WriteSoapButton(true);  // 비누 버튼 ON
```

### 상태 구독
```csharp
networkManager.OnConnectionChanged += (connected) => {
    connectionIcon.color = connected ? Color.green : Color.red;
};
networkManager.OnStatusChanged += (msg) => {
    statusLabel.text = msg;
};
```
