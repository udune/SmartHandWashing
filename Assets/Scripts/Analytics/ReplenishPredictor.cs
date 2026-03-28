// System: DateTime 날짜/시간 처리
using System;
// System.Collections.Generic: Dictionary 컬렉션
using System.Collections.Generic;
// System.Linq: Average() 등 LINQ 확장 메서드
using System.Linq;
using UnityEngine;

// AlarmLevel: 비누 교체 알람의 심각도 단계
// None=정상, Warn=경고(노란색), Alert=긴급(빨간색)
public enum AlarmLevel { None, Warn, Alert }

// ReplenishResult: Predict() 메서드의 반환값을 담는 데이터 클래스
public class ReplenishResult
{
    // DaysRemaining: 비누가 소진될 때까지 남은 예상 일수
    public float      DaysRemaining;
    // PredictedDate: 비누 교체가 필요한 예상 날짜
    public DateTime   PredictedDate;
    // Level: 알람 심각도 (None/Warn/Alert)
    public AlarmLevel Level;
    // DailyUsageAvg: 계산에 사용된 일평균 사용 횟수
    public float      DailyUsageAvg;
    // Message: UI에 표시할 한국어 알림 메시지
    public string     Message;
}

/// <summary>
/// 최근 N일 이동 평균으로 비누 교체 예측일을 계산.
/// 알고리즘: 일평균 소비량(%) = 평균사용횟수 × 1회소비량
///           잔여일 = 현재잔량 / 일평균소비량
/// </summary>
// static class: 인스턴스 생성 불가, 모든 멤버가 정적
public static class ReplenishPredictor
{
    // DefaultDailyUsage: 사용 데이터가 없을 때 가정하는 일평균 사용 횟수
    private const float DefaultDailyUsage = 10f;

    // Predict(): 비누 잔량과 사용 패턴으로 교체 예측일 계산
    // currentSoapPct: 현재 비누 잔량 (0~100%)
    // dailyUsage: 날짜별 사용 횟수 딕셔너리 (예: {"2024-01-01": 15})
    // config: alertDays, warnDays 알람 임계값 설정
    // soapDecreasePerUse: 비누 1회 사용당 감소량 (%)
    public static ReplenishResult Predict(
        float currentSoapPct,
        Dictionary<string, int> dailyUsage,
        PredictionConfig config,
        float soapDecreasePerUse)
    {
        var result = new ReplenishResult();

        // 삼항 연산자: 데이터가 있으면 평균 계산, 없으면 기본값 사용
        // .Values: Dictionary의 값(int) 컬렉션 반환
        // .Average(): LINQ - 평균값 계산 (double 반환)
        float avgDaily = dailyUsage.Count > 0
            ? (float)dailyUsage.Values.Average()
            : DefaultDailyUsage;

        result.DailyUsageAvg = avgDaily;

        // 일평균 소비량(%) = 일평균 사용 횟수 × 1회당 감소량
        // 예: 20회/일 × 0.5% = 10%/일
        float dailyConsumePct = avgDaily * soapDecreasePerUse;

        // 가드 조건: 소비량이 0 이하면 예측 불가
        if (dailyConsumePct <= 0f)
        {
            // float.MaxValue: 실질적으로 무한대 (교체 불필요)
            result.DaysRemaining = float.MaxValue;
            result.Level         = AlarmLevel.None;
            result.Message       = "데이터 수집 중...";
            return result;
        }

        // 잔여일 = 현재 잔량(%) / 일평균 소비량(%)
        // 예: 50% / 10%/일 = 5일
        result.DaysRemaining = currentSoapPct / dailyConsumePct;
        // DateTime.AddDays(): 현재 시간에 일수를 더해 예측일 계산
        result.PredictedDate = DateTime.Now.AddDays(result.DaysRemaining);

        // 알람 레벨 결정: alertDays < warnDays 순서로 체크
        // config.alertDays: 긴급 알람 임계값 (예: 3일)
        if (result.DaysRemaining <= config.alertDays)
        {
            result.Level   = AlarmLevel.Alert;
            // 1일 미만이면 "오늘", 그 외에는 "D-N" 형식
            // Mathf.CeilToInt(): 올림 후 정수 변환 (1.2 → 2)
            result.Message = result.DaysRemaining < 1f
                ? "오늘 비누 교체 필요!"
                : $"D-{Mathf.CeilToInt(result.DaysRemaining)} 비누 교체 필요";
        }
        // config.warnDays: 경고 알람 임계값 (예: 7일)
        else if (result.DaysRemaining <= config.warnDays)
        {
            result.Level   = AlarmLevel.Warn;
            // {PredictedDate:MM/dd}: 날짜를 "01/15" 형식으로 포맷
            result.Message = $"약 {Mathf.CeilToInt(result.DaysRemaining)}일 후 교체 예정 ({result.PredictedDate:MM/dd})";
        }
        // 정상 상태: 알람 없음
        else
        {
            result.Level   = AlarmLevel.None;
            result.Message = $"교체 예정: {result.PredictedDate:MM/dd} (약 {Mathf.CeilToInt(result.DaysRemaining)}일 후)";
        }

        return result;
    }
}
