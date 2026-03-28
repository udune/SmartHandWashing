# ReplenishPredictor.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Analytics/ReplenishPredictor.cs`
- **목적**: 비누 잔량과 사용 패턴을 기반으로 교체 예측일 계산

## 구성 요소

이 파일은 3개의 타입을 정의합니다:
1. `AlarmLevel` — 알람 단계 열거형
2. `ReplenishResult` — 예측 결과 데이터 클래스
3. `ReplenishPredictor` — 예측 알고리즘 정적 클래스

---

## 코드 분석

### Line 6: AlarmLevel 열거형

```csharp
public enum AlarmLevel { None, Warn, Alert }
```

| 값 | 의미 | 트리거 조건 |
|----|------|-------------|
| `None` | 정상 | 잔여일 > warnDays |
| `Warn` | 경고 | alertDays < 잔여일 ≤ warnDays |
| `Alert` | 긴급 | 잔여일 ≤ alertDays |

---

### Line 8-15: ReplenishResult 클래스

```csharp
public class ReplenishResult
{
    public float      DaysRemaining;    // 예측 잔여일
    public DateTime   PredictedDate;    // 예측 교체일
    public AlarmLevel Level;            // 알람 단계
    public float      DailyUsageAvg;    // 일평균 사용 횟수
    public string     Message;          // UI 표시 메시지
}
```

**목적**: `Predict()` 메서드의 반환값을 담는 데이터 컨테이너

| 필드 | 타입 | 설명 |
|------|------|------|
| `DaysRemaining` | `float` | 비누가 소진될 때까지 남은 일수 |
| `PredictedDate` | `DateTime` | 예상 교체 날짜 (`DateTime.Now + DaysRemaining`) |
| `Level` | `AlarmLevel` | 알람 심각도 (None/Warn/Alert) |
| `DailyUsageAvg` | `float` | 계산에 사용된 일평균 사용 횟수 |
| `Message` | `string` | UI에 표시할 한국어 메시지 |

---

### Line 22-24: 클래스 선언 및 상수

```csharp
public static class ReplenishPredictor
{
    private const float DefaultDailyUsage = 10f;
```

- `static class`: 인스턴스 생성 불가, 모든 멤버가 정적
- `DefaultDailyUsage`: 사용 데이터가 없을 때 기본 일평균 (10회/일)

---

### Line 26-30: Predict() 메서드 시그니처

```csharp
public static ReplenishResult Predict(
    float currentSoapPct,
    Dictionary<string, int> dailyUsage,
    PredictionConfig config,
    float soapDecreasePerUse)
```

| 매개변수 | 타입 | 설명 |
|----------|------|------|
| `currentSoapPct` | `float` | 현재 비누 잔량 (0~100%) |
| `dailyUsage` | `Dictionary<string, int>` | 날짜별 사용 횟수 (예: `{"2024-01-01": 15}`) |
| `config` | `PredictionConfig` | 알람 임계값 설정 (alertDays, warnDays) |
| `soapDecreasePerUse` | `float` | 1회 사용당 감소량 (%) |

---

### Line 32-38: 일평균 사용 횟수 계산

```csharp
var result = new ReplenishResult();

float avgDaily = dailyUsage.Count > 0
    ? (float)dailyUsage.Values.Average()
    : DefaultDailyUsage;

result.DailyUsageAvg = avgDaily;
```

**동작 흐름**:
1. 빈 결과 객체 생성
2. `dailyUsage`에 데이터가 있으면 `.Values.Average()`로 평균 계산
3. 데이터가 없으면 `DefaultDailyUsage` (10회) 사용
4. 결과에 저장

**LINQ 메서드**:
- `.Values` — Dictionary의 값(int) 컬렉션
- `.Average()` — 평균 계산 (double 반환 → float 캐스팅)

---

### Line 40-48: 일평균 소비량 계산 및 가드 조건

```csharp
float dailyConsumePct = avgDaily * soapDecreasePerUse;

if (dailyConsumePct <= 0f)
{
    result.DaysRemaining = float.MaxValue;
    result.Level         = AlarmLevel.None;
    result.Message       = "데이터 수집 중...";
    return result;
}
```

**수식**: `일평균 소비량(%) = 일평균 사용 횟수 × 1회 감소량`

**예시**: 일평균 20회 × 0.5% = 10%/일 소비

**가드 조건**: 소비량이 0 이하면 예측 불가능
- `float.MaxValue` — 무한대에 가까운 값 (교체 필요 없음)
- "데이터 수집 중..." 메시지 표시

---

### Line 50-51: 잔여일 및 예측일 계산

```csharp
result.DaysRemaining = currentSoapPct / dailyConsumePct;
result.PredictedDate = DateTime.Now.AddDays(result.DaysRemaining);
```

**수식**: `잔여일 = 현재 잔량(%) / 일평균 소비량(%)`

**예시**: 50% / 10%/일 = 5일

**예측일**: 현재 시간에 잔여일을 더함

---

### Line 53-69: 알람 레벨 결정

```csharp
if (result.DaysRemaining <= config.alertDays)
{
    result.Level   = AlarmLevel.Alert;
    result.Message = result.DaysRemaining < 1f
        ? "오늘 비누 교체 필요!"
        : $"D-{Mathf.CeilToInt(result.DaysRemaining)} 비누 교체 필요";
}
else if (result.DaysRemaining <= config.warnDays)
{
    result.Level   = AlarmLevel.Warn;
    result.Message = $"약 {Mathf.CeilToInt(result.DaysRemaining)}일 후 교체 예정 ({result.PredictedDate:MM/dd})";
}
else
{
    result.Level   = AlarmLevel.None;
    result.Message = $"교체 예정: {result.PredictedDate:MM/dd} (약 {Mathf.CeilToInt(result.DaysRemaining)}일 후)";
}
```

**알람 결정 로직**:

| 조건 | 레벨 | 메시지 예시 |
|------|------|-------------|
| 잔여일 ≤ alertDays (예: 3일) | `Alert` | "D-2 비누 교체 필요" |
| 잔여일 ≤ warnDays (예: 7일) | `Warn` | "약 5일 후 교체 예정 (01/15)" |
| 그 외 | `None` | "교체 예정: 01/20 (약 10일 후)" |

**Unity API**:
- `Mathf.CeilToInt()` — 올림 후 정수 변환 (1.2일 → 2일)

**날짜 포맷**:
- `{PredictedDate:MM/dd}` — "01/15" 형식

---

## 예측 알고리즘 요약

```
입력:
  - currentSoapPct: 현재 비누 잔량 (%)
  - dailyUsage: 최근 N일 사용 기록
  - soapDecreasePerUse: 1회당 감소량 (%)

계산:
  1. avgDaily = 일평균 사용 횟수 (또는 기본값 10)
  2. dailyConsumePct = avgDaily × soapDecreasePerUse
  3. DaysRemaining = currentSoapPct / dailyConsumePct
  4. PredictedDate = 오늘 + DaysRemaining

출력:
  - DaysRemaining, PredictedDate, Level, Message
```

---

## 의존성

| 타입 | 역할 |
|------|------|
| `PredictionConfig` | `alertDays`, `warnDays` 설정값 제공 |
| `AnalyticsConfig` | 상위 설정에서 `prediction` 속성으로 포함 |
