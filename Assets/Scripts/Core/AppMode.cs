using UnityEngine;

/// <summary>
/// PLC_MODE: 모든 동작이 PLC 신호 기반 (기본값)
/// TEST_MODE: 버튼 클릭 시 임의 시간 동안 동작 (PLC 없이 테스트)
/// </summary>
public enum AppMode
{
    PLC_MODE,
    TEST_MODE
}

public static class AppModeManager
{
    public static AppMode Current { get; private set; } = AppMode.PLC_MODE;

    public static void SetMode(AppMode mode)
    {
        Current = mode;
        Debug.Log($"[AppMode] 모드 전환: {mode}");
    }

    public static bool IsTestMode => Current == AppMode.TEST_MODE;
    public static bool IsPLCMode  => Current == AppMode.PLC_MODE;
}
