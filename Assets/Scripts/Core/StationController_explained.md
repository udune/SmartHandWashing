# StationController.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Core/StationController.cs`
- **목적**: 손 세정 스테이션의 3가지 디스펜서(비누, 물, 에어)를 제어하는 컨트롤러. 인터락 로직으로 동시 작동을 방지하고, 코루틴 기반 타이머로 각 디스펜서의 작동 시간을 관리

## 아키텍처 위치
```
MVC 패턴에서의 역할:
├── Model: StationData (ScriptableObject)
├── View: HMIUIController (UI 표시)
└── Controller: StationController ← 이 파일
```

## 코드 분석

### Line 1-3: 네임스페이스 및 using 문
```csharp
using UnityEngine;
using System;
using System.Collections;
```
- `UnityEngine`: MonoBehaviour, ParticleSystem, Coroutine 등 Unity 핵심 API
- `System`: Action 델리게이트 타입 사용
- `System.Collections`: IEnumerator 인터페이스 (코루틴 반환 타입)

### Line 5-6: 클래스 선언
```csharp
public class StationController : MonoBehaviour
```
- `MonoBehaviour` 상속으로 Unity 생명주기 메서드와 코루틴 사용 가능
- 씬의 "StationManager" GameObject에 부착되어 실행

### Line 7-13: Inspector 노출 필드
```csharp
[Header("Data")]
public StationData stationData;

[Header("Particles")]
public ParticleSystem soapParticle;
public ParticleSystem waterParticle;
public ParticleSystem airParticle;
```
- `[Header]`: Inspector에서 필드를 그룹화하여 표시
- `StationData`: ScriptableObject 참조 (상태 데이터 저장소)
- `ParticleSystem`: 각 디스펜서 작동 시 재생할 파티클 이펙트

### Line 15-17: 이벤트 선언
```csharp
public event Action OnSoapUpdated;
public event Action OnWaterUpdated;
public event Action OnAirUpdated;
```
- `event Action`: 매개변수 없는 이벤트 델리게이트
- 옵저버 패턴 구현: HMIUIController가 구독하여 UI 갱신 트리거
- 디스펜서 시작/종료 시 `?.Invoke()`로 호출

### Line 19-21: 코루틴 참조 필드
```csharp
private Coroutine _soapCoroutine;
private Coroutine _waterCoroutine;
private Coroutine _airCoroutine;
```
- 실행 중인 코루틴 핸들 저장
- 중복 실행 방지 및 `StopCoroutine()` 호출에 사용
- `null` 체크로 현재 실행 여부 판단 가능

### Line 23-29: Awake 메서드
```csharp
void Awake()
{
    stationData.isSoapRunning = false;
    stationData.isWaterRunning = false;
    stationData.isAirRunning = false;
}
```
- `Awake()`: Start()보다 먼저 호출되는 초기화 콜백
- ScriptableObject는 에디터에서 값이 영속되므로 Play Mode 시작 시 강제 초기화 필요
- 이전 실행의 잔여 상태가 남아있는 문제 방지

### Line 31-49: IsAnyRunning 메서드 (인터락 체크)
```csharp
private bool IsAnyRunning()
{
    if (stationData.isSoapRunning) return true;
    if (stationData.isWaterRunning) return true;
    if (stationData.isAirRunning) return true;
    return false;
}
```
- **인터락(Interlock) 패턴**: 산업 자동화에서 안전을 위해 동시 작동 방지
- 어떤 디스펜서든 실행 중이면 `true` 반환
- 단축 평가(short-circuit)로 첫 번째 true 발견 시 즉시 반환

### Line 51-75: ActivateSoap 메서드
```csharp
public void ActivateSoap()
{
    if (IsAnyRunning() || stationData.soapLevel <= 0f) return;

    if (_soapCoroutine != null) StopCoroutine(_soapCoroutine);

    _soapCoroutine = StartCoroutine(RunDispenser(
        setter: v => stationData.isSoapRunning = v,
        duration: 3f,
        particle: soapParticle,
        onStart: () => { stationData.UseSoap(); OnSoapUpdated?.Invoke(); },
        onEnd:   () => OnSoapUpdated?.Invoke()
    ));
}
```
- **가드 절(Guard Clause)**: 인터락 또는 비누 부족 시 즉시 반환
- 기존 코루틴 정지 후 새로 시작 (중복 실행 방지)
- `StartCoroutine()`: 코루틴 시작하고 핸들 반환
- **Lambda 표현식**으로 setter/onStart/onEnd 콜백 전달
- `duration: 3f`: 비누는 3초간 작동

