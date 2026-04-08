[System.Serializable]
public class PLCDeviceMap
{
    // ── 입력 센서 (PLC → HMI 읽기) ──────────────────────────────────────
    public string handSensor = "X0A0";      // 손 센서
    public string fwdSensor = "X0A1";       // 전진단 센서

    // ── 모드 선택 버튼 (HMI → PLC 쓰기) ─────────────────────────────────
    public string soapSelectBtn = "X0A7";   // 비누 모드 선택
    public string waterSelectBtn = "X0A8";  // 물 모드 선택
    public string manualSelectBtn = "X0A9"; // 수동 모드 선택
    public string drySelectBtn = "X0A6";    // 건조 모드 선택

    // ── 모드 상태 (PLC → HMI 읽기) ──────────────────────────────────────
    public string soapMode = "M10";         // 비누 모드 활성
    public string waterMode = "M20";        // 물 모드 활성
    public string manualMode = "M30";       // 수동 모드 활성
    public string dryMode = "M40";          // 건조 모드 활성

    // ── 동작 상태 (8단계 시퀀스) ────────────────────────────────────────
    public string soapRunning = "M0";       // 1단계: 비누 동작
    public string waterWait = "M1";         // 2단계: 물 대기
    public string waterRunning = "M2";      // 3단계: 물 동작
    public string dryRunning = "M3";        // 4단계: 건조 동작
    public string rinseWait = "M4";         // 5단계: 헹굼 대기
    public string rinseRunning = "M5";      // 6단계: 헹굼 동작
    public string soap2Running = "M6";      // 7단계: 비누2 동작
    public string dry2Running = "M7";       // 8단계: 건조2 동작

    // ── 카운터 ─────────────────────────────────────────────────────────
    public string rinseCounter = "C0";      // 헹굼 횟수 카운터

    // ── 출력 (PLC → HMI 읽기) ───────────────────────────────────────────
    public string soapOutput = "Y0C0";      // 비누 출력 (M0 OR M6)
    public string waterWaitOutput = "Y0C1"; // 물 대기 출력 (M1 OR M4)
    public string waterOutput = "Y0C2";     // 물 동작 출력 (M2 OR M5)
    public string dryOutput = "Y0C3";       // 건조 출력 (M3 OR M7)
}

[System.Serializable]
public class PLCConfig
{
    public bool useMock = true;
    public string ip = "192.168.3.39";
    public int port = 5007;
    public int pollIntervalMs = 100;
    public int timeoutMs = 2000;
    public float soapDecreasePerUse = 5f;
    public PLCDeviceMap devices = new PLCDeviceMap();
}
