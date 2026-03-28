# AlarmBanner.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/UI/AlarmBanner.cs`
- **목적**: 비누 교체 예측 결과에 따라 상단 알람 배너를 표시/숨김

## 핵심 기능

| 기능 | 설명 |
|------|------|
| 주기적 체크 | 10초마다 예측 결과 확인 |
| 레벨별 스타일 | Warn(노란색), Alert(빨간색) CSS 클래스 적용 |
| 최적화 | 레벨 변경 시에만 클래스 교체 (불필요한 재드로우 방지) |

---

## 코드 분석

### Line 10-14: 상수 정의

```csharp
private const float CheckInterval = 10f;

private const string ClassHidden = "alarm-hidden";
private const string ClassWarn   = "alarm-warn";
private const string ClassAlert  = "alarm-alert";
```

| 상수 | 값 | 용도 |
|------|-----|------|
| `CheckInterval` | 10f | 예측 결과 확인 주기 (초) |
| `ClassHidden` | "alarm-hidden" | 배너 숨김 CSS 클래스 |
| `ClassWarn` | "alarm-warn" | 경고 스타일 CSS 클래스 (노란색) |
| `ClassAlert` | "alarm-alert" | 긴급 스타일 CSS 클래스 (빨간색) |

---

### Line 16-25: 필드 정의

```csharp
[Header("References")]
public UIDocument     uiDocument;
public AnalyticsPanel analyticsPanel;

private VisualElement _banner;
private Label         _message;
private Label         _detail;

private float      _checkTimer;
private AlarmLevel _currentLevel = AlarmLevel.None;
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `uiDocument` | `UIDocument` | UI 트리 접근용 (인스펙터 연결) |
| `analyticsPanel` | `AnalyticsPanel` | 예측 결과 제공자 |
| `_banner` | `VisualElement` | 알람 배너 컨테이너 |
| `_message` | `Label` | 메인 메시지 ("D-3 비누 교체 필요") |
| `_detail` | `Label` | 상세 정보 ("일평균 15.2회 사용 기준") |
| `_checkTimer` | `float` | 체크 간격 타이머 |
| `_currentLevel` | `AlarmLevel` | 현재 표시 중인 알람 레벨 |

---

### Line 27-40: Start() — 초기화

```csharp
void Start()
{
    if (uiDocument == null)
    {
        return;
    }

    var root = uiDocument.rootVisualElement;
    _banner  = root.Q<VisualElement>("alarm-banner");
    _message = root.Q<Label>("alarm-message");
    _detail  = root.Q<Label>("alarm-detail");

    UpdateBanner();
}
```

**동작 흐름**:
1. uiDocument null 체크 (Early Return)
2. `Q<T>("name")`: UXML에서 `name` 속성으로 요소 검색
3. 초기 배너 상태 업데이트

**UI 요소 매핑**:
```xml
<ui:VisualElement name="alarm-banner">
    <ui:Label name="alarm-message"/>
    <ui:Label name="alarm-detail"/>
</ui:VisualElement>
```

---

### Line 42-51: Update() — 주기적 체크

```csharp
void Update()
{
    _checkTimer += Time.deltaTime;

    if (_checkTimer >= CheckInterval)
    {
        _checkTimer = 0f;
        UpdateBanner();
    }
}
```

- `Time.deltaTime`: 이전 프레임 이후 경과 시간 (초)
- 10초마다 `UpdateBanner()` 호출
- 매 프레임 호출보다 효율적

---

### Line 53-92: UpdateBanner() — 배너 상태 갱신

```csharp
private void UpdateBanner()
{
    var prediction = analyticsPanel?.GetLastPrediction();

    if (prediction == null || _banner == null)
    {
        return;
    }

    if (prediction.Level == AlarmLevel.None)
    {
        HideBanner();
        return;
    }

    if (prediction.Level != _currentLevel)
    {
        _banner.RemoveFromClassList(ClassWarn);
        _banner.RemoveFromClassList(ClassAlert);

        string newClass = prediction.Level == AlarmLevel.Alert
            ? ClassAlert
            : ClassWarn;
        _banner.AddToClassList(newClass);

        _currentLevel = prediction.Level;
    }

    _banner.RemoveFromClassList(ClassHidden);

    if (_message != null)
    {
        _message.text = prediction.Message;
    }

    if (_detail != null)
    {
        _detail.text = $"일평균 {prediction.DailyUsageAvg:F1}회 사용 기준";
    }
}
```

**동작 흐름**:

1. **예측 결과 가져오기**
   - `analyticsPanel?.GetLastPrediction()`: null-safe 호출

2. **가드 조건**
   - prediction이나 _banner가 null이면 종료

3. **None 레벨 처리**
   - 알람이 필요 없으면 배너 숨김

4. **레벨 변경 감지**
   - 이전 레벨과 다를 때만 CSS 클래스 교체
   - 불필요한 DOM 조작 방지 (성능 최적화)

5. **배너 표시**
   - `alarm-hidden` 클래스 제거
   - 메시지 텍스트 갱신

**CSS 클래스 전환 로직**:
```
None  → alarm-hidden 추가 (숨김)
Warn  → alarm-warn 추가 (노란색)
Alert → alarm-alert 추가 (빨간색)
```

---

### Line 94-98: HideBanner() — 배너 숨김

```csharp
private void HideBanner()
{
    _banner?.AddToClassList(ClassHidden);
    _currentLevel = AlarmLevel.None;
}
```

- `alarm-hidden` CSS 클래스 추가 (display: none 또는 opacity: 0)
- 현재 레벨을 None으로 리셋

---

## 상태 다이어그램

```
                ┌─────────────┐
                │   Hidden    │
                │  (None)     │
                └──────┬──────┘
                       │ prediction.Level != None
                       ▼
        ┌──────────────┴──────────────┐
        │                             │
        ▼                             ▼
┌───────────────┐           ┌───────────────┐
│    Warn       │           │    Alert      │
│  (노란색)     │◄─────────►│  (빨간색)     │
└───────────────┘           └───────────────┘
        │                             │
        │    prediction.Level == None │
        └──────────────┬──────────────┘
                       ▼
                ┌─────────────┐
                │   Hidden    │
                └─────────────┘
```

---

## CSS 클래스 (MainTheme.uss)

```css
.alarm-banner {
    position: absolute;
    top: 10px;
    left: 50%;
    /* 배너 내용 스타일 */
}

.alarm-hidden {
    display: none;  /* 또는 opacity: 0 */
}

.alarm-warn {
    background-color: #FFA726;  /* 주황색 */
    border-color: #FF9800;
}

.alarm-alert {
    background-color: #EF5350;  /* 빨간색 */
    border-color: #F44336;
}
```

---

## 의존성

| 클래스 | 역할 |
|--------|------|
| `AnalyticsPanel` | `GetLastPrediction()` 메서드로 예측 결과 제공 |
| `ReplenishResult` | 예측 결과 데이터 (Level, Message, DailyUsageAvg) |
| `AlarmLevel` | 알람 단계 열거형 (None, Warn, Alert) |
