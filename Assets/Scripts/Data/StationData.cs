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

    [Header("Running State")]
    public bool isSoapRunning = false;
    public bool isWaterRunning = false;
    public bool isAirRunning  = false;

    [Header("PLC Mode")]
    public bool isAutoMode   = false;   // M13: 자동 모드 동작 중
    public bool isManualMode = false;   // M10: 수동 모드 동작 중
    public bool isCycleEnd   = false;   // M6: 자동 종료 신호

    public enum SystemStatus
    {
        Normal,
        Warning,
        Error
    }

    public SystemStatus systemStatus = SystemStatus.Normal;

    public event Action OnDataChanged;

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
        systemStatus = SystemStatus.Normal;
        OnDataChanged?.Invoke();
    }
}
