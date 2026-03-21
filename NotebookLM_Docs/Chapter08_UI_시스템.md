# Chapter 08: UI 시스템 - HMIUIController

## HMI란?

**HMI**는 **Human-Machine Interface**의 약자예요.
사람과 기계가 소통하는 접점이죠.

ATM 기계를 생각해 보세요:
- 화면에 버튼이 있고
- 잔액이 표시되고
- 상태 표시등이 깜빡이죠

HMIUIController는 바로 그 역할을 합니다!

---

## UI 요소 캐싱

```csharp
// UI 요소 캐시 - 한 번만 찾아서 저장!
private Label _datetimeLabel;      // 날짜/시간
private Label _statusText;          // 상태 텍스트
private VisualElement _statusLed;   // 상태 LED
private VisualElement _gaugeFill;   // 게이지 채움
private Label _gaugePctLabel;       // 퍼센트 표시
private VisualElement _btnSoap, _btnWater, _btnAir;  // 버튼들
private Label _timerSoap, _timerWater, _timerAir;    // 타이머
private VisualElement _ledSoap, _ledWater, _ledAir;  // LED들
```

### 왜 캐싱을 할까요?

```
❌ 나쁜 방법: 매번 찾기
─────────────────────────────────────────
void Update()  // 초당 60번 호출!
{
    var label = root.Q<Label>("datetime-label");  // 매번 검색!
    label.text = "2024-01-15 14:30:25";
}
→ UI 트리를 매번 탐색... 느림!

✅ 좋은 방법: 미리 찾아두기
─────────────────────────────────────────
void Start()  // 1번만 호출
{
    _datetimeLabel = root.Q<Label>("datetime-label");  // 저장!
}

void Update()  // 초당 60번 호출
{
    _datetimeLabel.text = "2024-01-15 14:30:25";  // 바로 접근!
}
→ 변수 접근만 하면 됨... 빠름!
```

> 💡 **핵심 포인트**: Q<T>("name")는 UI 트리를 검색하는 비용이 있어요.
> Start()에서 한 번 찾아서 변수에 저장해 두세요!

---

## Q<T>() 메서드 이해하기

```csharp
var root = uiDocument.rootVisualElement;

// 특정 이름의 Label 찾기
_datetimeLabel = root.Q<Label>("datetime-label");

// 특정 이름의 VisualElement 찾기
_statusLed = root.Q<VisualElement>("status-led");
```

### UXML과의 연결

```xml
<!-- MainHMI.uxml -->
<ui:Label name="datetime-label" text="2024-01-15"/>
<ui:VisualElement name="status-led" class="led"/>
```

```
UXML의 name 속성 ──────▶ Q<T>("name")으로 찾기
     │                          │
     ▼                          ▼
name="datetime-label"    Q<Label>("datetime-label")
```

---

## 버튼 이벤트 등록

```csharp
void Start()
{
    // 비누 버튼 클릭 이벤트
    _btnSoap.RegisterCallback<ClickEvent>(_ =>
    {
        stationController.ActivateSoap();
        networkManager?.WriteSoapButton(true);
    });

    // 물 버튼 클릭 이벤트
    _btnWater.RegisterCallback<ClickEvent>(_ =>
    {
        stationController.ActivateWater();
        networkManager?.WriteWaterButton(true);
    });

    // 에어 버튼 클릭 이벤트
    _btnAir.RegisterCallback<ClickEvent>(_ =>
    {
        stationController.ActivateAir();
        networkManager?.WriteAirButton(true);
    });
}
```

### 이벤트 흐름

```
사용자가 비누 버튼 클릭!
         │
         ▼
RegisterCallback<ClickEvent> 발동!
         │
         ├──▶ stationController.ActivateSoap()
         │           │
         │           ▼
         │      비누 분사 시작 🧴
         │
         └──▶ networkManager?.WriteSoapButton(true)
                     │
                     ▼
                PLC에 신호 전송 📡
```

---

## Update()에서 하는 일들

```csharp
void Update()
{
    // ═══════════════════════════════════════════
    // 1. 날짜/시간 업데이트 (1초마다)
    // ═══════════════════════════════════════════

    if (Time.time - _lastSecond >= 1f)
    {
        _lastSecond = Time.time;
        _datetimeLabel.text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    // ═══════════════════════════════════════════
    // 2. 타이머 카운트다운
    // ═══════════════════════════════════════════

    if (stationController.stationData.isSoapRunning)
    {
        _soapRemain = Mathf.Max(0f, _soapRemain - Time.deltaTime);
        _timerSoap.text = _soapRemain > 0 ? $"{_soapRemain:F1}초" : "";
    }

    // ... 물, 에어도 동일하게 처리

    // ═══════════════════════════════════════════
    // 3. 게이지 & LED 업데이트
    // ═══════════════════════════════════════════

    UpdateGauge(stationController.stationData.soapLevel);
    UpdateStatusLED(stationController.stationData.systemStatus);
}
```

