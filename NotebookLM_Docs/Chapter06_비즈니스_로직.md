# Chapter 06: 비즈니스 로직 - StationController

## StationController의 역할

**StationController**는 손 세정 스테이션의 "두뇌"예요.

오케스트라 지휘자를 생각해 보세요:
- "바이올린, 지금 시작!"
- "드럼, 3초 후에 멈춰!"
- "다른 악기 연주 중엔 대기!"

StationController도 마찬가지예요:
- "비누, 지금 분사 시작!"
- "3초 후에 분사 멈춰!"
- "다른 동작 중엔 새 동작 금지!"

---

## 인터락(Interlock) 시스템

> 💡 **핵심 개념**: 인터락은 "동시 작동 방지" 시스템이에요.

교통 신호등을 생각해 보세요. 빨간불과 초록불이 동시에 켜지면?
사고가 나죠! 그래서 **절대 동시에 켜지지 않도록** 설계되어 있어요.

```csharp
private bool IsAnyRunning()
{
    if (stationData.isSoapRunning)
    {
        return true;  // 비누 분사 중!
    }
    if (stationData.isWaterRunning)
    {
        return true;  // 물 분사 중!
    }
    if (stationData.isAirRunning)
    {
        return true;  // 에어 작동 중!
    }
    return false;     // 아무것도 안 돌아감
}
```

### 인터락 적용

```csharp
public void ActivateSoap()
{
    // 인터락 체크!
    if (IsAnyRunning() || stationData.soapLevel <= 0f)
    {
        return;  // 거부! 아무 일도 안 일어남
    }

    // 여기까지 왔다면 → 분사 시작!
    _soapCoroutine = StartCoroutine(RunDispenser(...));
}
```

```
인터락 작동 예시:
─────────────────────────────────────────
비누 분사 중 (isSoapRunning = true)

  사용자: "물 버튼 클릭!"
  Controller: IsAnyRunning() 호출
  결과: true (비누가 돌아가고 있음!)
  Controller: return; (무시!)

  → 물은 비누가 끝난 후에야 작동 가능
```

---

## ActivateSoap() 함수 분석

```csharp
public void ActivateSoap()
{
    // ═══════════════════════════════════════════
    // Step 1: 조건 검사
    // ═══════════════════════════════════════════

    // 다른 동작 중이거나, 비누 없으면 거부
    if (IsAnyRunning() || stationData.soapLevel <= 0f)
    {
        return;
    }

    // ═══════════════════════════════════════════
    // Step 2: 기존 코루틴 정리
    // ═══════════════════════════════════════════

    if (_soapCoroutine != null)
    {
        StopCoroutine(_soapCoroutine);
    }

    // ═══════════════════════════════════════════
    // Step 3: 새 코루틴 시작
    // ═══════════════════════════════════════════

    _soapCoroutine = StartCoroutine(RunDispenser(
        setter: v => stationData.isSoapRunning = v,
        duration: 3f,           // 3초간
        particle: soapParticle,
        onStart: () =>
        {
            stationData.UseSoap();     // 비누 사용 기록
            OnSoapUpdated?.Invoke();   // UI에 알림
        },
        onEnd: () => OnSoapUpdated?.Invoke()
    ));
}
```

### 실행 흐름도

```
ActivateSoap() 호출
      │
      ▼
┌─────────────────┐
│ IsAnyRunning()? │─── Yes ──▶ return; (거부!)
│ soapLevel <= 0? │
└────────┬────────┘
         │ No
         ▼
┌─────────────────┐
│ 기존 코루틴     │─── 있음 ──▶ StopCoroutine()
│ 체크            │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│ StartCoroutine  │
│ (RunDispenser)  │
└────────┬────────┘
         │
         ▼
    비누 분사 시작! 🧴
```

---

## 람다 표현식 이해하기

```csharp
setter: v => stationData.isSoapRunning = v
```

이게 뭘까요? **람다 표현식(Lambda Expression)**이에요!

```
v => stationData.isSoapRunning = v

해석:
├── v          : 입력 파라미터 (bool 값)
├── =>         : "이것을 가지고"
└── stationData.isSoapRunning = v : "이렇게 해라"

풀어쓰면:
void SetSoapRunning(bool v)
{
    stationData.isSoapRunning = v;
}
```

### 왜 람다를 쓰나요?

RunDispenser 함수를 **재사용**하기 위해서예요!

```csharp
// 비누용
setter: v => stationData.isSoapRunning = v

// 물용
setter: v => stationData.isWaterRunning = v

// 에어용
setter: v => stationData.isAirRunning = v

→ 같은 RunDispenser를 3가지 경우에 모두 사용!
→ 코드 중복 제거! ✅
```

---

## 세 가지 Activate 함수 비교

| 함수 | 조건 | 지속 시간 | 추가 동작 |
|------|------|----------|----------|
| ActivateSoap() | 비누 잔량 > 0 | 3초 | UseSoap() 호출 |
| ActivateWater() | - | 10초 | - |
| ActivateAir() | - | 10초 | - |

```csharp
// 물과 에어는 비누 잔량 체크 없음!
public void ActivateWater()
{
    if (IsAnyRunning())  // 인터락만 체크
    {
        return;
    }
    // ... 물 분사 시작
}

public void ActivateAir()
{
    if (IsAnyRunning())  // 인터락만 체크
    {
        return;
    }
    // ... 에어 작동 시작
}
```

---

## 파티클 시스템 연동

```csharp
[Header("Particles")]
public ParticleSystem soapParticle;   // 🧴 비누 거품
public ParticleSystem waterParticle;  // 💧 물방울
public ParticleSystem airParticle;    // 💨 에어 바람
```

Inspector에서 파티클 시스템을 연결하면,
분사 시 자동으로 재생됩니다!

```
분사 시작
    │
    ├── particle.gameObject.SetActive(true)  ← 오브젝트 활성화
    └── particle.Play()                       ← 파티클 재생!

분사 종료
    │
    └── particle.Stop()                       ← 파티클 정지
```

---

## 코루틴 레퍼런스 관리

```csharp
private Coroutine _soapCoroutine;
private Coroutine _waterCoroutine;
private Coroutine _airCoroutine;
```

왜 코루틴 참조를 저장해 둘까요?

```
1. 중복 실행 방지
   └── 빠르게 버튼 여러 번 누르면?
       이전 코루틴 정리 후 새로 시작!

2. 중간 취소 가능
   └── 필요하면 StopCoroutine(_soapCoroutine)으로 중단

3. 상태 확인 가능
   └── _soapCoroutine != null 이면 실행 중
```

---

## 자주 묻는 질문

**Q: 왜 직접 Thread를 쓰지 않고 Coroutine을 쓰나요?**

A: Unity는 메인 스레드에서만 게임 오브젝트를 조작할 수 있어요.
   별도 Thread를 쓰면 UI 업데이트, 파티클 제어 등이 안 돼요.
   Coroutine은 메인 스레드에서 실행되면서 비동기 처럼 동작해서 안전합니다.

**Q: onStart와 onEnd 콜백은 왜 분리했나요?**

A: 시작과 종료 시 다른 동작이 필요해서요.
   - onStart: 비누 사용 기록, 타이머 시작
   - onEnd: UI 상태 갱신, 다음 동작 허용

---

## 다음 챕터 미리보기

다음 챕터에서는 **Coroutine**을 더 자세히 살펴봅니다.
yield return이 뭔지, 시간 제어는 어떻게 하는지 알아볼 거예요!
