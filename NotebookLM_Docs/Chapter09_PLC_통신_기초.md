# Chapter 09: PLC 통신 기초 - SLMP 프로토콜

## PLC란 무엇인가요?

**PLC (Programmable Logic Controller)**는 공장 자동화의 "두뇌"예요.

집에 있는 스마트홈 허브를 생각해 보세요:
- 온도 센서 연결
- 에어컨 제어
- 조명 자동화

PLC는 이런 걸 **공장 규모**로 하는 장치예요!

```
┌──────────────────────────────────────────────────────────┐
│                    공장의 PLC 예시                        │
├──────────────────────────────────────────────────────────┤
│                                                          │
│   입력 (센서)           PLC          출력 (액추에이터)    │
│   ──────────         ─────────      ────────────         │
│   온도 센서 ───────▶ │         │ ───▶ 히터 ON/OFF        │
│   압력 센서 ───────▶ │ 미쓰비시 │ ───▶ 밸브 개폐          │
│   버튼 입력 ───────▶ │  iQ-R   │ ───▶ 모터 회전          │
│   레벨 센서 ───────▶ │         │ ───▶ 알람 출력          │
│                      ─────────                           │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

---

## SLMP 프로토콜이란?

**SLMP (Seamless Message Protocol)**는 미쓰비시 PLC의 통신 규약이에요.

편지 보내는 것과 비슷합니다:

```
실제 편지:
─────────────────────────────────────────
봉투: 주소, 발신인
내용물: 하고 싶은 말
반송: 답장

SLMP 통신:
─────────────────────────────────────────
헤더: 네트워크 정보, 명령 종류
데이터: 읽을/쓸 값
응답: 결과 또는 오류 코드
```

---

## D 디바이스와 M 디바이스

PLC에는 여러 종류의 "저장 공간"이 있어요.

```
┌──────────────────────────────────────────────────────────┐
│                    PLC 디바이스 종류                      │
├──────────────────────────────────────────────────────────┤
│                                                          │
│   D 디바이스 (Data Register) - 숫자 저장용               │
│   ├── D0  = 1000  → 비누 잔량 (0~1000 = 0~100%)          │
│   ├── D1  = 256   → 다른 수치                            │
│   └── D10 = 42    → 누적 사용 횟수                       │
│                                                          │
│   M 디바이스 (Internal Relay) - ON/OFF 저장용            │
│   ├── M0  = ON    → 비누 버튼 눌림                       │
│   ├── M1  = OFF   → 물 버튼 안 눌림                      │
│   ├── M2  = OFF   → 에어 버튼 안 눌림                    │
│   └── M10 = ON    → 비누 부족 알람!                      │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

### 왜 D와 M을 구분하나요?

```
D (Data Register):
├── 16비트 정수 저장 (0~65535 또는 -32768~32767)
├── 수치 데이터에 적합 (온도, 압력, 잔량...)
└── "워드(Word)" 단위로 읽고 씀

M (Internal Relay):
├── 1비트 저장 (true/false)
├── 상태 플래그에 적합 (버튼, 알람, 센서...)
└── "비트(Bit)" 단위로 읽고 씀
```

---

## PLCDeviceMap 클래스

```csharp
[System.Serializable]
public class PLCDeviceMap
{
    public string soapLevel  = "D0";    // 비누 잔량 (워드)
    public string soapBtn    = "M0";    // 비누 버튼 (비트)
    public string waterBtn   = "M1";    // 물 버튼 (비트)
    public string airBtn     = "M2";    // 에어 버튼 (비트)
    public string soapAlarm  = "M10";   // 비누 부족 알람 (비트)
    public string waterAlarm = "M11";   // 물 관련 알람 (비트)
    public string usageCount = "D10";   // 사용 횟수 (워드)
}
```

### 매핑의 의미

```
Unity 측                    PLC 측
─────────────────────────────────────────
soapLevel ←──────────────▶ D0 (0~1000)
soapBtn   ←──────────────▶ M0 (ON/OFF)
waterBtn  ←──────────────▶ M1 (ON/OFF)
airBtn    ←──────────────▶ M2 (ON/OFF)
```

---

## PLCConfig 설정

