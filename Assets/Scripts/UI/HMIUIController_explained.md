# HMIUIController.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/UI/HMIUIController.cs`
- **목적**: UI Toolkit 기반 HMI(Human-Machine Interface) 컨트롤러. UXML로 정의된 UI 요소를 C# 코드와 바인딩하고, StationController/NetworkManager의 이벤트에 반응하여 UI를 실시간 갱신.

## 아키텍처 역할 (MVC 패턴의 View)

```
┌─────────────────────────────────────────────────────────────┐
│                    HMIUIController (View)                    │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐   │
│  │ UI Toolkit   │←──→│ StationData  │←──→│ StationCtrl  │   │
│  │ (UXML/USS)   │    │ (Model)      │    │ (Controller) │   │
│  └──────────────┘    └──────────────┘    └──────────────┘   │
│         ↑                   ↑                    ↑           │
│         │                   │                    │           │
│  ┌──────┴───────────────────┴────────────────────┴──────┐   │
│  │               NetworkManager (PLC 통신)               │   │
│  └───────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## UI 요소 매핑

| 변수명 | UXML name | 용도 |
|--------|-----------|------|
| `_datetimeLabel` | datetime-label | 현재 날짜/시간 표시 |
| `_statusText` | status-text | 시스템 상태 텍스트 |
| `_statusLed` | status-led | 상태 LED (정상/주의/오류) |
| `_gaugeFill` | gauge-fill | 비누 잔량 게이지 바 |
| `_gaugePctLabel` | gauge-pct-label | 잔량 퍼센트 텍스트 |
| `_btnSoap/Water/Air` | btn-soap/water/air | 동작 버튼 |
| `_timerSoap/Water/Air` | timer-soap/water/air | 남은 시간 표시 |
| `_ledSoap/Water/Air` | led-soap/water/air | 동작 상태 LED |
| `_viewer3D` | 3d-viewer | 3D 모델 RenderTexture 표시 |

## 코드 분석

### Line 1-3: using 문
```csharp
using UnityEngine;
using UnityEngine.UIElements;
using System;
```
- `UnityEngine.UIElements`: UI Toolkit (UXML/USS) API
- `System`: `DateTime` 클래스 사용

### Line 5-14: 클래스 선언 및 Inspector 참조
```csharp
public class HMIUIController : MonoBehaviour
{
    [Header("References")]
    public UIDocument uiDocument;
    public StationController stationController;
    public RenderTexture viewerRenderTexture;

