# AnalyticsPanel.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/UI/AnalyticsPanel.cs`
- **목적**: 우측 상단 비누 사용량 분석 패널 제어

## 핵심 기능

| 기능 | 설명 |
|------|------|
| 시간대별 차트 | BarChartElement로 24시간 사용량 시각화 |
| 피크 시간 표시 | 가장 많이 사용한 시간대 표시 |
| 평균 비교 | 피크 시간 vs 전체 평균 비교 |
| 교체 예측 | ReplenishPredictor 호출하여 예측 결과 저장 |

---

## 코드 분석

### Line 11-21: 상수 정의

```csharp
private const float RefreshInterval = 10f;

private const int   PanelWidth      = 320;
private const int   PanelMinHeight  = 200;
private const int   PanelOffset     = 10;
private const int   BorderRadius    = 12;
private const int   BorderWidth     = 1;
private const int   Padding         = 14;

private static readonly Color BackgroundColor = new Color(0.05f, 0.12f, 0.18f, 1f);
private static readonly Color BorderColor     = new Color(0f, 0.9f, 0.46f, 1f);
```

| 상수 | 값 | 용도 |
|------|-----|------|
| `RefreshInterval` | 10f | 차트 갱신 주기 (초) |
| `PanelWidth` | 320 | 패널 너비 (px) |
| `PanelMinHeight` | 200 | 패널 최소 높이 (px) |
| `PanelOffset` | 10 | 화면 가장자리로부터 거리 (px) |
| `BorderRadius` | 12 | 모서리 둥글기 (px) |
| `BorderWidth` | 1 | 테두리 두께 (px) |
| `Padding` | 14 | 내부 여백 (px) |
| `BackgroundColor` | 어두운 파란색 | 패널 배경색 (RGBA) |
| `BorderColor` | 초록색 | 테두리 색상 (#00E676) |

**`static readonly` vs `const`**:
- `const`: 컴파일 타임 상수 (primitive 타입만)
- `static readonly`: 런타임 상수 (객체 타입 가능)

---

### Line 23-34: 필드 정의

```csharp
[Header("References")]
public UIDocument      uiDocument;
public SoapUsageLogger logger;
public StationData     stationData;

private VisualElement   _panel;
private BarChartElement _barChart;
private Label           _peakTimeLabel;
private Label           _avgCompareLabel;

private ReplenishResult _lastPrediction;
private float           _refreshTimer;
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `uiDocument` | `UIDocument` | UI 트리 루트 (인스펙터 연결) |
| `logger` | `SoapUsageLogger` | 사용량 데이터 제공 |
| `stationData` | `StationData` | 현재 비누 잔량 제공 |
| `_panel` | `VisualElement` | 패널 컨테이너 |
| `_barChart` | `BarChartElement` | 동적 생성된 차트 |
| `_peakTimeLabel` | `Label` | "14:00 - 15:00" 표시 |
| `_avgCompareLabel` | `Label` | "전체 평균 대비 150% 사용량" 표시 |
| `_lastPrediction` | `ReplenishResult` | 마지막 예측 결과 (AlarmBanner에서 사용) |
| `_refreshTimer` | `float` | 갱신 타이머 |

---

### Line 36-67: Start() — 초기화

```csharp
void Start()
{
    if (uiDocument == null) { return; }

    var root = uiDocument.rootVisualElement;

    _panel           = root.Q<VisualElement>("analytics-panel");
    _peakTimeLabel   = root.Q<Label>("peak-time-label");
    _avgCompareLabel = root.Q<Label>("avg-compare-label");

    if (_panel == null) { return; }

    ApplyPanelStyles(_panel);
    _panel.BringToFront();

    var chartContainer = root.Q<VisualElement>("bar-chart-container");
    if (chartContainer != null)
    {
        _barChart = new BarChartElement();
        _barChart.style.flexGrow = 1;
        _barChart.style.height = new StyleLength(Length.Percent(100));
        chartContainer.Add(_barChart);
    }

    RefreshChart();
}
```

**동작 흐름**:
1. UI 요소 캐싱 (`Q<T>("name")`)
2. 인라인 스타일 적용 (`ApplyPanelStyles`)
3. 렌더링 순서 조정 (`BringToFront`)
4. BarChartElement 동적 생성 및 추가
5. 초기 데이터 로드

**BringToFront()가 필요한 이유**:
- UI Toolkit은 DOM 순서로 렌더링
- 절대 위치 요소가 다른 요소에 가려질 수 있음
- `BringToFront()`로 DOM 순서를 맨 뒤로 이동 (= 맨 위에 렌더링)

---

### Line 69-78: Update() — 주기적 갱신

```csharp
void Update()
{
    _refreshTimer += Time.deltaTime;

    if (_refreshTimer >= RefreshInterval)
    {
        _refreshTimer = 0f;
        RefreshChart();
    }
}
```

- 10초마다 차트 데이터 갱신
- 매 프레임 갱신보다 효율적

---

### Line 80-115: RefreshChart() — 차트 및 레이블 갱신

```csharp
private void RefreshChart()
{
    if (logger == null) { return; }

    _barChart?.SetData(logger.HourlyCount, logger.PeakHour);

    if (_peakTimeLabel != null)
    {
        int peak = logger.PeakHour;
        int nextHour = (peak + 1) % 24;

        _peakTimeLabel.text = logger.TodayTotal > 0
            ? $"{peak:D2}:00 - {nextHour:D2}:00"
            : "데이터 없음";
    }

    if (_avgCompareLabel != null)
    {
        double overall = logger.HourlyCount
            .Where(v => v > 0)
            .DefaultIfEmpty(1)
            .Average();

        float peak = logger.PeakCount > 0 ? logger.PeakCount : 1f;
        int pct = Mathf.RoundToInt(peak / (float)overall * 100f);

        _avgCompareLabel.text = logger.TodayTotal > 0
            ? $"전체 평균 대비 {pct}% 사용량"
            : "데이터 수집 중...";
    }

    RefreshPrediction();
}
```

**피크 시간 계산**:
- `logger.PeakHour`: 가장 많이 사용한 시간 (0-23)
- `(peak + 1) % 24`: 다음 시간 (23 → 0 순환)
- `:D2` 포맷: 2자리 숫자 (예: 9 → "09")

**평균 비교 계산**:
```
overall = 사용량이 있는 시간대의 평균
pct = (피크 사용량 / 평균) × 100
```

**LINQ 설명**:
- `.Where(v => v > 0)`: 0이 아닌 값만 필터
- `.DefaultIfEmpty(1)`: 빈 컬렉션이면 1 반환 (0 나누기 방지)
- `.Average()`: 평균 계산

---

### Line 117-132: RefreshPrediction() — 교체 예측

```csharp
private void RefreshPrediction()
{
    if (logger == null || stationData == null) { return; }

    var dailyUsage = logger.GetDailyUsage(logger.Config.prediction.lookbackDays);

    _lastPrediction = ReplenishPredictor.Predict(
        stationData.soapLevel,
        dailyUsage,
        logger.Config.prediction,
        logger.Config.soapDecreasePerUse
    );
}
```

- `GetDailyUsage()`: 최근 N일간 일별 사용량 조회
- `ReplenishPredictor.Predict()`: 교체 예측일 계산
- `_lastPrediction`: AlarmBanner에서 `GetLastPrediction()`으로 접근

---

### Line 134-137: GetLastPrediction() — 예측 결과 제공

```csharp
public ReplenishResult GetLastPrediction()
{
    return _lastPrediction;
}
```

- AlarmBanner가 호출하여 배너 표시 여부 결정
- 10초마다 갱신된 최신 예측 결과 반환

---

### Line 139-168: ApplyPanelStyles() — 인라인 스타일 적용

```csharp
private void ApplyPanelStyles(VisualElement panel)
{
    panel.style.position = Position.Absolute;
    panel.style.top      = PanelOffset;
    panel.style.right    = PanelOffset;
    // ... 나머지 스타일
}
```

**USS 대신 C# 인라인 스타일 사용 이유**:
- USS `@import`가 Unity 6에서 불안정
- BarChartElement가 동적 생성되어 USS 로드 타이밍 문제
- C# 스타일은 확실하게 적용됨

**스타일 속성 그룹**:
1. **위치**: position, top, right
2. **크기**: width, minHeight
3. **배경**: backgroundColor
4. **테두리**: border*Radius, border*Width, border*Color
5. **여백**: padding*

---

## 데이터 흐름

```
SoapUsageLogger
├── HourlyCount (int[24]) ──────► BarChartElement.SetData()
├── PeakHour (int) ─────────────► _peakTimeLabel.text
├── PeakCount (int) ────────────► 평균 비교 계산
├── TodayTotal (int) ───────────► 데이터 유무 판단
└── GetDailyUsage() ────────────► ReplenishPredictor.Predict()
                                         │
                                         ▼
                                  _lastPrediction
                                         │
                                         ▼
                              AlarmBanner.GetLastPrediction()
```

---

## UI 요소 매핑 (UXML)

```xml
<ui:VisualElement name="analytics-panel">
    <ui:Label name="peak-time-label"/>        <!-- 피크 시간 -->
    <ui:VisualElement name="bar-chart-container">
        <!-- BarChartElement 동적 추가 -->
    </ui:VisualElement>
    <ui:Label name="avg-compare-label"/>      <!-- 평균 비교 -->
</ui:VisualElement>
```