### Line 77-97: ActivateWater 메서드
```csharp
public void ActivateWater()
{
    if (IsAnyRunning()) return;
    // ... 구조 동일
    duration: 10f,
}
```
- 물은 잔량 체크 없음 (무한 공급 가정)
- `duration: 10f`: 물은 10초간 작동

### Line 99-119: ActivateAir 메서드
```csharp
public void ActivateAir()
{
    if (IsAnyRunning()) return;
    // ... 구조 동일
    duration: 10f,
}
```
- 에어도 잔량 체크 없음
- `duration: 10f`: 에어 드라이도 10초간 작동

### Line 121-147: RunDispenser 코루틴 (핵심 로직)
```csharp
private IEnumerator RunDispenser(
    Action<bool> setter, float duration,
    ParticleSystem particle,
    Action onStart, Action onEnd)
{
    setter(true);                              // 상태 ON
    if (particle != null)
    {
        particle.gameObject.SetActive(true);   // 파티클 오브젝트 활성화
        particle.Play();                       // 파티클 재생
    }
    onStart?.Invoke();                         // 시작 콜백

    float elapsed = 0f;
    while (elapsed < duration)                 // 타이머 루프
    {
        elapsed += Time.deltaTime;             // 프레임 시간 누적
        yield return null;                     // 다음 프레임까지 대기
    }

    setter(false);                             // 상태 OFF
    if (particle != null) particle.Stop();     // 파티클 정지
    onEnd?.Invoke();                           // 종료 콜백
}
```
- `IEnumerator`: 코루틴 반환 타입
- **제네릭 디스펜서 로직**: Action 파라미터로 비누/물/에어 공통 처리
- `yield return null`: 1프레임 대기 (Update와 동기화)
- `Time.deltaTime`: 이전 프레임과의 시간 차이 (초 단위)
- `?.Invoke()`: null-conditional로 구독자 없을 때 안전하게 호출

### Line 149-172: 남은 시간 조회 메서드
```csharp
public float GetSoapRemaining() => GetRemaining(_soapCoroutine, 3f);
public float GetWaterRemaining() => GetRemaining(_waterCoroutine, 10f);
public float GetAirRemaining() => GetRemaining(_airCoroutine, 10f);

private float GetRemaining(Coroutine c, float total)
{
    if (c != null) return total;
    return 0f;
}
```
- UI에서 폴링(polling) 방식으로 호출
- **한계점**: 현재 구현은 실제 남은 시간이 아닌 전체 시간 반환
- 정확한 남은 시간 표시를 위해서는 `elapsed` 값을 필드로 노출해야 함

## 개선 제안

### 1. 남은 시간 정확도
현재 `GetRemaining()`은 코루틴 존재 여부만 확인하여 전체 시간을 반환합니다. 실제 남은 시간을 알려면:
```csharp
private float _soapElapsed;
public float GetSoapRemaining() => Mathf.Max(0, 3f - _soapElapsed);
```

### 2. 코루틴 정리
`OnDisable()` 또는 `OnDestroy()`에서 실행 중인 코루틴 정리 권장:
```csharp
void OnDisable()
{
    if (_soapCoroutine != null) StopCoroutine(_soapCoroutine);
    // ...
}
```

## 의존성 관계
```
StationController
├── StationData (ScriptableObject)
│   ├── isSoapRunning, isWaterRunning, isAirRunning (상태)
│   ├── soapLevel (잔량)
│   └── UseSoap() (소모 메서드)
├── ParticleSystem (x3) - 시각적 피드백
└── HMIUIController (이벤트 구독자) - UI 갱신
```
