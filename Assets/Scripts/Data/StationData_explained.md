# StationData.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Data/StationData.cs`
- **목적**: 손 세정 스테이션의 상태 데이터를 저장하는 ScriptableObject. MVC 패턴의 Model 역할

## 아키텍처 위치
```
MVC 패턴에서의 역할:
├── Model: StationData ← 이 파일 (데이터 저장소)
├── View: HMIUIController (UI 표시)
└── Controller: StationController (로직 처리)
```

## 코드 분석

### Line 1-2: 네임스페이스 및 using 문
```csharp
using UnityEngine;
using System;
```
- `UnityEngine`: ScriptableObject, Mathf, Range 등 Unity 핵심 API
- `System`: Action 델리게이트 타입 (이벤트용)

### Line 4: CreateAssetMenu 어트리뷰트
```csharp
[CreateAssetMenu(menuName = "SmartWash/Station Data")]
```
- Unity 에디터의 **Create 메뉴**에 항목 추가
- `Assets` 폴더 우클릭 → Create → SmartWash → Station Data로 인스턴스 생성 가능
- 없으면 `ScriptableObject.CreateInstance<T>()`로만 생성 가능

### Line 5: 클래스 선언
```csharp
public class StationData : ScriptableObject
```
- `ScriptableObject` 상속: 씬과 독립적인 데이터 에셋
- MonoBehaviour와 달리 **GameObject에 부착하지 않음**
- `.asset` 파일로 프로젝트에 저장됨

### Line 7-12: 비누 관련 필드
```csharp
[Header("Soap")]
[Range(0f, 100f)]
public float soapLevel = 100f;

public int soapUseCount = 0;
public float soapDecreasePerUse = 5f;
```
- `[Header]`: Inspector에서 필드 그룹 제목 표시
- `[Range]`: Inspector에서 슬라이더로 표시, 값 범위 제한
- `soapLevel`: 비누 잔량 (0~100%)
- `soapUseCount`: 누적 사용 횟수
- `soapDecreasePerUse`: 1회 사용당 감소량 (기본 5%)

### Line 14-17: 디스펜서 작동 상태 필드
```csharp
[Header("Running State")]
public bool isSoapRunning = false;
public bool isWaterRunning = false;
public bool isAirRunning  = false;
```
- 각 디스펜서의 현재 작동 여부
- `StationController.IsAnyRunning()`에서 인터락 체크에 사용
- UI LED 상태 표시에도 사용

### Line 19-24: SystemStatus 열거형
```csharp
public enum SystemStatus
{
    Normal,
    Warning,
    Error
}
```
- `Normal`: 정상 (비누 20% 초과)
- `Warning`: 주의 (비누 0% 초과 ~ 20% 이하)
- `Error`: 오류 (비누 0%)
- 중첩 열거형(Nested Enum): 클래스 내부에 정의되어 `StationData.SystemStatus`로 접근

### Line 26: 현재 시스템 상태
```csharp
public SystemStatus systemStatus = SystemStatus.Normal;
```
- 현재 시스템 상태 저장
- UI 헤더의 LED 색상 결정에 사용

### Line 28: 데이터 변경 이벤트
```csharp
public event Action OnDataChanged;
```
- `event Action`: 매개변수 없는 이벤트 델리게이트
- 데이터 변경 시 구독자에게 알림 (옵저버 패턴)
- 현재는 사용되지 않지만 확장 가능한 구조

### Line 30-46: UseSoap 메서드
```csharp
public void UseSoap()
{
    soapLevel = Mathf.Max(0f, soapLevel - soapDecreasePerUse);
    soapUseCount++;

    if (soapLevel <= 20f && soapLevel > 0f)
    {
        systemStatus = SystemStatus.Warning;
    }

    if (soapLevel <= 0f)
    {
        systemStatus = SystemStatus.Error;
    }

    OnDataChanged?.Invoke();
}
```
- `Mathf.Max(0f, ...)`: 음수 방지 (최소값 0)
- 비누 잔량에 따른 상태 자동 전환:
  - 20% 초과: 상태 변경 없음 (이전 상태 유지 또는 Normal)
  - 0% 초과 ~ 20% 이하: Warning
  - 0% 이하: Error
- `?.Invoke()`: null-conditional로 구독자 없을 때 안전하게 호출

> **주의**: PLC Master 패턴에서는 이 메서드가 직접 호출되지 않습니다.
> NetworkManager가 PLC에서 읽은 값으로 `soapLevel`을 갱신합니다.

### Line 48-57: ResetData 메서드
```csharp
public void ResetData()
{
    soapLevel = 100f;
    soapUseCount = 0;
    isSoapRunning = false;
    isWaterRunning = false;
    isAirRunning = false;
    systemStatus = SystemStatus.Normal;
    OnDataChanged?.Invoke();
}
```
- 모든 필드를 초기값으로 리셋
- 테스트 또는 시스템 재시작 시 사용
- 이벤트 발행으로 UI 갱신 트리거

## ScriptableObject 특징

### 영속성 (Persistence)
```
에디터 모드:
├── Play Mode 시작: soapLevel = 100%
├── 비누 사용: soapLevel = 95%
├── Play Mode 종료: soapLevel = 95% (값 유지!)  ← 주의
└── 다음 Play Mode: soapLevel = 95% (이전 값 유지)
```

이 때문에 `StationController.Awake()`에서 상태 필드를 강제 초기화합니다.

### 빌드 시 동작
- 빌드된 게임에서는 ScriptableObject 값이 읽기 전용
- 런타임 변경은 메모리에서만 유효, 앱 재시작 시 원본 복원

## 의존성 관계
```
StationData (Model)
├── StationController (읽기/쓰기)
│   ├── isSoapRunning, isWaterRunning, isAirRunning
│   └── UseSoap() 호출 (PLC Master 모드에서는 미사용)
├── HMIUIController (읽기)
│   ├── soapLevel → 게이지 표시
│   └── systemStatus → LED 색상
└── NetworkManager (쓰기)
    └── soapLevel, systemStatus (PLC에서 읽은 값으로 갱신)
```
