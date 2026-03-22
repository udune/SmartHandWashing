# SLMPClient.cs 코드 설명서

## 개요
- **파일 경로**: `Assets/Scripts/Network/SLMPClient.cs`
- **목적**: 미쓰비시 PLC(iQ-R, iQ-F, Q, L 시리즈)와 SLMP(Seamless Message Protocol) 3E Frame Binary 모드로 TCP 통신하는 클라이언트. `IPLCClient` 인터페이스 구현.

## SLMP 프로토콜 개요

### SLMP (Seamless Message Protocol)
- 미쓰비시 FA 기기 간 통신 표준 프로토콜
- TCP/UDP 지원, 이 코드는 TCP 사용
- 3E Frame: 가장 일반적인 프레임 형식

### 3E Frame 구조
```
┌────────────────┬────────────────┬────────────────┐
│   서브헤더(2)   │   헤더(7)      │   데이터부      │
│   0x5000       │ 네트워크/PC/IO │ 명령+디바이스   │
└────────────────┴────────────────┴────────────────┘
```

## 코드 분석

### Line 1-4: using 문
```csharp
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
```
- `System.Net.Sockets`: `TcpClient`, `NetworkStream` 소켓 통신
- `System.Threading.Tasks`: `async/await` 비동기 패턴

### Line 10-25: 클래스 선언 및 상태
```csharp
public class SLMPClient : IPLCClient
{
    private TcpClient _tcp;
    private NetworkStream _stream;

    public bool IsConnected { get { ... } }
}
```
- `TcpClient`: .NET TCP 소켓 클라이언트
- `NetworkStream`: TCP 스트림 읽기/쓰기
- `IsConnected`: null 체크 + Connected 상태 확인

### Line 28-46: ConnectAsync 메서드
```csharp
public async Task ConnectAsync(string ip, int port, int timeoutMs)
{
    _tcp = new TcpClient();
    var connectTask = _tcp.ConnectAsync(ip, port);
    if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
        throw new TimeoutException(...);

    _stream = _tcp.GetStream();
}
```
**타임아웃 구현 패턴:**
- `Task.WhenAny(task1, task2)`: 먼저 완료되는 Task 반환
- `connectTask` vs `Task.Delay(timeoutMs)` 경쟁
- Delay가 먼저 완료되면 타임아웃 예외 발생

### Line 48-54: Disconnect 메서드
```csharp
public void Disconnect()
{
    _stream?.Close();
    _tcp?.Close();
    _stream = null;
    _tcp = null;
}
```
- 스트림과 소켓 순서대로 정리
- null 대입으로 IsConnected = false 보장

### Line 57-72: ReadWordsAsync / ReadBitsAsync
```csharp
public async Task<int[]> ReadWordsAsync(string device, int count)
{
    byte[] req = BuildReadRequest(device, count, 0x0401, 0x0000);
    byte[] res = await SendReceiveAsync(req);
    return ParseWordResponse(res, count);
}
```
**SLMP 명령 코드:**
| 명령 | 서브명령 | 용도 |
|------|---------|------|
| 0x0401 | 0x0000 | 배치 읽기 (워드 단위) |
| 0x0401 | 0x0001 | 배치 읽기 (비트 단위) |
| 0x1401 | 0x0000 | 배치 쓰기 (워드 단위) |
| 0x1401 | 0x0001 | 배치 쓰기 (비트 단위) |

### Line 92-110: BuildReadRequest 메서드
```csharp
private byte[] BuildReadRequest(string device, int count, ushort cmd, ushort subCmd)
{
    byte[] data = new byte[10];
    // 명령(2) + 서브명령(2) + 디바이스번호(3) + 디바이스코드(1) + 점수(2)
    data[0] = (byte)(cmd & 0xFF);         // 명령 하위
    data[1] = (byte)(cmd >> 8);           // 명령 상위
    ...
    return Wrap3EFrame(data);
}
```
**리틀 엔디안 (Little Endian):**
- SLMP는 리틀 엔디안 사용
- 0x0401 → data[0]=0x01, data[1]=0x04

**디바이스 번호 (3바이트):**
```csharp
data[4] = (byte)(devNum & 0xFF);        // 하위
data[5] = (byte)((devNum >> 8) & 0xFF); // 중간
data[6] = (byte)((devNum >> 16) & 0xFF);// 상위
```

### Line 112-136: BuildWriteWordRequest 메서드
```csharp
private byte[] BuildWriteWordRequest(string device, int[] values)
{
    int dataLen = 10 + values.Length * 2;  // 헤더(10) + 데이터(2바이트×개수)
    byte[] data = new byte[dataLen];
    // 명령 0x1401 (쓰기)
    data[0] = 0x01;
    data[1] = 0x14;
    ...
    // 값 데이터 추가 (리틀 엔디안)
    for (int i = 0; i < values.Length; i++)
    {
        data[10 + i * 2] = (byte)(values[i] & 0xFF);
        data[10 + i * 2 + 1] = (byte)(values[i] >> 8);
    }
}
```

### Line 138-179: BuildWriteBitRequest 메서드 (니블 패킹)
```csharp
private byte[] BuildWriteBitRequest(string device, bool[] values)
{
    int packed = (values.Length + 1) / 2;  // 2비트당 1바이트
    ...
    for (int i = 0; i < values.Length; i++)
    {
        int nibble = values[i] ? 0x10 : 0x00;
        if (i % 2 == 0)
            data[byteIdx] = (byte)(nibble);      // 상위 니블
        else
            data[byteIdx] |= (byte)(nibble >> 4);// 하위 니블
    }
}
```
**SLMP 비트 패킹:**
- 1바이트에 2개 비트 저장 (니블 단위)
- bit0 → 상위 니블 (0x10=ON, 0x00=OFF)
- bit1 → 하위 니블 (0x01=ON, 0x00=OFF)

