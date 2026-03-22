// [System.Serializable]: JsonUtility 및 Unity Inspector 직렬화 가능
// PLCDeviceMap: PLC 메모리 주소를 문자열로 매핑 (D=워드, M=비트)
[System.Serializable]
public class PLCDeviceMap
{
    // D 디바이스: 워드(16비트) 레지스터 - 수치 데이터 저장
    public string soapLevel  = "D0";   // 비누 잔량 (0~1000)
    // M 디바이스: 비트 릴레이 - ON/OFF 상태 저장
    public string soapBtn    = "M0";   // 비누 버튼 상태
    public string waterBtn   = "M1";   // 물 버튼 상태
    public string airBtn     = "M2";   // 에어 버튼 상태
    public string soapAlarm  = "M10";  // 비누 부족 알람
    public string waterAlarm = "M11";  // 물 공급 알람
    public string usageCount = "D10";  // 누적 사용 횟수
}

// PLCConfig: PLC 통신 전체 설정 (JSON에서 로드)
// Resources/PLCConfig.json에서 JsonUtility.FromJson<PLCConfig>()로 역직렬화
[System.Serializable]
public class PLCConfig
{
    // useMock=true: MockPLCClient 사용 (개발/테스트용)
    // useMock=false: SLMPClient로 실제 PLC 연결
    public bool   useMock        = true;
    public string ip             = "192.168.1.10";   // PLC IP 주소
    public int    port           = 5007;             // SLMP 포트 (미쓰비시 기본: 5000~5007)
    public int    pollIntervalMs = 100;              // 폴링 주기 (밀리초)
    public int    timeoutMs      = 2000;             // 연결 타임아웃 (밀리초)
    // devices: 중첩 객체 - JSON에서 자동으로 역직렬화됨
    public PLCDeviceMap devices  = new PLCDeviceMap();
}
