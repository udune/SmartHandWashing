# Chapter 05: 이벤트 시스템 - 구독/발행 패턴

## 이벤트란 무엇인가요?

**이벤트(Event)**는 "뭔가 일어났다!"고 알려주는 신호예요.

유튜브 구독을 생각해 보세요:
- 새 영상이 올라오면 → 구독자들에게 알림이 가죠
- 구독 안 한 사람은 → 알림을 받지 않아요

코드에서도 똑같아요!

```
┌──────────────────────────────────────────────────────────┐
│                    이벤트 시스템                          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│   발행자 (Publisher)        구독자 (Subscriber)           │
│   ┌─────────────┐          ┌─────────────┐               │
│   │ StationData │ ──알림──▶ │ HMIUIController │          │
│   │             │          │             │               │
│   │ "비누 잔량   │          │ "알겠어!    │               │
│   │  바뀌었어!" │          │  화면 갱신!" │               │
│   └─────────────┘          └─────────────┘               │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## C# 이벤트 기초

```csharp
// StationData.cs에서

// 1. 이벤트 선언
public event Action OnDataChanged;

// 2. 이벤트 발생 (비누 사용 시)
public void UseSoap()
{
    soapLevel -= soapDecreasePerUse;
    soapUseCount++;

    // "나 바뀌었어!" 알림 발송
    OnDataChanged?.Invoke();
}
```

### Action이란?

**Action**은 "아무것도 반환하지 않는 함수"를 가리키는 타입이에요.

```csharp
// Action 예시들
Action                    // 매개변수 없음, 반환 없음
Action<int>              // int 하나 받음, 반환 없음
Action<string, bool>     // string과 bool 받음, 반환 없음
```

---

## ?.Invoke() 문법

```csharp
OnDataChanged?.Invoke();
```

이 코드는 뭘 의미할까요?

```
OnDataChanged가 null이 아니면?
├── Yes → Invoke() 호출 (구독자들에게 알림!)
└── No  → 아무것도 안 함 (오류 없이 넘어감)

// 위 코드는 이것과 같아요:
if (OnDataChanged != null)
{
    OnDataChanged.Invoke();
}
```

> 💡 **핵심 포인트**: `?.`는 **Null 조건부 연산자**예요.
> 구독자가 없어도(null) 오류가 나지 않아서 안전합니다!

---

## 구독과 해지

### 구독하기 (+=)

```csharp
// HMIUIController.cs의 Start()에서

void Start()
{
    // "나도 알림 받을래!" - 구독 신청
    stationController.OnSoapUpdated += RefreshSoapUI;
    stationController.OnWaterUpdated += RefreshWaterUI;
    stationController.OnAirUpdated += RefreshAirUI;
}
```

### 해지하기 (-=)

```csharp
void OnDestroy()
{
    // "이제 알림 그만 받을래" - 구독 취소
    stationController.OnSoapUpdated -= RefreshSoapUI;
    stationController.OnWaterUpdated -= RefreshWaterUI;
    stationController.OnAirUpdated -= RefreshAirUI;
}
```

---

## 왜 OnDestroy에서 해지해야 하나요?

> ⚠️ **중요**: 해지 안 하면 **메모리 누수**가 발생해요!

```
문제 상황:
─────────────────────────────────────────
1. HMIUIController가 이벤트 구독
2. 씬 전환으로 HMIUIController 파괴
3. 하지만 StationData는 아직 구독자 목록에 참조를 보유
4. 가비지 컬렉터가 HMIUIController를 정리 못 함!
5. 메모리 누수 + 다음 이벤트 발생 시 오류 가능

해결책:
─────────────────────────────────────────
OnDestroy()에서 -= 로 구독 해지!
→ 참조가 끊어져서 정상적으로 정리됨
```

---

## StationController의 이벤트들

```csharp
// StationController.cs

public event Action OnSoapUpdated;   // 비누 상태 변경 시
public event Action OnWaterUpdated;  // 물 상태 변경 시
public event Action OnAirUpdated;    // 에어 상태 변경 시
```

### 언제 발생하나요?

```
OnSoapUpdated 발생 시점:
├── 비누 분사 시작할 때 (onStart 콜백)
└── 비누 분사 끝날 때 (onEnd 콜백)

OnWaterUpdated 발생 시점:
├── 물 분사 시작할 때
└── 물 분사 끝날 때

OnAirUpdated 발생 시점:
├── 에어 작동 시작할 때
└── 에어 작동 끝날 때
```

---

## 이벤트 흐름 시각화

```
[사용자] 비누 버튼 클릭!
         │
         ▼
┌─────────────────────────────────┐
│      HMIUIController            │
│      _btnSoap.RegisterCallback  │
└──────────────┬──────────────────┘
               │
               ▼
┌─────────────────────────────────┐
│      StationController          │
│      ActivateSoap()             │
│                                 │
│      1. stationData.UseSoap()   │
│      2. OnSoapUpdated?.Invoke() │  ←── 이벤트 발생!
└──────────────┬──────────────────┘
               │
         ┌─────┴─────┐
         ▼           ▼
┌─────────────┐ ┌─────────────┐
│ HMI         │ │ (다른       │
│ RefreshSoap │ │  구독자)    │
│ UI()        │ │             │
└─────────────┘ └─────────────┘
```

---

## 폴링 vs 이벤트 비교

```
폴링 방식 (나쁜 예) ❌
─────────────────────────────────────────
void Update()  // 매 프레임 호출 (초당 60번!)
{
    // 매번 확인
    if (previousSoapLevel != currentSoapLevel)
    {
        UpdateGauge(currentSoapLevel);
        previousSoapLevel = currentSoapLevel;
    }
}
문제: CPU 낭비, 변화 없어도 계속 확인

이벤트 방식 (좋은 예) ✅
─────────────────────────────────────────
// 이벤트 구독
stationData.OnDataChanged += UpdateGauge;

// 데이터 변경 시에만 호출됨
void UpdateGauge()
{
    _gaugeFill.style.height = Length.Percent(soapLevel);
}
장점: 필요할 때만 실행, 효율적!
```

---

## 실전 예제: 완전한 흐름

```csharp
// 1. StationData.cs - 이벤트 정의
public event Action OnDataChanged;

public void UseSoap()
{
    soapLevel -= soapDecreasePerUse;
    OnDataChanged?.Invoke();  // 알림!
}

// 2. HMIUIController.cs - 구독
void Start()
{
    stationData.OnDataChanged += OnSoapDataChanged;
}

void OnDestroy()
{
    stationData.OnDataChanged -= OnSoapDataChanged;
}

void OnSoapDataChanged()
{
    UpdateGauge(stationData.soapLevel);
    UpdateStatusLED(stationData.systemStatus);
}
```

---

## 자주 묻는 질문

**Q: delegate와 event의 차이가 뭔가요?**

A: event는 delegate에 **제한을 건** 버전이에요.
   - delegate: 외부에서 직접 호출 가능 (위험!)
   - event: 선언한 클래스 내부에서만 Invoke 가능 (안전!)

**Q: Action 대신 UnityEvent를 쓰면 안 되나요?**

A: UnityEvent는 Inspector에서 연결할 수 있어서 편하지만,
   성능이 약간 떨어져요. 코드에서만 다룬다면 Action이 더 효율적입니다.

---

## 다음 챕터 미리보기

다음 챕터에서는 **StationController**를 살펴봅니다.
비누/물/에어 분사의 핵심 비즈니스 로직을 이해할 거예요!
