# Chapter 10: 네트워크 통합 - NetworkManager

## NetworkManager의 역할

**NetworkManager**는 PLC와 Unity 사이의 "통역사"예요.

```
┌──────────────────────────────────────────────────────────┐
│                    NetworkManager의 역할                  │
├──────────────────────────────────────────────────────────┤
│                                                          │
│   Unity 세계              NetworkManager      PLC 세계   │
│   ──────────              ─────────────      ─────────   │
│                                                          │
│   soapLevel = 80.0f  ←── 변환 ───  D0 = 800             │
│   isSoapRunning = true ←── 변환 ───  M0 = ON             │
│                                                          │
│   버튼 클릭! ───── 명령 전송 ─────▶ M0 = ON              │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## 설정 로드하기

```csharp
private void LoadConfig()
{
    // Resources 폴더에서 PLCConfig.json 로드
    var json = Resources.Load<TextAsset>("PLCConfig");

    if (json != null)
    {
        _config = JsonUtility.FromJson<PLCConfig>(json.text);
        Debug.Log($"[Network] 설정 로드: useMock={_config.useMock}");
    }
    else
    {
        // 설정 파일 없으면 기본값 사용
        _config = new PLCConfig();
        Debug.LogWarning("[Network] PLCConfig.json 없음 — Mock 모드 사용");
    }

    // 클라이언트 생성 (Mock 또는 실제)
    if (_config.useMock)
    {
        _client = new MockPLCClient();
    }
    else
    {
        _client = new SLMPClient();
    }
}
```

### Mock vs 실제 클라이언트

```
useMock = true:
─────────────────────────────────────────
┌─────────────────┐
│  MockPLCClient  │  ← 가상 PLC
│                 │
│  내부 Dictionary │  ← 메모리에서 데이터 관리
│  로 데이터 관리  │
└─────────────────┘
→ PLC 없이 개발/테스트 가능!

useMock = false:
─────────────────────────────────────────
┌─────────────────┐
│   SLMPClient    │  ← 실제 PLC 통신
│                 │
│  TCP/IP 소켓    │  ← 네트워크 통신
│  SLMP 프로토콜  │
└─────────────────┘
→ 실제 장비와 연동!
```

---

## 연결 루프 (ConnectionLoop)

```csharp
private IEnumerator ConnectionLoop()
{
    // 최초 연결 시도
    if (!_hasConnectedOnce)
    {
        SetStatus("연결 시도 중...", false);
        yield return ConnectCoroutine();
        _hasConnectedOnce = true;
    }

    // 무한 루프 - 연결 유지
    while (true)
    {
        if (!IsConnected)
        {
            SetStatus("연결 시도 중...", false);
            yield return ConnectCoroutine();  // 재연결 시도
        }
        else
        {
            yield return PollCoroutine();     // 데이터 폴링
        }
    }
}
```

### 상태 전이도

```
┌─────────┐     연결 성공     ┌─────────┐
│  대기   │ ────────────────▶ │  연결됨 │
│         │                   │         │
└────┬────┘                   └────┬────┘
     │                              │
     │     연결 실패                │ 통신 오류
     │     (5초 대기 후 재시도)      │
     │                              │
     └──────────◀───────────────────┘