    [Header("Network")]
    public NetworkManager networkManager;
}
```
- `UIDocument`: UXML 파일이 연결된 UI Toolkit 컴포넌트
- `RenderTexture`: 3D 뷰어 카메라의 렌더 타겟
- `[Header]`: Inspector에서 섹션 구분용 라벨

### Line 15-28: UI 요소 캐시 및 상태
```csharp
private Label _datetimeLabel;
private VisualElement _gaugeFill;
private float _soapRemain, _waterRemain, _airRemain;
private float _lastSecond;
```
- UI 요소를 Start()에서 캐싱하여 매 프레임 검색 오버헤드 제거
- `_xxxRemain`: 각 동작의 남은 시간 (초)
- `_lastSecond`: 1초 간격 갱신용 타이머

### Line 30-90: Start 메서드
```csharp
void Start()
{
    var root = uiDocument.rootVisualElement;
    _datetimeLabel = root.Q<Label>("datetime-label");
    // ...
}
```

**UI 요소 쿼리:**
- `root.Q<T>("name")`: UXML에서 `name="xxx"` 속성으로 요소 검색
- `Q<Label>()`: Label 타입으로 캐스팅
- `Q<VisualElement>()`: 일반 컨테이너/요소

**RenderTexture 바인딩:**
```csharp
_viewer3D.style.backgroundImage = Background.FromRenderTexture(viewerRenderTexture);
```
- 3D 뷰어 카메라의 렌더 결과를 UI 요소 배경으로 표시

**이벤트 등록:**
```csharp
_btnSoap.RegisterCallback<ClickEvent>(_ => {
    stationController.ActivateSoap();
    networkManager?.WriteSoapButton(true);
});
```
- `RegisterCallback<T>()`: UI Toolkit의 이벤트 리스너 등록
- `ClickEvent`: 마우스/터치 클릭 이벤트
- `?.`: null 조건부 연산자 (networkManager가 null이면 스킵)

**이벤트 구독:**
```csharp
stationController.OnSoapUpdated += RefreshSoapUI;
networkManager.OnConnectionChanged += OnNetworkConnectionChanged;
```
- `+=`: C# 이벤트 구독 (델리게이트 추가)

### Line 92-121: Update 메서드
```csharp
void Update()
{
    // 1초 간격 날짜시간 갱신
    if (Time.time - _lastSecond >= 1f)
    {
        _lastSecond = Time.time;
        _datetimeLabel.text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // 타이머 카운트다운
    if (stationController.stationData.isSoapRunning)
    {
        _soapRemain = Mathf.Max(0f, _soapRemain - Time.deltaTime);
        _timerSoap.text = _soapRemain > 0 ? $"{_soapRemain:F1}초" : "";
    }
}
```
**날짜시간 갱신:**
- `Time.time`: 게임 시작 후 경과 시간 (초)
- 매 프레임 대신 1초 간격으로 갱신 (성능 최적화)

**타이머 카운트다운:**
- `Time.deltaTime`: 이전 프레임과의 시간 차이
- `Mathf.Max(0f, ...)`: 음수 방지
- `:F1`: 소수점 1자리 포맷

### Line 123-144: 네트워크 콜백
```csharp
private void OnNetworkConnectionChanged(bool connected)
{
    if (!connected)
    {
        _statusLed.AddToClassList("warning");
        _statusText.text = "시스템 상태: 통신 오류";
    }
}
```
- PLC 연결 끊김 시 UI에 경고 표시
- `AddToClassList()`: USS 클래스 추가 (CSS와 유사)

### Line 146-190: StationController 콜백
```csharp
void RefreshSoapUI()
{
    bool running = stationController.stationData.isSoapRunning;
    SetBtnActive(_btnSoap, _ledSoap, running);
    if (running)
        _soapRemain = 3f;  // 비누 3초
    else
        _timerSoap.text = "";
}
```
- 동작 시작 시 타이머 초기화 (비누 3초, 물/에어 10초)
- 동작 종료 시 타이머 텍스트 클리어

### Line 192-204: SetBtnActive 헬퍼
```csharp
void SetBtnActive(VisualElement btn, VisualElement led, bool active)
{
    if (active)
    {
        btn.AddToClassList("active");
        led.AddToClassList("active");
    }
    else
    {
        btn.RemoveFromClassList("active");
        led.RemoveFromClassList("active");
    }
}
```
- `AddToClassList()` / `RemoveFromClassList()`: USS 클래스 토글
- USS에서 `.active` 클래스에 스타일 정의 (색상 변경 등)

### Line 206-221: UpdateGauge 메서드
```csharp
void UpdateGauge(float pct)
{
    _gaugeFill.style.height = Length.Percent(pct);
    _gaugePctLabel.text = $"{pct:F0}%";

    _gaugeFill.RemoveFromClassList("warning");
    _gaugeFill.RemoveFromClassList("error");
    if (pct <= 0f)
        _gaugeFill.AddToClassList("error");
    else if (pct <= 20f)
        _gaugeFill.AddToClassList("warning");
}
```
- `Length.Percent()`: 퍼센트 단위 길이 값
- `style.height`: 인라인 스타일로 높이 설정
- 잔량에 따른 색상 클래스 적용 (0%=오류, ≤20%=경고)

### Line 223-241: UpdateStatusLED 메서드
```csharp
void UpdateStatusLED(StationData.SystemStatus status)
{
    _statusLed.RemoveFromClassList("warning");
    _statusLed.RemoveFromClassList("error");
    switch (status)
    {
        case StationData.SystemStatus.Normal:
            _statusText.text = "시스템 상태: 정상";
            break;
        case StationData.SystemStatus.Warning:
            _statusLed.AddToClassList("warning");
            _statusText.text = "시스템 상태: 주의";
            break;
        case StationData.SystemStatus.Error:
            _statusLed.AddToClassList("error");
            _statusText.text = "시스템 상태: 오류";
            break;
    }
}
```
- 먼저 모든 상태 클래스 제거 후 현재 상태만 추가
- USS에서 `.warning { background-color: yellow; }` 등 정의

### Line 243-255: OnDestroy 메서드
```csharp
void OnDestroy()
{
    stationController.OnSoapUpdated -= RefreshSoapUI;
    networkManager.OnConnectionChanged -= OnNetworkConnectionChanged;
}
```
- `-=`: 이벤트 구독 해제 (메모리 누수 방지)
- GameObject 파괴 시 호출되어 정리

## UI Toolkit 핵심 API

| API | 설명 |
|-----|------|
| `root.Q<T>("name")` | 이름으로 UI 요소 검색 |
| `RegisterCallback<TEvent>()` | 이벤트 리스너 등록 |
| `AddToClassList("class")` | USS 클래스 추가 |
| `RemoveFromClassList("class")` | USS 클래스 제거 |
| `element.style.xxx` | 인라인 스타일 설정 |
| `Length.Percent(n)` | 퍼센트 단위 값 |
| `Background.FromRenderTexture()` | RenderTexture를 배경으로 |

## 이벤트 흐름 다이어그램

```
[버튼 클릭]
    │
    ├──→ StationController.ActivateSoap()
    │         │
    │         └──→ OnSoapUpdated 이벤트 발생
    │                   │
    │                   └──→ RefreshSoapUI() 호출
    │                             │
    │                             └──→ UI 갱신 (버튼 활성화, 타이머 시작)
    │
    └──→ NetworkManager.WriteSoapButton(true)
              │
              └──→ PLC에 M0 비트 쓰기

[PLC 폴링]
    │
    └──→ NetworkManager.OnConnectionChanged 이벤트
              │
              └──→ OnNetworkConnectionChanged() 호출
                        │
                        └──→ 상태 LED 갱신
```

## 타이머 동작 시간

| 동작 | 시간 | 관련 변수 |
|------|------|----------|
| 비누 | 3초 | `_soapRemain = 3f` |
| 물 | 10초 | `_waterRemain = 10f` |
| 에어 | 10초 | `_airRemain = 10f` |
