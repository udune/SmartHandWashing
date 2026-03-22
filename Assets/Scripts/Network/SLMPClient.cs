using System;
// System.Net.Sockets: TcpClient, NetworkStream TCP 소켓 통신
using System.Net.Sockets;
// System.Threading.Tasks: async/await 비동기 패턴
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 미쓰비시 SLMP 3E Frame (Binary) TCP 클라이언트.
/// iQ-R / iQ-F / Q / L 시리즈 대응.
/// </summary>
// IPLCClient 구현: MockPLCClient와 동일 인터페이스로 교체 가능
public class SLMPClient : IPLCClient
{
    // TcpClient: .NET 표준 TCP 소켓 클라이언트
    private TcpClient _tcp;
    // NetworkStream: TCP 연결의 읽기/쓰기 스트림
    private NetworkStream _stream;

    // IsConnected: null 체크 + 소켓 연결 상태 확인
    public bool IsConnected
    {
        get
        {
            if (_tcp != null && _tcp.Connected)
            {
                return true;
            }
            return false;
        }
    }

    // ── 연결 ──────────────────────────────────────────────────────────
    // ConnectAsync: TCP 소켓 연결 + 타임아웃 처리
    public async Task ConnectAsync(string ip, int port, int timeoutMs)
    {
        try
        {
            _tcp = new TcpClient();
            var connectTask = _tcp.ConnectAsync(ip, port);
            // Task.WhenAny: 두 Task 중 먼저 완료되는 것 반환
            // connectTask vs Delay 경쟁 → Delay 승리 시 타임아웃
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
                throw new TimeoutException($"SLMP 연결 타임아웃: {ip}:{port}");

            // GetStream(): TCP 연결의 NetworkStream 획득
            _stream = _tcp.GetStream();
            Debug.Log($"[SLMP] 연결 성공: {ip}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SLMP] 연결 실패: {e.Message}");
            Disconnect();
            // throw: 예외를 상위 호출자에게 전파
            throw;
        }
    }

    // Disconnect: 스트림 → 소켓 순서로 정리, null 대입으로 IsConnected=false 보장
    public void Disconnect()
    {
        _stream?.Close();
        _tcp?.Close();
        _stream = null;
        _tcp = null;
    }

    // ── 워드 읽기 (D 디바이스) ────────────────────────────────────────
    // ReadWordsAsync: D 레지스터에서 16비트 워드 배열 읽기
    public async Task<int[]> ReadWordsAsync(string device, int count)
    {
        // 명령: 0x0401 (배치 읽기), 서브명령: 0x0000 (워드 단위)
        byte[] req = BuildReadRequest(device, count, 0x0401, 0x0000);
        byte[] res = await SendReceiveAsync(req);
        return ParseWordResponse(res, count);
    }

    // ── 비트 읽기 (M 디바이스) ────────────────────────────────────────
    // ReadBitsAsync: M 릴레이에서 비트(ON/OFF) 배열 읽기
    public async Task<bool[]> ReadBitsAsync(string device, int count)
    {
        // 명령: 0x0401, 서브명령: 0x0001 (비트 단위)
        byte[] req = BuildReadRequest(device, count, 0x0401, 0x0001);
        byte[] res = await SendReceiveAsync(req);
        return ParseBitResponse(res, count);
    }

    // ── 워드 쓰기 ────────────────────────────────────────────────────
    // WriteWordsAsync: D 레지스터에 16비트 워드 배열 쓰기
    public async Task WriteWordsAsync(string device, int[] values)
    {
        byte[] req = BuildWriteWordRequest(device, values);
        await SendReceiveAsync(req);
    }

