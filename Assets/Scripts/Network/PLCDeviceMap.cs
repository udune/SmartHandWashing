[System.Serializable]
public class PLCDeviceMap
{
    // 입력 (HMI → PLC 쓰기)
    public string handSensor       = "X0A0";   // 손 센서
    public string soapBtnManual    = "X0A3";   // 비누 PB (수동)
    public string waterBtnManual   = "X0A4";   // 물 PB (수동)
    public string airBtnManual     = "X0A5";   // 건조 PB (수동)

    // 상태 읽기 — 자동 모드
    public string soapRunningAuto  = "M0";     // 비누 실린더 동작
    public string waterRunningAuto = "M4";     // 물 모터 동작
    public string airRunningAuto   = "M5";     // 건조 모터 동작

    // 상태 읽기 — 수동 모드
    public string soapRunningManual  = "M7";   // 비누 (수동)
    public string waterRunningManual = "M11";  // 물 (수동)
    public string airRunningManual   = "M12";  // 건조 (수동)

    // 모드 및 종료
    public string autoMode   = "M13";          // 자동 모드
    public string manualMode = "M10";          // 수동 모드
    public string cycleEnd   = "M6";           // 자동 종료
}

[System.Serializable]
public class PLCConfig
{
    public bool   useMock            = true;
    public string ip                 = "192.168.3.39";
    public int    port               = 5007;
    public int    pollIntervalMs     = 100;
    public int    timeoutMs          = 2000;
    public float  soapDecreasePerUse = 5f;
    public PLCDeviceMap devices      = new PLCDeviceMap();
}