예: [true, false, true] → 0x10, 0x10 (2바이트)

### Line 182-207: Wrap3EFrame 메서드
```csharp
private byte[] Wrap3EFrame(byte[] dataBody)
{
    // 3E Frame 헤더 구조 (11바이트)
    byte[] full = new byte[11 + dataBody.Length];
    full[0] = 0x50;  // 서브헤더 (3E Frame)
    full[1] = 0x00;
    full[2] = 0x00;  // 네트워크 번호
    full[3] = 0xFF;  // PC 번호
    full[4] = 0xFF;
    full[5] = 0x03;  // 요청처 모듈 I/O
    full[6] = 0x00;  // 요청처 스테이션
    full[7] = (byte)(dataLen & 0xFF);   // 데이터 길이 하위
    full[8] = (byte)(dataLen >> 8);     // 데이터 길이 상위
    full[9] = 0x0A;  // CPU 타이머 (10 = 1초)
    full[10] = 0x00;
    // dataBody 복사...
}
```

**3E Frame 헤더 필드:**
| 오프셋 | 크기 | 값 | 설명 |
|--------|------|-----|------|
| 0-1 | 2 | 0x5000 | 서브헤더 (3E Binary) |
| 2 | 1 | 0x00 | 네트워크 번호 |
| 3 | 1 | 0xFF | PC 번호 |
| 4-5 | 2 | 0x03FF | 요청처 모듈 I/O |
| 6 | 1 | 0x00 | 요청처 스테이션 |
| 7-8 | 2 | 가변 | 데이터 길이 |
| 9-10 | 2 | 0x000A | CPU 감시 타이머 |

### Line 213-245: 응답 파서
```csharp
private int[] ParseWordResponse(byte[] res, int count)
{
    int offset = 11; // 헤더(9) + 종료코드(2)
    for (int i = 0; i < count; i++)
    {
        result[i] = res[offset + i * 2] | (res[offset + i * 2 + 1] << 8);
    }
}

private bool[] ParseBitResponse(byte[] res, int count)
{
    for (int i = 0; i < count; i++)
    {
        int byteVal = res[offset + i / 2];
        if (i % 2 == 0)
            bit = (byteVal & 0x10) != 0;  // 상위 니블
        else
            bit = (byteVal & 0x01) != 0;  // 하위 니블
    }
}
```

### Line 251-269: SendReceiveAsync 메서드
```csharp
private async Task<byte[]> SendReceiveAsync(byte[] request)
{
    await _stream.WriteAsync(request, 0, request.Length);
    int len = await _stream.ReadAsync(buf, 0, buf.Length);

    // 종료코드 확인 (offset 9-10)
    int endCode = buf[9] | (buf[10] << 8);
    if (endCode != 0x0000)
        throw new Exception($"SLMP 오류 코드: 0x{endCode:X4}");
}
```
**SLMP 종료 코드:**
| 코드 | 의미 |
|------|------|
| 0x0000 | 정상 완료 |
| 0xC059 | 디바이스 범위 오류 |
| 0xC061 | 요청 데이터 길이 오류 |

### Line 275-316: 헬퍼 메서드
```csharp
private int ParseDeviceNumber(string device)
{
    // "D100" → 100, "M0" → 0
}

private byte GetDeviceCode(string device)
{
    switch (prefix)
    {
        case 'D': return 0xA8;  // 데이터 레지스터
        case 'M': return 0x90;  // 내부 릴레이
        case 'X': return 0x9C;  // 입력
        case 'Y': return 0x9D;  // 출력
        ...
    }
}
```
**SLMP 디바이스 코드표:**
| 접두사 | 코드 | 설명 |
|--------|------|------|
| D | 0xA8 | 데이터 레지스터 |
| M | 0x90 | 내부 릴레이 |
| X | 0x9C | 입력 접점 |
| Y | 0x9D | 출력 접점 |
| R | 0xAF | 파일 레지스터 |
| W | 0xB4 | 링크 레지스터 |

## 패킷 예시

### D0에서 2워드 읽기 요청
```
50 00 00 FF FF 03 00 0E 00 0A 00  ← 헤더 (11바이트)
01 04 00 00                        ← 명령 0x0401 + 서브명령 0x0000
00 00 00                           ← 디바이스 번호 0
A8                                 ← 디바이스 코드 (D)
02 00                              ← 점수 2
```

### 응답 (D0=1000, D1=500)
```
D0 00 00 FF FF 03 00 06 00         ← 헤더 (9바이트)
00 00                              ← 종료코드 0x0000 (정상)
E8 03                              ← D0 = 0x03E8 = 1000
F4 01                              ← D1 = 0x01F4 = 500
```

## 에러 처리

| 상황 | 처리 |
|------|------|
| 연결 타임아웃 | `TimeoutException` 발생 |
| 연결 실패 | `Disconnect()` 후 예외 재발생 |
| 응답 부족 | `Exception("응답 너무 짧음")` |
| SLMP 오류 | `Exception("오류 코드: 0xXXXX")` |

## 사용 예시

```csharp
var client = new SLMPClient();
await client.ConnectAsync("192.168.1.10", 5000, 3000);

// D0에서 워드 읽기
int[] data = await client.ReadWordsAsync("D0", 1);
Debug.Log($"비누 잔량: {data[0]}");

// M0에 비트 쓰기
await client.WriteBitsAsync("M0", new[] { true });

client.Disconnect();
```