```

---

## 폴링 루프 (PollCoroutine)

```csharp
private IEnumerator PollCoroutine()
{
    // ═══════════════════════════════════════════
    // Step 1: 워드 읽기 (비누 잔량)
    // ═══════════════════════════════════════════

    var readTask = _client.ReadWordsAsync(_config.devices.soapLevel, 1);
    yield return new WaitUntil(() => readTask.IsCompleted);

    if (readTask.IsFaulted)
    {
        SetStatus("통신 오류 — 재연결", false);
        _client.Disconnect();
        yield break;  // 루프 탈출 → ConnectionLoop에서 재연결
    }

    // D0: 0~1000 → 0.0~100.0%
    int raw = readTask.Result[0];
    float pct = Mathf.Clamp(raw / 10f, 0f, 100f);

    if (Mathf.Abs(stationData.soapLevel - pct) > 0.1f)
    {
        stationData.soapLevel = pct;
        UpdateSystemStatus(pct);
    }

    // ═══════════════════════════════════════════
    // Step 2: 비트 읽기 (버튼 상태)
    // ═══════════════════════════════════════════

    var bitTask = _client.ReadBitsAsync(_config.devices.soapBtn, 3);
    yield return new WaitUntil(() => bitTask.IsCompleted);

    if (!bitTask.IsFaulted)
    {
        bool[] bits = bitTask.Result;
        // 상승 에지 검출 (다음 섹션에서 자세히!)
        // ...
    }

    // ═══════════════════════════════════════════
    // Step 3: 대기 (100ms)
    // ═══════════════════════════════════════════

    yield return new WaitForSeconds(_config.pollIntervalMs / 1000f);
}
```

---

## 상승 에지 검출

> 💡 **핵심 개념**: 버튼을 꾹 누르고 있어도 한 번만 반응해야 해요!

```csharp
private bool[] _prevBits = new bool[3];  // 이전 상태 저장

// 폴링 중...
if (bits[0] && !_prevBits[0])  // OFF → ON 전환!
{
    stationController.ActivateSoap();
    Debug.Log("[Network] PLC 신호: 비누 활성화");
}

// 현재 상태를 이전 상태로 저장
_prevBits[0] = bits[0];
_prevBits[1] = bits[1];
_prevBits[2] = bits[2];
```

### 상승 에지란?

```
시간 ─────────────────────────────────────────────────▶

M0:  OFF OFF ON  ON  ON  OFF OFF OFF ON  ON  OFF
          │         │              │
          ▼         ×              ▼
       감지!     무시!         다시 감지!
    (OFF→ON)   (이미 ON)       (OFF→ON)

상승 에지 (Rising Edge) = OFF에서 ON으로 바뀌는 순간만!
```

### 왜 상승 에지만?

```
문제 상황:
─────────────────────────────────────────
버튼을 1초간 누르고 있으면?
폴링 100ms 간격 → 10번 읽힘
bits[0] = true가 10번 반환됨

에지 검출 없이 처리하면:
→ ActivateSoap() 10번 호출 시도! ❌

에지 검출로 처리하면:
→ 첫 번째만 true && !false = true
→ 나머지 true && !true = false
→ ActivateSoap() 1번만 호출! ✅
```

---

## HMI → PLC 쓰기

```csharp
public void WriteSoapButton(bool value)
{
    StartCoroutine(WriteBitCoroutine(_config.devices.soapBtn, value));
}

private IEnumerator WriteBitCoroutine(string device, bool value)
{
    if (!IsConnected)
    {
        yield break;  // 연결 안 됐으면 무시
    }

    var task = _client.WriteBitsAsync(device, new[] { value });
    yield return new WaitUntil(() => task.IsCompleted);

    if (task.IsFaulted)
    {
        Debug.LogWarning($"[Network] 비트 쓰기 실패: {device} = {value}");
    }
}
```

### 양방향 통신

```
┌─────────────────────────────────────────────────────────────┐
│                    양방향 통신 흐름                          │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   PLC → Unity (폴링)                                        │
│   ─────────────────────────────────────                     │
│   100ms마다 D0, M0~M2 읽기                                  │
│   → soapLevel 업데이트                                      │
│   → 버튼 상태 감지 (에지 검출)                               │
│                                                             │
│   Unity → PLC (이벤트)                                      │
│   ─────────────────────────────────────                     │
│   버튼 클릭 시 WriteBitCoroutine 호출                        │
│   → M0/M1/M2에 true 쓰기                                    │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## MockPLCClient 상세

