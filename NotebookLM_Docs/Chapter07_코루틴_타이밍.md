# Chapter 07: 코루틴과 타이밍 제어

## 코루틴(Coroutine)이란?

**코루틴**은 "일시 정지가 가능한 함수"예요.

일반 함수는 시작하면 끝까지 쭉 실행되지만,
코루틴은 중간에 "여기서 잠깐 멈춰!" 할 수 있어요.

```
일반 함수:
─────────────────────────────────────────
void NormalFunction()
{
    Step1();
    Step2();  // Step1 끝나야 실행
    Step3();  // Step2 끝나야 실행
}
→ 전체가 한 프레임에서 실행됨

코루틴:
─────────────────────────────────────────
IEnumerator CoroutineFunction()
{
    Step1();
    yield return new WaitForSeconds(1f);  // 1초 대기
    Step2();
    yield return new WaitForSeconds(2f);  // 2초 대기
    Step3();
}
→ 여러 프레임에 걸쳐 실행됨
```

---

## yield return의 마법

> 💡 **핵심 개념**: yield return은 "여기서 일시정지하고 나중에 계속해줘"라는 의미예요.

```csharp
IEnumerator Example()
{
    Debug.Log("시작!");

    yield return null;  // 다음 프레임까지 대기

    Debug.Log("1프레임 지남!");

    yield return new WaitForSeconds(2f);  // 2초 대기

    Debug.Log("2초 지남!");
}
```

### 타임라인 시각화

```
프레임 1   프레임 2   ...   2초 후
   │          │              │
   ▼          ▼              ▼
"시작!"   "1프레임      "2초 지남!"
          지남!"

게임은 계속 돌아가면서 코루틴만 잠시 쉬어요!
```

---

## RunDispenser 함수 심층 분석

```csharp
private IEnumerator RunDispenser(
    Action<bool> setter,      // 상태 변경 함수
    float duration,            // 지속 시간 (초)
    ParticleSystem particle,   // 파티클 효과
    Action onStart,            // 시작 콜백
    Action onEnd)              // 종료 콜백
{
    // ═══════════════════════════════════════════
    // 🟢 시작 단계
    // ═══════════════════════════════════════════

    setter(true);  // 예: isSoapRunning = true

    if (particle != null)
    {
        particle.gameObject.SetActive(true);
        particle.Play();  // 파티클 재생!
    }

    onStart?.Invoke();  // 시작 콜백 호출

    // ═══════════════════════════════════════════
    // ⏳ 대기 단계 (핵심!)
    // ═══════════════════════════════════════════

    float elapsed = 0f;
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;  // 경과 시간 누적
        yield return null;           // 다음 프레임까지 대기
    }

    // ═══════════════════════════════════════════
    // 🔴 종료 단계
    // ═══════════════════════════════════════════

    setter(false);  // 예: isSoapRunning = false

    if (particle != null)
    {
        particle.Stop();  // 파티클 정지
    }

    onEnd?.Invoke();  // 종료 콜백 호출
}
```

---

## while 루프 vs WaitForSeconds

```csharp
// 방법 1: while 루프 (이 프로젝트에서 사용)
float elapsed = 0f;
while (elapsed < duration)
{
    elapsed += Time.deltaTime;
    yield return null;
}

// 방법 2: WaitForSeconds
yield return new WaitForSeconds(duration);
```

### 왜 while 루프를 쓸까요?

```
while 루프의 장점:
├── elapsed 값을 외부에서 읽을 수 있음 (타이머 표시용)
├── 중간에 조건 검사 가능 (취소 로직 추가 가능)
└── 더 세밀한 제어 가능

WaitForSeconds의 장점:
├── 코드가 더 간결함
├── 내부 최적화가 되어 있음
└── 가비지 생성이 적음
```

---

## Time.deltaTime 이해하기

```csharp
elapsed += Time.deltaTime;
```

**Time.deltaTime**은 "이전 프레임부터 지금까지 경과한 시간"이에요.

