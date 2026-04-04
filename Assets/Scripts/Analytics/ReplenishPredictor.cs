using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum AlarmLevel { None, Warn, Alert }

public class ReplenishResult
{
    public float      DaysRemaining;
    public DateTime   PredictedDate;
    public AlarmLevel Level;
    public float      DailyUsageAvg;
    public string     Message;
}

/// <summary>
/// 최근 N일 이동 평균으로 비누 교체 예측일을 계산.
/// 알고리즘: 일평균 소비량(%) = 평균사용횟수 × 1회소비량
///           잔여일 = 현재잔량 / 일평균소비량
/// </summary>
public static class ReplenishPredictor
{
    private const float DefaultDailyUsage = 10f;

    public static ReplenishResult Predict(
        float currentSoapPct,
        Dictionary<string, int> dailyUsage,
        PredictionConfig config,
        float soapDecreasePerUse)
    {
        var result = new ReplenishResult();

        float avgDaily = dailyUsage.Count > 0
            ? (float)dailyUsage.Values.Average()
            : DefaultDailyUsage;

        result.DailyUsageAvg = avgDaily;

        float dailyConsumePct = avgDaily * soapDecreasePerUse;

        if (dailyConsumePct <= 0f)
        {
            result.DaysRemaining = float.MaxValue;
            result.Level         = AlarmLevel.None;
            result.Message       = "데이터 수집 중...";
            return result;
        }

        result.DaysRemaining = currentSoapPct / dailyConsumePct;
        result.PredictedDate = DateTime.Now.AddDays(result.DaysRemaining);

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

        return result;
    }
}
