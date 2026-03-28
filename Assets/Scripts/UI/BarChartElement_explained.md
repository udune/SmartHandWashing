# BarChartElement.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/UI/BarChartElement.cs`
- **목적**: UI Toolkit용 커스텀 VisualElement로 24시간 막대 차트를 painter2D로 직접 렌더링

## 핵심 기능

| 기능 | 설명 |
|------|------|
| 커스텀 VisualElement | VisualElement를 상속하여 확장 |
| painter2D 그래픽 | MeshGenerationContext의 painter2D로 벡터 그래픽 드로우 |
| 피크 시간 강조 | 가장 많이 사용한 시간대를 주황색으로 표시 |
| 자동 스케일링 | 최댓값 기준으로 막대 높이 자동 조정 |

---

## 코드 분석

### Line 11-19: 레이아웃 상수

```csharp
private const int   HoursPerDay   = 24;
private const float PaddingLeft   = 8f;
private const float PaddingRight  = 8f;
private const float PaddingBottom = 20f;
private const float PaddingTop    = 8f;
private const float BarGapRatio   = 0.15f;
private const float AxisLineWidth = 0.5f;
private const float MinBarHeight  = 1f;
```

| 상수 | 값 | 용도 |
|------|-----|------|
| `HoursPerDay` | 24 | 하루 시간 수 (막대 개수) |
| `PaddingLeft/Right` | 8f | 좌우 여백 (px) |
| `PaddingBottom` | 20f | 하단 여백 — X축 레이블 공간 |
| `PaddingTop` | 8f | 상단 여백 (px) |
| `BarGapRatio` | 0.15f | 막대 간 간격 비율 (15%) |
| `AxisLineWidth` | 0.5f | X축 선 두께 (px) |
| `MinBarHeight` | 1f | 최소 막대 높이 (px) — 0 데이터도 표시 |

---

### Line 22-25: 색상 상수

```csharp
private static readonly Color BarNormal = new Color(0.26f, 0.60f, 0.26f, 1f);
private static readonly Color BarPeak   = new Color(1.00f, 0.57f, 0.00f, 1f);
private static readonly Color BarAxis   = new Color(0.47f, 0.56f, 0.61f, 0.5f);
private static readonly Color TextColor = new Color(0.47f, 0.56f, 0.61f, 1f);
```

