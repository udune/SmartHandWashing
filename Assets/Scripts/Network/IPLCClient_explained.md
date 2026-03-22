# IPLCClient.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Network/IPLCClient.cs`
- **목적**: PLC(Programmable Logic Controller)와의 통신을 추상화하는 인터페이스. Mock 클라이언트와 실제 SLMP(Seamless Message Protocol) 클라이언트를 교체 가능하게 하여 의존성 역전 원칙(DIP)을 구현.

## 설계 패턴
이 인터페이스는 **Strategy Pattern**과 **Dependency Injection**을 가능하게 합니다:
- 개발 중: `MockPLCClient` 사용 (실제 PLC 없이 테스트)
- 프로덕션: `SLMPClient` 사용 (실제 미쓰비시 PLC 통신)

## 코드 분석

### Line 1: using 문
```csharp
using System.Threading.Tasks;
```
- `System.Threading.Tasks`: `Task` 및 `Task<T>` 클래스를 사용하기 위한 네임스페이스
- 비동기 프로그래밍(async/await)을 위해 필수

### Line 3-6: XML 문서 주석
```csharp
/// <summary>
/// PLC 통신 인터페이스.
/// Mock 클라이언트와 실제 SLMP 클라이언트를 교체 가능하게 한다.
/// </summary>
```
- `<summary>`: Visual Studio/Rider에서 IntelliSense 툴팁으로 표시됨
- API 문서 자동 생성 도구(DocFX 등)에서 활용 가능

### Line 7-8: 인터페이스 선언
```csharp
public interface IPLCClient
{
```
- `public interface`: 외부에서 접근 가능한 인터페이스 정의
- 접두사 `I`는 C# 인터페이스 명명 규칙 (Interface)
- 인터페이스는 구현 없이 메서드/프로퍼티 시그니처만 정의

### Line 9: 연결 상태 프로퍼티
```csharp
bool IsConnected { get; }
```
- 읽기 전용 프로퍼티 (getter만 존재)
- PLC 연결 상태를 외부에서 확인 가능
- 구현 클래스에서 실제 연결 상태 로직 제공

### Line 11-12: 워드 읽기 메서드
```csharp
/// <summary>device 시작 주소에서 count개 워드 읽기 (D 디바이스용)</summary>
Task<int[]> ReadWordsAsync(string device, int count);
```
- **D 디바이스**: 미쓰비시 PLC의 데이터 레지스터 (16비트 워드 단위)
- `device`: 시작 주소 (예: "D100" → D100번지부터)
- `count`: 읽을 워드 개수
- `Task<int[]>`: 비동기로 정수 배열 반환 (await 가능)
- 용도: 센서 값, 카운터, 아날로그 데이터 읽기

### Line 14-15: 비트 읽기 메서드
```csharp
/// <summary>device 시작 주소에서 count개 비트 읽기 (M 디바이스용)</summary>
Task<bool[]> ReadBitsAsync(string device, int count);
```
- **M 디바이스**: 미쓰비시 PLC의 내부 릴레이 (비트 단위 ON/OFF)
- `device`: 시작 주소 (예: "M0" → M0번지부터)
- `count`: 읽을 비트 개수
- `Task<bool[]>`: 비동기로 불리언 배열 반환
- 용도: 스위치 상태, 알람 플래그, 동작 상태 읽기

### Line 17-18: 워드 쓰기 메서드
```csharp
/// <summary>워드 쓰기</summary>
Task WriteWordsAsync(string device, int[] values);
```
- `device`: 시작 주소
- `values`: 쓸 워드 값들의 배열
- `Task`: 반환값 없는 비동기 작업 (완료만 await)
- 용도: 설정값 변경, 목표값 전송

### Line 20-21: 비트 쓰기 메서드
```csharp
/// <summary>비트 쓰기</summary>
Task WriteBitsAsync(string device, bool[] values);
```
- `device`: 시작 주소
- `values`: 쓸 비트 값들의 배열 (true=ON, false=OFF)
- 용도: 출력 제어, 명령 전송, 플래그 설정

### Line 23: 연결 메서드
```csharp
Task ConnectAsync(string ip, int port, int timeoutMs);
```
- `ip`: PLC의 IP 주소 (예: "192.168.1.10")
- `port`: 통신 포트 (SLMP 기본값: 5000 또는 5001)
- `timeoutMs`: 연결 타임아웃 (밀리초)
- 비동기 연결로 UI 블로킹 방지

### Line 24: 연결 해제 메서드
```csharp
void Disconnect();
```
- 동기 메서드 (즉시 연결 해제)
- 소켓 리소스 해제
- `IsConnected`를 `false`로 설정해야 함

## PLC 디바이스 타입 참고 (미쓰비시 MELSEC)

| 디바이스 | 타입 | 용도 |
|---------|------|------|
| D | 워드(16비트) | 데이터 레지스터, 수치 저장 |
| M | 비트 | 내부 릴레이, ON/OFF 상태 |
| X | 비트 | 입력 접점 (센서 입력) |
| Y | 비트 | 출력 접점 (액추에이터 제어) |
| W | 워드 | 링크 레지스터 |

## 구현 예시

이 인터페이스를 구현하는 클래스:
```csharp
public class MockPLCClient : IPLCClient { /* 테스트용 더미 구현 */ }
public class SLMPClient : IPLCClient { /* 실제 SLMP 프로토콜 구현 */ }
```

## 사용 예시 (의존성 주입)
```csharp
public class StationController : MonoBehaviour
{
    private IPLCClient _plc;

    public void Initialize(IPLCClient client)
    {
        _plc = client; // Mock 또는 실제 클라이언트 주입
    }

    private async void ReadSensorData()
    {
        if (_plc.IsConnected)
        {
            int[] data = await _plc.ReadWordsAsync("D100", 10);
            // data[0] ~ data[9] 처리
        }
    }
}
```
