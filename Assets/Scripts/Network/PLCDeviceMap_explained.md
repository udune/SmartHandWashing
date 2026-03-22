# PLCDeviceMap.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Network/PLCDeviceMap.cs`
- **목적**: PLC 통신 설정 및 디바이스 주소 매핑을 정의하는 데이터 클래스. JSON 직렬화를 통해 외부 설정 파일(`PLCConfig.json`)에서 로드 가능.

## 설계 패턴
- **Data Transfer Object (DTO)**: 순수 데이터 전송용 클래스
- **Configuration Object**: 외부 설정을 코드로 매핑

## 코드 분석

### Line 1-11: PLCDeviceMap 클래스

```csharp
[System.Serializable]
public class PLCDeviceMap
{
    public string soapLevel  = "D0";
    public string soapBtn    = "M0";
    public string waterBtn   = "M1";
    public string airBtn     = "M2";
    public string soapAlarm  = "M10";
    public string waterAlarm = "M11";
    public string usageCount = "D10";
}
```

#### `[System.Serializable]` 어트리뷰트
- Unity의 `JsonUtility` 및 Inspector에서 직렬화 가능하게 만듦
- JSON ↔ C# 객체 변환 필수 조건

#### 필드 설명

| 필드 | 기본값 | PLC 디바이스 | 용도 |
|------|--------|-------------|------|
| `soapLevel` | "D0" | D 레지스터 (워드) | 비누 잔량 (0~1000) |
| `soapBtn` | "M0" | M 릴레이 (비트) | 비누 버튼 상태 |
| `waterBtn` | "M1" | M 릴레이 (비트) | 물 버튼 상태 |
| `airBtn` | "M2" | M 릴레이 (비트) | 에어 버튼 상태 |
| `soapAlarm` | "M10" | M 릴레이 (비트) | 비누 부족 알람 |
| `waterAlarm` | "M11" | M 릴레이 (비트) | 물 공급 알람 |
| `usageCount` | "D10" | D 레지스터 (워드) | 누적 사용 횟수 |

### Line 13-22: PLCConfig 클래스

```csharp
[System.Serializable]
public class PLCConfig
{
    public bool   useMock        = true;
    public string ip             = "192.168.1.10";
    public int    port           = 5007;
    public int    pollIntervalMs = 100;
    public int    timeoutMs      = 2000;
    public PLCDeviceMap devices  = new PLCDeviceMap();
}
```

#### 필드 설명

| 필드 | 기본값 | 용도 |
|------|--------|------|
| `useMock` | true | Mock 클라이언트 사용 여부 (true=테스트, false=실제 PLC) |
| `ip` | "192.168.1.10" | PLC의 IP 주소 |
| `port` | 5007 | SLMP 통신 포트 (미쓰비시 기본: 5000~5007) |
| `pollIntervalMs` | 100 | 폴링 주기 (밀리초) |
| `timeoutMs` | 2000 | 연결/통신 타임아웃 (밀리초) |
| `devices` | new PLCDeviceMap() | 디바이스 주소 매핑 (중첩 객체) |

#### 중첩 객체 직렬화
- `PLCDeviceMap devices = new PLCDeviceMap()`
- JSON에서 중첩 구조로 표현됨
- `JsonUtility.FromJson`이 자동으로 중첩 객체도 역직렬화

## JSON 파일 예시

`Resources/PLCConfig.json`:
```json
{
  "useMock": false,
  "ip": "192.168.1.100",
  "port": 5000,
  "pollIntervalMs": 100,
  "timeoutMs": 3000,
  "devices": {
    "soapLevel": "D0",
    "soapBtn": "M0",
    "waterBtn": "M1",
    "airBtn": "M2",
    "soapAlarm": "M10",
    "waterAlarm": "M11",
    "usageCount": "D10"
  }
}
```

## 미쓰비시 PLC 디바이스 타입 참고

| 접두사 | 타입 | 비트/워드 | 용도 |
|--------|------|----------|------|
| D | 데이터 레지스터 | 워드 (16비트) | 수치 저장 (센서값, 카운터) |
| M | 보조 릴레이 | 비트 | 내부 플래그, 버튼 상태 |
| X | 입력 접점 | 비트 | 외부 센서/스위치 입력 |
| Y | 출력 접점 | 비트 | 외부 액추에이터 제어 |
| W | 링크 레지스터 | 워드 | 네트워크 통신용 |

## 사용 예시

### NetworkManager에서 로드
```csharp
var json = Resources.Load<TextAsset>("PLCConfig");
PLCConfig config = JsonUtility.FromJson<PLCConfig>(json.text);

// 디바이스 주소 접근
string soapAddr = config.devices.soapLevel;  // "D0"
int pollMs = config.pollIntervalMs;          // 100
```

### 런타임 주소 변경
```csharp
// 다른 PLC 모델이나 메모리 맵 사용 시
config.devices.soapLevel = "D100";
config.devices.soapBtn = "M100";
```

## 왜 문자열로 주소를 저장하는가?

1. **유연성**: 다양한 PLC 주소 형식 지원 ("D0", "D100", "MW10" 등)
2. **JSON 호환**: 숫자+접두사 조합을 단일 필드로 저장
3. **파싱 용이**: `IncrementDevice()` 함수에서 접두사/숫자 분리 처리

## 기본값의 의미

모든 필드에 기본값이 지정되어 있어:
- JSON 파일이 없어도 `new PLCConfig()`로 동작 가능
- 일부 필드만 JSON에 있어도 나머지는 기본값 유지
- Mock 모드(`useMock = true`)가 기본이라 개발 중 PLC 없이 테스트 가능