---

## 게이지 업데이트 함수

```csharp
void UpdateGauge(float pct)
{
    // 높이를 퍼센트로 설정
    _gaugeFill.style.height = Length.Percent(pct);
    _gaugePctLabel.text = $"{pct:F0}%";

    // 기존 스타일 클래스 제거
    _gaugeFill.RemoveFromClassList("warning");
    _gaugeFill.RemoveFromClassList("error");

    // 상태에 따른 색상 적용
    if (pct <= 0f)
    {
        _gaugeFill.AddToClassList("error");    // 🔴 빨간색
    }
    else if (pct <= 20f)
    {
        _gaugeFill.AddToClassList("warning");  // 🟡 노란색
    }
    // 20% 초과면 기본 색상 (녹색)
}
```

### CSS 클래스 토글

```css
/* USS 파일에서 정의 */
.gauge-fill {
    background-color: #4CAF50;  /* 기본: 녹색 */
}

.gauge-fill.warning {
    background-color: #FFC107;  /* 주의: 노란색 */
}

.gauge-fill.error {
    background-color: #F44336;  /* 오류: 빨간색 */
}
```

```
AddToClassList("warning")
     │
     ▼
.gauge-fill 에 .warning 추가됨
     │
     ▼
CSS 규칙 적용: background-color: #FFC107
     │
     ▼
게이지 색상이 노란색으로 변경! 🟡
```

---

## 상태 LED 업데이트

```csharp
void UpdateStatusLED(StationData.SystemStatus status)
{
    // 기존 클래스 초기화
    _statusLed.RemoveFromClassList("warning");
    _statusLed.RemoveFromClassList("error");

    switch (status)
    {
        case StationData.SystemStatus.Normal:
            _statusText.text = "시스템 상태: 정상";
            // 클래스 없음 = 기본 녹색
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

---

## ModelRotator: 3D 모델 회전

```csharp
void OnMouseMove(MouseMoveEvent e)
{
    if (!_isDragging || modelRoot == null)
    {
        return;
    }

    // 마우스 이동량 계산
    float deltaX = e.localMousePosition.x - _lastMousePos.x;

    // Y축 기준 회전
    modelRoot.Rotate(Vector3.up, -deltaX * rotationSpeed, Space.World);

    _lastMousePos = e.localMousePosition;
}
```

### 회전 원리

```
마우스 오른쪽으로 드래그 →
         │
         ▼
deltaX = +50 (양수)
         │
         ▼
Rotate(Vector3.up, -50 * 0.3) = -15도
         │
         ▼
모델이 왼쪽으로 회전! ↺

마우스 방향과 반대로 회전해야 자연스러워요!
```

---

## 이벤트 구독 해제

```csharp
void OnDestroy()
{
    // StationController 이벤트 해제
    stationController.OnSoapUpdated  -= RefreshSoapUI;
    stationController.OnWaterUpdated -= RefreshWaterUI;
    stationController.OnAirUpdated   -= RefreshAirUI;

    // NetworkManager 이벤트 해제
    if (networkManager != null)
    {
        networkManager.OnConnectionChanged -= OnNetworkConnectionChanged;
        networkManager.OnStatusChanged     -= OnNetworkStatusChanged;
    }
}
```

> ⚠️ **중요**: 반드시 OnDestroy에서 이벤트를 해제하세요!
> 안 하면 메모리 누수가 발생합니다!

---

## 자주 묻는 질문

**Q: UI Toolkit은 UGUI와 뭐가 다른가요?**

A: UI Toolkit은 웹 기술(HTML/CSS)과 비슷해요.
   - UXML = HTML (구조)
   - USS = CSS (스타일)
   - 더 현대적이고 유지보수 쉬움!

**Q: Length.Percent(pct)는 뭔가요?**

A: CSS의 % 단위예요. `height: 80%` 같은 거죠.
   UI Toolkit에서 스타일을 코드로 설정할 때 사용합니다.

---

## 다음 챕터 미리보기

다음 챕터에서는 **PLC 통신 기초**를 살펴봅니다.
SLMP 프로토콜이 뭔지, D/M 디바이스가 뭔지 알아볼 거예요!
