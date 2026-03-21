# Chapter 04: 데이터 모델 - StationData 클래스

## StationData는 무엇인가요?

**StationData**는 손 세정 스테이션의 모든 상태를 저장하는 "데이터 금고"예요.

은행 금고를 생각해 보세요.
- 돈이 얼마 있는지 기록하고
- 누가 입출금했는지 추적하고
- 잔액이 부족하면 알려주죠

StationData도 마찬가지예요!

---

## 코드 살펴보기

```csharp
[CreateAssetMenu(menuName = "SmartWash/Station Data")]
public class StationData : ScriptableObject
{
    // ═══════════════════════════════════════════
    // 비누 관련 데이터
    // ═══════════════════════════════════════════

    [Header("Soap")]
    [Range(0f, 100f)]
    public float soapLevel = 100f;        // 비누 잔량 (%)
    public int soapUseCount = 0;           // 총 사용 횟수
    public float soapDecreasePerUse = 5f;  // 1회당 감소량

    // ═══════════════════════════════════════════
    // 작동 상태
    // ═══════════════════════════════════════════

    [Header("Running State")]
    public bool isSoapRunning = false;   // 비누 분사 중?
    public bool isWaterRunning = false;  // 물 분사 중?
    public bool isAirRunning = false;    // 에어 건조 중?

    // ═══════════════════════════════════════════
    // 시스템 상태
    // ═══════════════════════════════════════════

    public enum SystemStatus
    {
        Normal,   // 🟢 정상
        Warning,  // 🟡 주의 (비누 20% 이하)
        Error     // 🔴 오류 (비누 0%)
    }

    public SystemStatus systemStatus = SystemStatus.Normal;
}
```

---

## ScriptableObject란?

> 💡 **핵심 개념**: ScriptableObject는 Unity에서 데이터를 저장하는 특별한 방법이에요.

```
┌──────────────────────────────────────────────────────────┐
│          일반 클래스 vs ScriptableObject                   │
├────────────────────────┬─────────────────────────────────┤
│      일반 클래스        │       ScriptableObject          │
├────────────────────────┼─────────────────────────────────┤
│  메모리에만 존재        │  파일로 저장됨 (.asset)          │
│  게임 종료 시 사라짐    │  게임 종료해도 유지              │
│  코드에서만 값 수정     │  Inspector에서 직접 수정 가능 ✅ │
│  인스턴스 여러 개 생성  │  에셋 하나를 여러 곳에서 공유    │
└────────────────────────┴─────────────────────────────────┘
```

### Inspector에서 바로 수정!

```
Unity Editor에서:

┌────────────────────────────────────┐
│ StationDataInstance (Asset)        │
├────────────────────────────────────┤
│ Soap                               │
│   Soap Level        [████░░] 80    │  ← 슬라이더로 조절!
│   Soap Use Count    [ 15 ]         │
│   Soap Decrease     [ 5  ]         │
├────────────────────────────────────┤
│ Running State                      │
│   Is Soap Running   [ ✓ ]          │  ← 체크박스로 토글!
│   Is Water Running  [   ]          │
│   Is Air Running    [   ]          │
├────────────────────────────────────┤
│ System Status       [Normal  ▼]    │  ← 드롭다운 선택!
└────────────────────────────────────┘
```

코드 수정 없이 Inspector에서 값을 바꿀 수 있어서 테스트가 편해요!

---

## CreateAssetMenu 속성

```csharp
[CreateAssetMenu(menuName = "SmartWash/Station Data")]
```

이 한 줄이 뭘 하냐면요...

```
Unity Editor에서:
Assets 폴더 우클릭 → Create → SmartWash → Station Data

이렇게 메뉴가 생겨요! 클릭하면 새 에셋 파일이 만들어집니다.
```

---

## 데이터 필드 상세 설명

### 비누 잔량 (soapLevel)

```csharp
[Range(0f, 100f)]
public float soapLevel = 100f;
```

- **타입**: float (소수점 가능)
- **범위**: 0% ~ 100%
- **기본값**: 100% (가득 참)
- **[Range]**: Inspector에서 슬라이더로 표시됨

```
100% ████████████████████ 가득 참
 50% ██████████░░░░░░░░░░ 절반
 20% ████░░░░░░░░░░░░░░░░ 주의! 🟡
  0% ░░░░░░░░░░░░░░░░░░░░ 비었음! 🔴
```

### 작동 상태 (Running State)

```csharp
public bool isSoapRunning = false;
public bool isWaterRunning = false;
public bool isAirRunning = false;
```

- **타입**: bool (true/false)
- **용도**: 현재 뭐가 작동 중인지 추적
- **인터락**: 셋 중 하나만 true일 수 있음!

```
상태 조합:
┌─────────┬─────────┬─────────┬───────────┐
│  Soap   │  Water  │   Air   │   결과    │
├─────────┼─────────┼─────────┼───────────┤
│  false  │  false  │  false  │ 대기 상태  │
│  true   │  false  │  false  │ 비누 분사중│
│  false  │  true   │  false  │ 물 분사중  │
│  false  │  false  │  true   │ 에어 작동중│
│  true   │  true   │  false  │ ❌ 불가능! │
└─────────┴─────────┴─────────┴───────────┘
```

### 시스템 상태 (SystemStatus)

```csharp
public enum SystemStatus
{
    Normal,   // 정상 - 비누 20% 초과
    Warning,  // 주의 - 비누 1~20%
    Error     // 오류 - 비누 0%
}
```

**enum**은 "열거형"이에요. 정해진 값들 중에서만 선택할 수 있죠.

```
Normal  → LED 녹색  🟢 → "시스템 상태: 정상"
Warning → LED 노란색 🟡 → "시스템 상태: 주의"
Error   → LED 빨간색 🔴 → "시스템 상태: 오류"
```

---

## Header 속성

```csharp
[Header("Soap")]
public float soapLevel = 100f;

[Header("Running State")]
public bool isSoapRunning = false;
```

Inspector에서 항목들을 그룹으로 묶어서 보기 좋게 정리해 줘요.

```
┌────────────────────────────────────┐
│ ▼ Soap ─────────────────────────── │  ← [Header("Soap")]
│   Soap Level: 100                  │
│   Soap Use Count: 0                │
│   Soap Decrease Per Use: 5         │
│                                    │
│ ▼ Running State ─────────────────  │  ← [Header("Running State")]
│   Is Soap Running: ☐               │
│   Is Water Running: ☐              │
│   Is Air Running: ☐                │
└────────────────────────────────────┘
```

---

## 자주 묻는 질문

**Q: 왜 float 대신 int를 안 쓰나요?**

A: 소수점 표현이 필요해서요. 87.5% 같은 값을 표시하려면
   float이 필요합니다. 또한 0~1000 범위를 0~100%로 변환할 때도
   소수점이 필요하고요.

**Q: bool 변수 3개 대신 enum으로 상태를 관리하면 안 되나요?**

A: 좋은 질문이에요! enum으로 `Idle, Soap, Water, Air`처럼 할 수도 있어요.
   하지만 bool 3개는 직관적이고, 각 상태를 독립적으로 체크하기 편해요.
   설계 선택의 문제입니다.

---

## 다음 챕터 미리보기

다음 챕터에서는 **이벤트 시스템**을 살펴봅니다.
OnDataChanged 이벤트가 어떻게 작동하는지,
구독/발행 패턴이 뭔지 알아볼 거예요!