    // ── 비트 쓰기 ────────────────────────────────────────────────────
    // WriteBitsAsync: M 릴레이에 비트(ON/OFF) 배열 쓰기
    public async Task WriteBitsAsync(string device, bool[] values)
    {
        byte[] req = BuildWriteBitRequest(device, values);
        await SendReceiveAsync(req);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SLMP 3E Frame 패킷 빌더
    // ═══════════════════════════════════════════════════════════════════

    // BuildReadRequest: SLMP 배치 읽기 요청 패킷 생성
    // cmd: 명령코드(0x0401=읽기), subCmd: 서브명령(0x0000=워드, 0x0001=비트)
    private byte[] BuildReadRequest(string device, int count, ushort cmd, ushort subCmd)
    {
        // 데이터부: 명령(2) + 서브명령(2) + 디바이스번호(3) + 디바이스코드(1) + 점수(2)
        byte[] data = new byte[10];
        // 리틀 엔디안: 하위 바이트 먼저 (0x0401 → 0x01, 0x04)
        data[0] = (byte)(cmd & 0xFF);
        data[1] = (byte)(cmd >> 8);
        data[2] = (byte)(subCmd & 0xFF);
        data[3] = (byte)(subCmd >> 8);

        // 디바이스 번호 파싱: "D100" → 100
        int devNum = ParseDeviceNumber(device);
        // 디바이스 번호 3바이트 (리틀 엔디안)
        data[4] = (byte)(devNum & 0xFF);
        data[5] = (byte)((devNum >> 8) & 0xFF);
        data[6] = (byte)((devNum >> 16) & 0xFF);
        // 디바이스 코드: D=0xA8, M=0x90 등
        data[7] = GetDeviceCode(device);
        // 점수 (읽을 개수) 2바이트
        data[8] = (byte)(count & 0xFF);
        data[9] = (byte)(count >> 8);

        // 3E Frame 헤더 래핑
        return Wrap3EFrame(data);
    }

    // BuildWriteWordRequest: SLMP 워드 쓰기 요청 패킷 생성 (명령 0x1401)
    private byte[] BuildWriteWordRequest(string device, int[] values)
    {
        // 헤더(10) + 데이터(2바이트 × 값 개수)
        int dataLen = 10 + values.Length * 2;
        byte[] data = new byte[dataLen];
        // 명령 0x1401 (배치 쓰기), 서브명령 0x0000 (워드 단위)
        data[0] = 0x01;
        data[1] = 0x14;
        data[2] = 0x00;
        data[3] = 0x00;

        int devNum = ParseDeviceNumber(device);
        data[4] = (byte)(devNum & 0xFF);
        data[5] = (byte)((devNum >> 8) & 0xFF);
        data[6] = (byte)((devNum >> 16) & 0xFF);
        data[7] = GetDeviceCode(device);
        data[8] = (byte)(values.Length & 0xFF);
        data[9] = (byte)(values.Length >> 8);

        // 값 데이터 추가 (각 워드 2바이트, 리틀 엔디안)
        for (int i = 0; i < values.Length; i++)
        {
            data[10 + i * 2] = (byte)(values[i] & 0xFF);
            data[10 + i * 2 + 1] = (byte)(values[i] >> 8);
        }
        return Wrap3EFrame(data);
    }

    // BuildWriteBitRequest: SLMP 비트 쓰기 요청 패킷 생성 (명령 0x1401, 서브명령 0x0001)
    // SLMP 비트는 니블(4bit) 단위로 패킹: 2비트 → 1바이트
    private byte[] BuildWriteBitRequest(string device, bool[] values)
    {
        // 비트는 니블(4bit) 단위 — 2비트씩 1바이트로 패킹
        // (values.Length + 1) / 2: 올림 나눗셈 (3비트 → 2바이트)
        int packed = (values.Length + 1) / 2;
        byte[] data = new byte[10 + packed];
        data[0] = 0x01;
        data[1] = 0x14;
        data[2] = 0x01;
        data[3] = 0x00;   // 서브명령 0x0001 (비트 단위)

        int devNum = ParseDeviceNumber(device);
        data[4] = (byte)(devNum & 0xFF);
        data[5] = (byte)((devNum >> 8) & 0xFF);
        data[6] = (byte)((devNum >> 16) & 0xFF);
        data[7] = GetDeviceCode(device);
        data[8] = (byte)(values.Length & 0xFF);
        data[9] = (byte)(values.Length >> 8);

        // 니블 패킹: bit0→상위니블(0x10), bit1→하위니블(0x01)
        for (int i = 0; i < values.Length; i++)
        {
            int byteIdx = 10 + i / 2;
            // ON=0x10, OFF=0x00 (상위 니블 기준)
            int nibble;
            if (values[i])
            {
                nibble = 0x10;
            }
            else
            {
                nibble = 0x00;
            }

            // 짝수 인덱스: 상위 니블에 배치
            if (i % 2 == 0)
            {
                data[byteIdx] = (byte)(nibble);
            }
            // 홀수 인덱스: 하위 니블에 OR 연산으로 추가
            else
            {
                data[byteIdx] |= (byte)(nibble >> 4);
            }
        }
        return Wrap3EFrame(data);
    }

    /// <summary>SLMP 3E Frame 헤더 래핑</summary>
    // Wrap3EFrame: 데이터부에 3E Frame 헤더(11바이트) 추가
    private byte[] Wrap3EFrame(byte[] dataBody)
    {
        // 헤더: 서브헤더(2) + 네트워크번호(1) + PC번호(1) + 요청처I/O(2) + 스테이션(1) + 데이터길이(2) + CPU타이머(2)
        // dataLen: CPU타이머(2) + 실제 데이터
        ushort dataLen = (ushort)(dataBody.Length + 2); // CPU타이머 포함
        byte[] frame = new byte[9 + dataBody.Length];

        // 서브헤더 0x5000: 3E Frame Binary 모드
        frame[0] = 0x50;
        frame[1] = 0x00;   // 서브헤더 (3E)
        frame[2] = 0x00;                     // 네트워크 번호 (자국)
        frame[3] = 0xFF;                     // PC 번호 (자국)
        frame[4] = 0xFF;
        frame[5] = 0x03;   // 요청처 모듈 I/O (0x03FF = CPU 유닛)
        frame[6] = 0x00;                     // 요청처 스테이션 (자국)
        // 데이터 길이 (리틀 엔디안)
        frame[7] = (byte)(dataLen & 0xFF);
        frame[8] = (byte)(dataLen >> 8);

        // CPU 타이머는 dataBody 첫 2바이트로 넣지 않고 별도 추가
        // 실제로는 frame[9~10] = CPU 타이머(0x000A), frame[11~] = 실제 데이터
        // 아래 재구성 버전 사용:
        byte[] full = new byte[11 + dataBody.Length];
        // Array.Copy(소스, 소스시작, 대상, 대상시작, 길이)
        Array.Copy(frame, 0, full, 0, 9);
        // CPU 감시 타이머: 0x000A = 10 × 250ms = 2.5초 (응답 대기 시간)
        full[9] = 0x0A;
        full[10] = 0x00;   // CPU 타이머 10 (1초)
        Array.Copy(dataBody, 0, full, 11, dataBody.Length);
        return full;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 응답 파서
    // ═══════════════════════════════════════════════════════════════════

    // ParseWordResponse: 워드 읽기 응답에서 int 배열 추출
    private int[] ParseWordResponse(byte[] res, int count)
    {
        // 응답: 헤더(9) + 종료코드(2) + 데이터(count*2)
        var result = new int[count];
        int offset = 11; // 헤더(9) + 종료코드(2)
        for (int i = 0; i < count; i++)
        {
            // 리틀 엔디안: 하위 바이트 | (상위 바이트 << 8)
            result[i] = res[offset + i * 2] | (res[offset + i * 2 + 1] << 8);
        }
        return result;
    }

    // ParseBitResponse: 비트 읽기 응답에서 bool 배열 추출 (니블 언패킹)
    private bool[] ParseBitResponse(byte[] res, int count)
    {
        // 비트 응답: 니블 단위 (1바이트에 2비트)
        var result = new bool[count];
        int offset = 11;
        for (int i = 0; i < count; i++)
        {
            int byteVal = res[offset + i / 2];
            bool bit;
            // 짝수: 상위 니블(0x10), 홀수: 하위 니블(0x01)
            if (i % 2 == 0)
            {
                bit = (byteVal & 0x10) != 0;
            }
            else
            {
                bit = (byteVal & 0x01) != 0;
            }
            result[i] = bit;
        }
        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    // TCP 송수신
    // ═══════════════════════════════════════════════════════════════════

    // SendReceiveAsync: SLMP 요청 전송 + 응답 수신 + 오류 검증
    private async Task<byte[]> SendReceiveAsync(byte[] request)
    {
        // 연결 상태 확인
        if (!IsConnected) throw new InvalidOperationException("SLMP 미연결 상태");

        // WriteAsync: 비동기로 TCP 스트림에 요청 패킷 전송
        await _stream.WriteAsync(request, 0, request.Length);

        // 응답 버퍼 (512바이트 충분)
        byte[] buf = new byte[512];
        // ReadAsync: 비동기로 응답 수신, len=실제 수신 바이트 수
        int len = await _stream.ReadAsync(buf, 0, buf.Length);

        // 최소 응답 길이 검증 (헤더 9 + 종료코드 2 = 11바이트)
        if (len < 11)
            throw new Exception($"SLMP 응답 너무 짧음: {len} bytes");

        // 종료코드 확인 (offset 9-10, 0x0000 = 정상)
        int endCode = buf[9] | (buf[10] << 8);
        if (endCode != 0x0000)
            // :X4 = 16진수 4자리 포맷
            throw new Exception($"SLMP 오류 코드: 0x{endCode:X4}");

        return buf;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 헬퍼
    // ═══════════════════════════════════════════════════════════════════

    // ParseDeviceNumber: 디바이스 주소에서 숫자 부분 추출
    // "D0" → 0, "M10" → 10, "D100" → 100
    private int ParseDeviceNumber(string device)
    {
        // "D0" → 0,  "M10" → 10,  "D100" → 100
        string numStr = "";
        foreach (char c in device)
        {
            // char.IsDigit: 숫자 문자 판별 ('0'~'9')
            if (char.IsDigit(c))
            {
                numStr += c;
            }
        }

        if (numStr.Length > 0)
        {
            // int.Parse: 문자열 → 정수 변환
            return int.Parse(numStr);
        }
        return 0;
    }

    // GetDeviceCode: 디바이스 접두사 → SLMP Binary 코드 변환
    // 미쓰비시 SLMP 프로토콜 디바이스 코드표 참조
    private byte GetDeviceCode(string device)
    {
        // SLMP Binary 디바이스 코드표 (주요 디바이스)
        // char.ToUpper: 소문자 → 대문자 변환 (d → D)
        char prefix = char.ToUpper(device[0]);

        switch (prefix)
        {
            case 'D':
                return 0xA8;   // 데이터 레지스터 (워드)
            case 'M':
                return 0x90;   // 내부 릴레이 (비트)
            case 'X':
                return 0x9C;   // 입력 접점 (비트)
            case 'Y':
                return 0x9D;   // 출력 접점 (비트)
            case 'R':
                return 0xAF;   // 파일 레지스터 (워드)
            case 'W':
                return 0xB4;   // 링크 레지스터 (워드)
            default:
                return 0xA8;   // 기본값: D 레지스터
        }
    }
}
