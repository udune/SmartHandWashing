using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 미쓰비시 SLMP 3E Frame (Binary) TCP 클라이언트.
/// iQ-R / iQ-F / Q / L 시리즈 대응.
/// </summary>
public class SLMPClient : IPLCClient
{
    private TcpClient _tcp;
    private NetworkStream _stream;

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
    public async Task ConnectAsync(string ip, int port, int timeoutMs)
    {
        try
        {
            _tcp = new TcpClient();
            var connectTask = _tcp.ConnectAsync(ip, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) != connectTask)
                throw new TimeoutException($"SLMP 연결 타임아웃: {ip}:{port}");

            _stream = _tcp.GetStream();
            Debug.Log($"[SLMP] 연결 성공: {ip}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SLMP] 연결 실패: {e.Message}");
            Disconnect();
            throw;
        }
    }

    public void Disconnect()
    {
        _stream?.Close();
        _tcp?.Close();
        _stream = null;
        _tcp = null;
    }

    // ── 워드 읽기 (D 디바이스) ────────────────────────────────────────
    public async Task<int[]> ReadWordsAsync(string device, int count)
    {
        // 명령: 0x0401 (배치 읽기), 서브명령: 0x0000 (워드 단위)
        byte[] req = BuildReadRequest(device, count, 0x0401, 0x0000);
        byte[] res = await SendReceiveAsync(req);
        return ParseWordResponse(res, count);
    }

    // ── 비트 읽기 (M 디바이스) ────────────────────────────────────────
    public async Task<bool[]> ReadBitsAsync(string device, int count)
    {
        // 명령: 0x0401, 서브명령: 0x0001 (비트 단위)
        byte[] req = BuildReadRequest(device, count, 0x0401, 0x0001);
        byte[] res = await SendReceiveAsync(req);
        return ParseBitResponse(res, count);
    }

    // ── 워드 쓰기 ────────────────────────────────────────────────────
    public async Task WriteWordsAsync(string device, int[] values)
    {
        byte[] req = BuildWriteWordRequest(device, values);
        await SendReceiveAsync(req);
    }

    // ── 비트 쓰기 ────────────────────────────────────────────────────
    public async Task WriteBitsAsync(string device, bool[] values)
    {
        byte[] req = BuildWriteBitRequest(device, values);
        await SendReceiveAsync(req);
    }

    // ═══════════════════════════════════════════════════════════════════
    // SLMP 3E Frame 패킷 빌더
    // ═══════════════════════════════════════════════════════════════════

    private byte[] BuildReadRequest(string device, int count, ushort cmd, ushort subCmd)
    {
        // 데이터부: 명령(2) + 서브명령(2) + 디바이스번호(3) + 디바이스코드(1) + 점수(2)
        byte[] data = new byte[10];
        data[0] = (byte)(cmd & 0xFF);
        data[1] = (byte)(cmd >> 8);
        data[2] = (byte)(subCmd & 0xFF);
        data[3] = (byte)(subCmd >> 8);

        int devNum = ParseDeviceNumber(device);
        data[4] = (byte)(devNum & 0xFF);
        data[5] = (byte)((devNum >> 8) & 0xFF);
        data[6] = (byte)((devNum >> 16) & 0xFF);
        data[7] = GetDeviceCode(device);
        data[8] = (byte)(count & 0xFF);
        data[9] = (byte)(count >> 8);

        return Wrap3EFrame(data);
    }

    private byte[] BuildWriteWordRequest(string device, int[] values)
    {
        int dataLen = 10 + values.Length * 2;
        byte[] data = new byte[dataLen];
        // 명령 0x1401, 서브명령 0x0000
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

        for (int i = 0; i < values.Length; i++)
        {
            data[10 + i * 2] = (byte)(values[i] & 0xFF);
            data[10 + i * 2 + 1] = (byte)(values[i] >> 8);
        }
        return Wrap3EFrame(data);
    }

    private byte[] BuildWriteBitRequest(string device, bool[] values)
    {
        // 비트는 니블(4bit) 단위 — 2비트씩 1바이트로 패킹
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

        for (int i = 0; i < values.Length; i++)
        {
            int byteIdx = 10 + i / 2;
            int nibble;
            if (values[i])
            {
                nibble = 0x10;
            }
            else
            {
                nibble = 0x00;
            }

            if (i % 2 == 0)
            {
                data[byteIdx] = (byte)(nibble);
            }
            else
            {
                data[byteIdx] |= (byte)(nibble >> 4);
            }
        }
        return Wrap3EFrame(data);
    }

    /// <summary>SLMP 3E Frame 헤더 래핑</summary>
    private byte[] Wrap3EFrame(byte[] dataBody)
    {
        // 헤더: 서브헤더(2) + 네트워크번호(1) + PC번호(1) + 요청처I/O(2) + 스테이션(1) + 데이터길이(2) + CPU타이머(2)
        ushort dataLen = (ushort)(dataBody.Length + 2); // CPU타이머 포함
        byte[] frame = new byte[9 + dataBody.Length];

        frame[0] = 0x50;
        frame[1] = 0x00;   // 서브헤더 (3E)
        frame[2] = 0x00;                     // 네트워크 번호
        frame[3] = 0xFF;                     // PC 번호
        frame[4] = 0xFF;
        frame[5] = 0x03;   // 요청처 모듈 I/O
        frame[6] = 0x00;                     // 요청처 스테이션
        frame[7] = (byte)(dataLen & 0xFF);
        frame[8] = (byte)(dataLen >> 8);

        // CPU 타이머는 dataBody 첫 2바이트로 넣지 않고 별도 추가
        // 실제로는 frame[9~10] = CPU 타이머(0x000A), frame[11~] = 실제 데이터
        // 아래 재구성 버전 사용:
        byte[] full = new byte[11 + dataBody.Length];
        Array.Copy(frame, 0, full, 0, 9);
        full[9] = 0x0A;
        full[10] = 0x00;   // CPU 타이머 10 (1초)
        Array.Copy(dataBody, 0, full, 11, dataBody.Length);
        return full;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 응답 파서
    // ═══════════════════════════════════════════════════════════════════

    private int[] ParseWordResponse(byte[] res, int count)
    {
        // 응답: 헤더(9) + 종료코드(2) + 데이터(count*2)
        var result = new int[count];
        int offset = 11; // 헤더(9) + 종료코드(2)
        for (int i = 0; i < count; i++)
        {
            result[i] = res[offset + i * 2] | (res[offset + i * 2 + 1] << 8);
        }
        return result;
    }

    private bool[] ParseBitResponse(byte[] res, int count)
    {
        // 비트 응답: 니블 단위 (1바이트에 2비트)
        var result = new bool[count];
        int offset = 11;
        for (int i = 0; i < count; i++)
        {
            int byteVal = res[offset + i / 2];
            bool bit;
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

    private async Task<byte[]> SendReceiveAsync(byte[] request)
    {
        if (!IsConnected) throw new InvalidOperationException("SLMP 미연결 상태");

        await _stream.WriteAsync(request, 0, request.Length);

        byte[] buf = new byte[512];
        int len = await _stream.ReadAsync(buf, 0, buf.Length);

        if (len < 11)
            throw new Exception($"SLMP 응답 너무 짧음: {len} bytes");

        // 종료코드 확인 (0x0000 = 정상)
        int endCode = buf[9] | (buf[10] << 8);
        if (endCode != 0x0000)
            throw new Exception($"SLMP 오류 코드: 0x{endCode:X4}");

        return buf;
    }

    // ═══════════════════════════════════════════════════════════════════
    // 헬퍼
    // ═══════════════════════════════════════════════════════════════════

    private int ParseDeviceNumber(string device)
    {
        // "D0" → 0,  "M10" → 10,  "D100" → 100
        string numStr = "";
        foreach (char c in device)
        {
            if (char.IsDigit(c))
            {
                numStr += c;
            }
        }

        if (numStr.Length > 0)
        {
            return int.Parse(numStr);
        }
        return 0;
    }

    private byte GetDeviceCode(string device)
    {
        // SLMP Binary 디바이스 코드표 (주요 디바이스)
        char prefix = char.ToUpper(device[0]);

        switch (prefix)
        {
            case 'D':
                return 0xA8;   // 데이터 레지스터
            case 'M':
                return 0x90;   // 내부 릴레이
            case 'X':
                return 0x9C;   // 입력
            case 'Y':
                return 0x9D;   // 출력
            case 'R':
                return 0xAF;   // 파일 레지스터
            case 'W':
                return 0xB4;   // 링크 레지스터
            default:
                return 0xA8;
        }
    }
}