| 색상 | RGB | 용도 |
|------|-----|------|
| `BarNormal` | 초록 (#439943) | 일반 시간대 막대 |
| `BarPeak` | 주황 (#FF9100) | 피크 시간 막대 |
| `BarAxis` | 회색 (50% 투명) | X축 기준선 |
| `TextColor` | 회색 | 텍스트용 (미사용) |

**`static readonly` 사용 이유**:
- `Color`는 struct이지만 `new` 키워드로 생성하므로 `const` 불가
- 런타임에 한 번만 초기화되어 메모리 효율적

---

### Line 28-29: 데이터 필드

```csharp
private int[] _data     = new int[HoursPerDay];
private int   _peakHour = -1;
```

| 필드 | 타입 | 설명 |
|------|------|------|
| `_data` | `int[24]` | 시간대별 사용량 (0-23시) |
| `_peakHour` | `int` | 피크 시간 인덱스 (-1 = 없음) |

---

### Line 31-34: 생성자

```csharp
public BarChartElement()
{
    generateVisualContent += OnGenerateVisualContent;
}
```

**`generateVisualContent` 이벤트**:
- UI Toolkit의 VisualElement가 렌더링될 때 발생
- 이 이벤트에 핸들러를 등록하면 커스텀 드로잉 가능
- `+=` 연산자로 이벤트 구독 (멀티캐스트 델리게이트)

**VisualElement 생명주기**:
1. 생성자 호출
2. 스타일 적용
3. 레이아웃 계산
4. `generateVisualContent` 이벤트 발생 → 커스텀 드로잉
5. 화면에 렌더링

---

### Line 36-41: SetData() — 데이터 업데이트

```csharp
public void SetData(int[] hourlyCount, int peakHour)
{
    _data     = hourlyCount;
    _peakHour = peakHour;
    MarkDirtyRepaint();
}
```

**`MarkDirtyRepaint()`**:
- VisualElement를 "더티" 상태로 표시
- 다음 프레임에 `generateVisualContent` 이벤트가 다시 발생
- 수동으로 호출하지 않으면 차트가 갱신되지 않음

**데이터 흐름**:
```
SoapUsageLogger.HourlyCount → SetData() → MarkDirtyRepaint() → OnGenerateVisualContent()
```

---

### Line 43-97: OnGenerateVisualContent() — 차트 드로잉

#### 1. 컨텍스트 및 크기 가져오기

```csharp
var painter = ctx.painter2D;
float w     = contentRect.width;
float h     = contentRect.height;

if (w <= 0 || h <= 0)
{
    return;
}
```

- `ctx.painter2D`: 벡터 그래픽 API (Canvas 2D와 유사)
- `contentRect`: 패딩을 제외한 실제 콘텐츠 영역
- 크기가 0 이하면 드로잉 스킵 (레이아웃 미완료 상태)

#### 2. 차트 영역 계산

```csharp
float chartW = w - PaddingLeft - PaddingRight;
float chartH = h - PaddingBottom - PaddingTop;

int maxVal = GetMaxValue();

float barW   = chartW / HoursPerDay;
float barGap = barW * BarGapRatio;
float bottom = h - PaddingBottom;
```

**레이아웃 계산**:
```
┌─────────────────────────────────────┐
│           PaddingTop (8px)          │
├─────────────────────────────────────┤
│     │                         │     │
│ Pad │       chartH            │ Pad │
│Left │  (막대 영역)            │Right│
│(8px)│                         │(8px)│
│     │                         │     │
├─────────────────────────────────────┤
│         PaddingBottom (20px)        │
│        [X축 레이블 공간]            │
└─────────────────────────────────────┘
```

#### 3. X축 기준선 드로잉

```csharp
painter.strokeColor = BarAxis;
painter.lineWidth   = AxisLineWidth;
painter.BeginPath();
painter.MoveTo(new Vector2(PaddingLeft, bottom));
painter.LineTo(new Vector2(w - PaddingRight, bottom));
painter.Stroke();
```

**painter2D API**:
- `BeginPath()`: 새 경로 시작
- `MoveTo()`: 펜 이동 (그리지 않음)
- `LineTo()`: 현재 위치에서 선 그림
- `Stroke()`: 경로를 선으로 그림
- `strokeColor/lineWidth`: 선 스타일 설정

#### 4. 막대 드로잉 (24시간 루프)

```csharp
for (int hour = 0; hour < HoursPerDay; hour++)
{
    float barHeight = _data[hour] > 0
        ? (float)_data[hour] / maxVal * chartH
        : MinBarHeight;

    float x = PaddingLeft + hour * barW + barGap * 0.5f;
    float y = bottom - barHeight;

    painter.fillColor = (hour == _peakHour && _data[hour] > 0)
        ? BarPeak
        : BarNormal;

    painter.BeginPath();
    painter.MoveTo(new Vector2(x,           bottom));
    painter.LineTo(new Vector2(x,           y));
    painter.LineTo(new Vector2(x + barW - barGap, y));
    painter.LineTo(new Vector2(x + barW - barGap, bottom));
    painter.ClosePath();
    painter.Fill();
}
```

**막대 높이 계산**:
```
barHeight = (현재값 / 최댓값) × 차트 높이
```
- 비례 스케일링으로 최댓값이 차트 상단에 도달

**막대 위치 계산**:
```
x = 좌측 패딩 + (시간 × 막대 너비) + (간격 / 2)
```
- 간격의 절반을 왼쪽에 추가하여 중앙 정렬 효과

**막대 사각형 그리기**:
```
   (x, y) ─────────── (x+barW-gap, y)
      │                     │
      │     막대 영역       │
      │                     │
(x, bottom) ───────── (x+barW-gap, bottom)
```
- 시계방향으로 4개 점 연결
- `ClosePath()`로 경로 닫기
- `Fill()`로 내부 채우기

---

### Line 99-112: GetMaxValue() — 최댓값 계산

```csharp
private int GetMaxValue()
{
    int maxVal = 1;

    foreach (var v in _data)
    {
        if (v > maxVal)
        {
            maxVal = v;
        }
    }

    return maxVal;
}
```

- 최솟값 1로 시작 (0 나누기 방지)
- O(n) 선형 탐색으로 최댓값 찾기
- LINQ `Max()` 대신 수동 루프 사용 (GC 부담 감소)

---

## 아키텍처 다이어그램

```
┌──────────────────────────────────────────────────────────────┐
│                    BarChartElement                           │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────────┐│
│  │   SetData()   │  │GetMaxValue()  │  │OnGenerateVisual   ││
│  │ (외부 API)    │→│ (정규화)       │→│Content() (렌더링) ││
│  └───────────────┘  └───────────────┘  └───────────────────┘│
└──────────────────────────────────────────────────────────────┘
                         ▲
                         │ _data, _peakHour
                         │
┌──────────────────────────────────────────────────────────────┐
│                   AnalyticsPanel                             │
│            _barChart.SetData(HourlyCount, PeakHour)          │
└──────────────────────────────────────────────────────────────┘
                         ▲
                         │ HourlyCount[24]
                         │
┌──────────────────────────────────────────────────────────────┐
│                   SoapUsageLogger                            │
│                 LogSoapEvent() → HourlyCount++               │
└──────────────────────────────────────────────────────────────┘
```

---

## painter2D vs IMGUI 비교

| 특성 | painter2D (UI Toolkit) | OnGUI (IMGUI) |
|------|------------------------|---------------|
| 렌더링 방식 | 메시 기반 (GPU) | 즉시 모드 (CPU) |
| 성능 | 배칭으로 효율적 | 매 프레임 재생성 |
| 스타일링 | USS 지원 | 제한적 |
| 사용 사례 | 런타임 UI | 에디터 확장 |

---

## UXML 호환성 문제 (주석 참조)

```csharp
// C#에서 동적으로 생성하여 사용 (UXML 호환성 문제 회피)
```

**UXML에서 커스텀 요소 사용 시 문제**:
1. `UxmlFactory` 정의 필요
2. USS 로드 타이밍 문제
3. 동적 데이터 바인딩 어려움

**C# 동적 생성 장점**:
- 명시적 생성자 호출
- 참조 직접 관리
- 데이터 바인딩 간편

---

## 좌표계 참고

```
(0,0) ────────────────────► X
  │
  │
  │
  ▼
  Y

- 원점: 좌상단
- Y축: 아래로 증가
- 막대 그릴 때 y = bottom - barHeight (위로 성장)
```

---

## 관련 파일

| 파일 | 역할 |
|------|------|
| `AnalyticsPanel.cs` | BarChartElement 생성 및 데이터 연결 |
| `SoapUsageLogger.cs` | HourlyCount 데이터 제공 |
| `MainHMI.uxml` | bar-chart-container 요소 정의 |