```csharp
public class MockPLCClient : IPLCClient
{
    public bool IsConnected { get { return true; } }  // 항상 연결됨!

    // 가상 메모리
    private readonly Dictionary<string, int> _words = new Dictionary<string, int>
    {
        { "D0", 1000 },  // 비누 잔량 100%
        { "D10", 0 },    // 사용 횟수 0
    };

    private readonly Dictionary<string, bool> _bits = new Dictionary<string, bool>
    {
        { "M0", false },  // 비누 버튼
        { "M1", false },  // 물 버튼
        { "M2", false },  // 에어 버튼
    };

    // 테스트용 메서드
    public void SimulateSoapUse(int decreaseAmount = 50)
    {
        _words["D0"] = Mathf.Max(0, _words["D0"] - decreaseAmount);
        _words["D10"]++;

        if (_words["D0"] <= 200)
        {
            _bits["M10"] = true;  // 비누 알람!
        }
    }
}
```

### Mock 테스트 방법

```
Unity Inspector에서:
NetworkManager 컴포넌트 우클릭
├── "Mock: 비누 사용 시뮬레이션" ← 비누 5% 감소
└── "Mock: 비누 잔량 리셋 (100%)" ← 리필!
```

---

## 전체 아키텍처 정리

```
┌─────────────────────────────────────────────────────────────┐
│                  SmartHandWash 전체 구조                     │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  ┌─────────────┐   이벤트   ┌─────────────────┐             │
│  │ StationData │ ─────────▶│ HMIUIController │             │
│  │   (Model)   │           │     (View)      │             │
│  └──────┬──────┘           └───────┬─────────┘             │
│         │                          │                        │
│         ▼                          ▼                        │
│  ┌─────────────────────────────────────────┐                │
│  │          StationController              │                │
│  │            (Controller)                  │                │
│  └─────────────────┬───────────────────────┘                │
│                    │                                        │
│                    ▼                                        │
│  ┌─────────────────────────────────────────┐                │
│  │           NetworkManager                 │                │
│  │                                          │                │
│  │    ┌───────────┐    ┌───────────┐       │                │
│  │    │ MockPLC   │ or │ SLMPClient│       │                │
│  │    │ (테스트)  │    │ (실제 PLC)│       │                │
│  │    └───────────┘    └───────────┘       │                │
│  └─────────────────────────────────────────┘                │
│                    │                                        │
│                    ▼                                        │
│         ┌─────────────────────┐                             │
│         │   실제 PLC 장비      │                             │
│         │  (Mitsubishi iQ-R)  │                             │
│         └─────────────────────┘                             │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

---

## 자주 묻는 질문

**Q: 왜 async/await 대신 Coroutine을 쓰나요?**

A: Unity의 메인 스레드 제약 때문이에요.
   Coroutine은 자동으로 메인 스레드에서 실행되어
   UI 업데이트, 게임 오브젝트 조작이 안전합니다.

**Q: 실제 PLC 연결 시 주의할 점은?**

A: 네트워크 설정이 중요해요!
   - IP 주소가 같은 서브넷인지
   - 방화벽이 5007 포트를 허용하는지
   - PLC 측 SLMP 설정이 활성화되어 있는지

**Q: 폴링 간격 100ms는 적절한가요?**

A: 산업용으로는 빠른 편이에요. 필요에 따라 조절하세요.
   너무 빠르면 네트워크 부하, 너무 느리면 반응성 저하!

---

## 축하합니다! 🎉

SmartHandWash 프로젝트의 모든 핵심 개념을 학습했습니다!

### 배운 것들 정리

| 챕터 | 핵심 내용 |
|------|----------|
| 01~02 | 프로젝트 개요, 기술 스택 |
| 03 | MVC 아키텍처 패턴 |
| 04~05 | 데이터 모델, 이벤트 시스템 |
| 06~07 | 비즈니스 로직, 코루틴 |
| 08 | UI Toolkit, HMI |
| 09~10 | PLC 통신, 네트워크 통합 |

이제 실제 코드를 수정하고 확장할 준비가 되었습니다!
