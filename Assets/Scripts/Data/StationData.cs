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

    [Header("Running State (Y 출력 기반)")]
    public bool isSoapRunning  = false;   // 세정제 실린더 동작 중 (Y0C1 OR Y0C2)
    public bool isWaterRunning = false;   // 물 출수 중 (Y0C0)
    public bool isAirRunning   = false;   // 바람 출력 중 (Y0C3)
    public bool isSoapForward  = false;   // 세정제 전진 중 (Y0C1 = M1 OR M4)
    public bool isSoapBackward = false;   // 세정제 후진 중 (Y0C2 = M2 OR M5)

    [Header("PLC Mode Relays")]
    public bool isManualWaterMode = false;  // M10: 수동 물 모드
    public bool isManualSoapMode  = false;  // M20: 수동 세정제 모드
    public bool isManualAirMode   = false;  // M30: 수동 바람 모드
    public bool isAutoMode        = false;  // M40: 자동 모드 (손 센서)

    [Header("Step Display")]
    [Range(0, 5)]
    public int currentStep = 0;
    // 0=대기  1=준비중(T0)  2=물  3=세정제전진  4=세정제후진  5=바람

    public enum SystemStatus { Normal, Warning, Error }
    public SystemStatus systemStatus = SystemStatus.Normal;

    public event Action OnDataChanged;

    public string GetActiveModeName()
    {
        if (isManualWaterMode)
        {
            return "수동 물";
        }
        if (isManualSoapMode)
        {
            return "수동 세정제";
        }
        if (isManualAirMode)
        {
            return "수동 바람";
        }
        if (isAutoMode)
        {
            return "자동";
        }
        return "대기";
    }

    public string GetCurrentStepName()
    {
        return currentStep switch
        {
            1 => "준비 중",
            2 => "물",
            3 => "세정제 전진",
            4 => "세정제 후진",
            5 => "바람",
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

    public static int ComputeStep(bool anyMode, bool soapFwd, bool soapBwd, bool water, bool air)
    {
        if (!anyMode)
        {
            return 0;
        }

        if (soapFwd)
        {
            return 3;
        }

        if (soapBwd)
        {
            return 4;
        }

        if (water)
        {
            return 2;
        }

        if (air)
        {
            return 5;
        }
        
        return 1;
    }

    public void ResetData()
    {
        soapLevel       = 100f;
        soapUseCount    = 0;
        isSoapRunning   = false;
        isWaterRunning  = false;
        isAirRunning    = false;
        isSoapForward   = false;
        isSoapBackward  = false;
        currentStep     = 0;
        isManualWaterMode = false;
        isManualSoapMode  = false;
        isManualAirMode   = false;
        isAutoMode        = false;
        systemStatus    = SystemStatus.Normal;
        OnDataChanged?.Invoke();
    }
}
