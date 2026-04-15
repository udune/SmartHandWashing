[System.Serializable]
public class PLCDeviceMap
{
    // ── 입력 센서 (X) — PLC 읽기 ──────────────────────────────────────
    public string bwdSensor  = "X0A0";   // 세정제 후진 완료 센서 (초기 위치)
    public string fwdSensor  = "X0A1";   // 세정제 전진 완료 센서
    public string handSensor = "X0A6";   // 손 인식 센서 (자동 모드 트리거)
    public string waterBtn   = "X0A7";   // 물 스위치 (수동 물 모드)
    public string soapBtn    = "X0A8";   // 세정제 스위치 (수동 세정제 모드)
    public string airBtn     = "X0A9";   // 바람 스위치 (수동 바람 모드)

    // ── 모드 선택 릴레이 (M) — 읽기 ──────────────────────────────────
    public string manualWaterMode = "M10";   // 수동 물 모드 (SET/RST)
    public string manualSoapMode  = "M20";   // 수동 세정제 모드 (SET/RST)
    public string manualAirMode   = "M30";   // 수동 바람 모드 (SET/RST)
    public string autoMode        = "M40";   // 자동 모드 (SET/RST)

    // ── 동작 릴레이 (M0~M7) — 읽기 ───────────────────────────────────
    public string manualWater   = "M0";   // 수동 물 출수 릴레이
    public string manualSoapFwd = "M1";   // 수동 세정제 전진 릴레이
    public string manualSoapBwd = "M2";   // 수동 세정제 후진 릴레이
    public string manualAir     = "M3";   // 수동 바람 릴레이
    public string autoSoapFwd   = "M4";   // 자동 세정제 전진 릴레이
    public string autoSoapBwd   = "M5";   // 자동 세정제 후진 릴레이
    public string autoWater     = "M6";   // 자동 물 릴레이
    public string autoAir       = "M7";   // 자동 바람 릴레이

    // ── 카운터 (C) — 읽기 ─────────────────────────────────────────────
    public string soapCounter = "C0";     // 자동 세정제 왕복 카운터 (K2)

    // ── 출력 (Y) — 읽기 ───────────────────────────────────────────────
    public string waterOutput   = "Y0C0";  // 물 작동 (M0 OR M6)
    public string soapFwdOutput = "Y0C1";  // 세정제 전진 (M1 OR M4)
    public string soapBwdOutput = "Y0C2";  // 세정제 후진 (M2 OR M5)
    public string airOutput     = "Y0C3";  // 바람 작동 (M3 OR M7)
}

[System.Serializable]
public class PLCConfig
{
    public bool   useMock          = true;
    public string ip               = "192.168.3.39";
    public int    port             = 5007;
    public int    pollIntervalMs   = 100;
    public int    timeoutMs        = 2000;
    public float  soapDecreasePerUse = 5f;
    public PLCDeviceMap devices    = new PLCDeviceMap();
}
