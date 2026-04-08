using UnityEngine;
using System;

[CreateAssetMenu(menuName = "SmartWash/Station Data")]
public class StationData : ScriptableObject
{
    [Header("Soap")]
    [Range(0f, 100f)]
    public float soapLevel = 100f;
    public int soapUseCount = 0;
    public float soapDecreasePerUse = 5f;

    [Header("Analytics")]
    public int[] hourlyUsageCount = new int[24];

    [Header("Running State (8단계 시퀀스)")]
    public bool isSoapRunning = false;   // 비누 동작 중 (M0 OR M6)
    public bool isWaterRunning = false;  // 물 동작 중 (M1 OR M2 OR M4 OR M5)
    public bool isAirRunning = false;    // 건조 동작 중 (M3 OR M7)
    [Range(0, 8)]
    public int currentStep = 0;          // 현재 시퀀스 단계 (0=대기, 1~8=동작 중)

    [Header("PLC Mode (4개 모드 선택)")]
    public bool isSoapMode = false;      // M10: 비누 모드 선택
    public bool isWaterMode = false;     // M20: 물 모드 선택
    public bool isManualMode = false;    // M30: 수동 모드 선택
    public bool isDryMode = false;       // M40: 건조 모드 선택

    public enum SystemStatus
    {
        Normal,
        Warning,
        Error
    }

    public SystemStatus systemStatus = SystemStatus.Normal;

    public event Action OnDataChanged;

    /// <summary>현재 활성화된 모드 이름 반환</summary>
    public string GetActiveModeName()
    {
        if (isSoapMode) return "비누";
        if (isWaterMode) return "물";
        if (isManualMode) return "수동";
        if (isDryMode) return "건조";
        return "대기";
    }

    /// <summary>현재 시퀀스 단계 이름 반환</summary>
    public string GetCurrentStepName()
    {
        return currentStep switch
        {
            1 => "비누",
            2 => "물 대기",
            3 => "물",
            4 => "건조",
            5 => "헹굼 대기",
            6 => "헹굼",
            7 => "비누2",
            8 => "건조2",
            _ => "대기"
        };
    }

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

    public void ResetData()
    {
        soapLevel = 100f;
        soapUseCount = 0;
        isSoapRunning = false;
        isWaterRunning = false;
        isAirRunning = false;
        currentStep = 0;
        isSoapMode = false;
        isWaterMode = false;
        isManualMode = false;
        isDryMode = false;
        systemStatus = SystemStatus.Normal;
        OnDataChanged?.Invoke();
    }
}