```csharp
[System.Serializable]
public class PLCConfig
{
    public bool   useMock        = true;          // Mock 모드?
    public string ip             = "192.168.1.10"; // PLC IP
    public int    port           = 5007;           // SLMP 포트
    public int    pollIntervalMs = 100;            // 폴링 주기 (ms)
    public int    timeoutMs      = 2000;           // 연결 타임아웃
    public PLCDeviceMap devices  = new PLCDeviceMap();
}
```

### JSON 설정 파일

```json
// Resources/PLCConfig.json
{
    "useMock": true,
    "ip": "192.168.1.10",
    "port": 5007,
    "pollIntervalMs": 100,
    "timeoutMs": 2000
}
```

| 설정 | 설명 |
|------|------|
| useMock | true면 가상 PLC, false면 실제 연결 |
| ip | PLC의 IP 주소 |
| port | SLMP 포트 (기본 5007) |
| pollIntervalMs | 데이터 읽기 주기 (100ms = 초당 10회) |
| timeoutMs | 연결 타임아웃 (2초) |

---

## IPLCClient 인터페이스

```csharp
public interface IPLCClient
{
    bool IsConnected { get; }

    // 워드 읽기/쓰기 (D 디바이스용)
    Task<int[]> ReadWordsAsync(string device, int count);
    Task WriteWordsAsync(string device, int[] values);

    // 비트 읽기/쓰기 (M 디바이스용)
    Task<bool[]> ReadBitsAsync(string device, int count);
    Task WriteBitsAsync(string device, bool[] values);

    // 연결 관리
    Task ConnectAsync(string ip, int port, int timeoutMs);
    void Disconnect();
}
```

### 인터페이스의 장점

```
┌──────────────────────────────────────────────────────────┐
│                    IPLCClient                             │
│                   (인터페이스)                             │
│                                                          │
│   "이런 기능은 반드시 있어야 해!"                           │
└──────────────────────┬───────────────────────────────────┘
                       │
           ┌───────────┴───────────┐
           ▼                       ▼
┌─────────────────┐      ┌─────────────────┐
│   SLMPClient    │      │  MockPLCClient  │
│  (실제 PLC용)   │      │   (테스트용)    │
└─────────────────┘      └─────────────────┘

→ 코드 수정 없이 구현체만 교체 가능!
```

---

## SLMP 3E Frame 구조

```
┌─────────────────────────────────────────────────────────────┐
│                    SLMP 3E Frame 구조                        │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  서브헤더(2) │ 네트워크(1) │ PC번호(1) │ 요청처I/O(2)        │
│     0x5000   │     0x00    │   0xFF    │    0x03FF          │
│                                                             │
│  스테이션(1) │  데이터길이(2)  │ CPU타이머(2) │              │
│     0x00     │     가변       │   0x000A     │              │
│                                                             │
│  명령(2) │ 서브명령(2) │ 디바이스번호(3) │ 디바이스코드(1)   │
│  0x0401  │   0x0000    │     가변        │      가변        │
│                                                             │
│  점수(2)  │  데이터(가변)                                   │
│   가변    │     ...                                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 디바이스 코드

```csharp
private byte GetDeviceCode(string device)
{
    char prefix = char.ToUpper(device[0]);

    switch (prefix)
    {
        case 'D': return 0xA8;   // 데이터 레지스터
        case 'M': return 0x90;   // 내부 릴레이
        case 'X': return 0x9C;   // 입력
        case 'Y': return 0x9D;   // 출력
        case 'R': return 0xAF;   // 파일 레지스터
        case 'W': return 0xB4;   // 링크 레지스터
        default:  return 0xA8;
    }
}
```

---

## 자주 묻는 질문

**Q: SLMP 외에 다른 프로토콜도 있나요?**

A: 네! 미쓰비시는 MC Protocol, CC-Link 등도 사용해요.
   다른 제조사는 Modbus, PROFINET, EtherNet/IP 등을 쓰고요.

**Q: 포트 5007은 고정인가요?**

A: 기본값이 5007이지만 PLC 설정에서 변경할 수 있어요.
   방화벽 설정도 확인해야 합니다!

**Q: 비누 잔량이 왜 0~1000인가요?**

A: PLC는 보통 정수(int)로 통신해요.
   소수점 표현을 위해 10배(0~1000)로 저장하고,
   Unity에서 나누기 10으로 퍼센트로 변환합니다.

---

## 다음 챕터 미리보기

다음 챕터에서는 **NetworkManager**를 살펴봅니다.
실제 통신 루프, Mock 클라이언트, 에지 검출 등을 알아볼 거예요!