```
60 FPS 기준:
─────────────────────────────────────────
프레임 1 → 프레임 2:  deltaTime ≈ 0.0167초 (1/60)
프레임 2 → 프레임 3:  deltaTime ≈ 0.0167초
...
60번 반복하면:        total ≈ 1.0초

30 FPS 기준:
─────────────────────────────────────────
프레임 1 → 프레임 2:  deltaTime ≈ 0.0333초 (1/30)
프레임 2 → 프레임 3:  deltaTime ≈ 0.0333초
...
30번 반복하면:        total ≈ 1.0초

→ FPS가 달라도 실제 시간은 같음! ✅
```

---

## 코루틴 시작과 중지

### 시작하기

```csharp
// 방법 1: 직접 호출
StartCoroutine(RunDispenser(...));

// 방법 2: 참조 저장 (나중에 중지하려면 이렇게!)
_soapCoroutine = StartCoroutine(RunDispenser(...));
```

### 중지하기

```csharp
// 특정 코루틴 중지
if (_soapCoroutine != null)
{
    StopCoroutine(_soapCoroutine);
    _soapCoroutine = null;
}

// 이 오브젝트의 모든 코루틴 중지
StopAllCoroutines();
```

---

## 코루틴 중첩과 체이닝

```csharp
// 코루틴 안에서 다른 코루틴 호출
IEnumerator SequenceExample()
{
    Debug.Log("비누 시작");
    yield return StartCoroutine(RunDispenser(/* 비누 설정 */));

    Debug.Log("물 시작");
    yield return StartCoroutine(RunDispenser(/* 물 설정 */));

    Debug.Log("에어 시작");
    yield return StartCoroutine(RunDispenser(/* 에어 설정 */));

    Debug.Log("모든 단계 완료!");
}
```

### 실행 흐름

```
시간 ─────────────────────────────────────────────────▶

     │ 3초 │    │ 10초 │    │ 10초 │
     ├─────┼────┼──────┼────┼──────┤
     │비누 │대기│  물  │대기│ 에어 │

"비누  "물    "에어   "모든
시작"  시작"  시작"   단계
                     완료!"
```

---

## GetRemaining 함수들

```csharp
public float GetSoapRemaining()
{
    return GetRemaining(_soapCoroutine, 3f);
}

public float GetWaterRemaining()
{
    return GetRemaining(_waterCoroutine, 10f);
}

public float GetAirRemaining()
{
    return GetRemaining(_airCoroutine, 10f);
}

private float GetRemaining(Coroutine c, float total)
{
    if (c != null)
    {
        return total;
    }
    return 0f;
}
```

### 한계점

```
현재 구현의 한계:
─────────────────────────────────────────
코루틴이 있으면 total(전체 시간) 반환
코루틴이 없으면 0 반환

문제: 정확한 남은 시간을 알 수 없음!
(코루틴 내부의 elapsed 값에 접근 불가)

실제 타이머는 HMIUIController에서 별도로 관리:
_soapRemain -= Time.deltaTime;
```

---

## 자주 묻는 질문

**Q: 코루틴은 별도 스레드에서 실행되나요?**

A: 아니요! 코루틴은 **메인 스레드**에서 실행돼요.
   "비동기처럼 보이는" 동기 코드입니다.
   그래서 Unity API를 안전하게 호출할 수 있어요.

**Q: 코루틴을 너무 많이 쓰면 성능에 문제가 있나요?**

A: 일반적으로 괜찮아요. 하지만 매 프레임 새 코루틴을 생성하면
   가비지 컬렉션 문제가 생길 수 있어요.
   한 번 시작하고 오래 돌리는 건 OK!

**Q: async/await와 뭐가 다른가요?**

A: 코루틴은 Unity 전용, async/await는 C# 표준이에요.
   코루틴이 Unity 라이프사이클과 더 잘 맞고,
   에디터에서 일시정지해도 같이 멈춰요.

---

## 다음 챕터 미리보기

다음 챕터에서는 **UI 시스템**을 살펴봅니다.
HMIUIController가 화면을 어떻게 그리는지,
이벤트 처리는 어떻게 하는지 알아볼 거예요!
