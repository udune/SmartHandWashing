using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Network;
using UnityEngine;

/// <summary>
/// 미쓰비시 SLMP 3E Frame (Binary) TCP 클라이언트.
/// iQ-R / iQ-F / Q / L 시리즈 대응.
/// </summary>
public class SLMPClient : IPLCClient
{
    private TcpClient _tcp;
    private NetworkStream _stream;

    public bool IsConnected => _tcp != null && _tcp.Connected;

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

    public async Task<int[]> ReadWordsAsync(string device, int count)
    {
        byte[] req = BuildReadRequest(device, count, 0x0401, 0x0000);
        byte[] res = await SendReceiveAsync(req);
        return ParseWordResponse(res, count);
    }

    public async Task<bool[]> ReadBitsAsync(string device, int count)
    {
        byte[] req = BuildReadRequest(device, count, 0x0401, 0x0001);
        byte[] res = await SendReceiveAsync(req);
        return ParseBitResponse(res, count);
    }

    public async Task WriteWordsAsync(string device, int[] values)
    {
        byte[] req = BuildWriteWordRequest(device, values);
        await SendReceiveAsync(req);
    }

    public async Task WriteBitsAsync(string device, bool[] values)
    {
        byte[] req = BuildWriteBitRequest(device, values);
        await SendReceiveAsync(req);
    }

    private byte[] BuildReadRequest(string device, int count, ushort cmd, ushort subCmd)
    {
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

    // SLMP 비트는 니블(4bit) 단위로 패킹
    private byte[] BuildWriteBitRequest(string device, bool[] values)
    {
        int packed = (values.Length + 1) / 2;
        byte[] data = new byte[10 + packed];
        data[0] = 0x01;
        data[1] = 0x14;
        data[2] = 0x01;
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
            int byteIdx = 10 + i / 2;
            int nibble = values[i] ? 0x10 : 0x00;

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

    private byte[] Wrap3EFrame(byte[] dataBody)
    {
        ushort dataLen = (ushort)(dataBody.Length + 2);
        byte[] frame = new byte[9 + dataBody.Length];

        frame[0] = 0x50;
        frame[1] = 0x00;
        frame[2] = 0x00;
        frame[3] = 0xFF;
        frame[4] = 0xFF;
        frame[5] = 0x03;
        frame[6] = 0x00;
        frame[7] = (byte)(dataLen & 0xFF);
        frame[8] = (byte)(dataLen >> 8);

        byte[] full = new byte[11 + dataBody.Length];
        Array.Copy(frame, 0, full, 0, 9);
        full[9] = 0x0A;
        full[10] = 0x00;
        Array.Copy(dataBody, 0, full, 11, dataBody.Length);
        return full;
    }

    private int[] ParseWordResponse(byte[] res, int count)
    {
        var result = new int[count];
        int offset = 11;
        for (int i = 0; i < count; i++)
        {
            result[i] = res[offset + i * 2] | (res[offset + i * 2 + 1] << 8);
        }
        return result;
    }

    private bool[] ParseBitResponse(byte[] res, int count)
    {
        var result = new bool[count];
        int offset = 11;
        for (int i = 0; i < count; i++)
        {
            int byteVal = res[offset + i / 2];
            result[i] = (i % 2 == 0) ? (byteVal & 0x10) != 0 : (byteVal & 0x01) != 0;
        }
        return result;
    }

    private async Task<byte[]> SendReceiveAsync(byte[] request)
    {
        if (!IsConnected) throw new InvalidOperationException("SLMP 미연결 상태");

        await _stream.WriteAsync(request, 0, request.Length);

        byte[] buf = new byte[512];
        int len = await _stream.ReadAsync(buf, 0, buf.Length);

        if (len < 11)
            throw new Exception($"SLMP 응답 너무 짧음: {len} bytes");

        int endCode = buf[9] | (buf[10] << 8);
        if (endCode != 0x0000)
            throw new Exception($"SLMP 오류 코드: 0x{endCode:X4}");

        return buf;
    }

    /// <summary>
    /// 디바이스 번호 파싱.
    /// X, Y 디바이스는 16진수 주소 (예: X0A0 → 0xA0 = 160)
    /// D, M, T, C 디바이스는 10진수 주소 (예: M13 → 13)
    /// </summary>
    public int ParseDeviceNumber(string device)
    {
        if (string.IsNullOrEmpty(device)) return 0;

        char prefix = char.ToUpper(device[0]);
        string numPart = device.Substring(1);   // 접두어 제거

        // X, Y 디바이스: 16진수 파싱
        if (prefix == 'X' || prefix == 'Y')
        {
            // "X0A0" → "0A0" → 앞의 0 제거 후 16진수 변환
            string hexStr = numPart.TrimStart('0');
            if (string.IsNullOrEmpty(hexStr)) hexStr = "0";

            try
            {
                return Convert.ToInt32(hexStr, 16);
            }
            catch
            {
                Debug.LogWarning($"[SLMP] X/Y 디바이스 주소 파싱 실패: {device}");
                return 0;
            }
        }

        // D, M, T, C, R 디바이스: 10진수 파싱
        string decStr = "";
        foreach (char c in numPart)
            if (char.IsDigit(c)) decStr += c;

        return decStr.Length > 0 ? int.Parse(decStr) : 0;
    }

    /// <summary>X 디바이스 주소 파싱 검증 — Console에서 확인</summary>
    public static void VerifyDeviceAddresses()
    {
        var client = new SLMPClient();
        var testCases = new (string device, int expected)[]
        {
            ("X0A0", 160),   // 0xA0
            ("X0A1", 161),   // 0xA1
            ("X0A2", 162),   // 0xA2
            ("X0A3", 163),   // 0xA3
            ("X0A4", 164),   // 0xA4
            ("X0A5", 165),   // 0xA5
            ("M0",   0  ),
            ("M13",  13 ),
            ("D0",   0  ),
            ("D4",   4  ),
        };

        bool allPass = true;
        foreach (var (device, expected) in testCases)
        {
            int result = client.ParseDeviceNumber(device);
            bool pass  = result == expected;
            if (!pass) allPass = false;
            Debug.Log($"[주소검증] {device} → {result} (기대값: {expected}) {(pass ? "✓" : "✗ 오류")}");
        }
        Debug.Log($"[주소검증] 전체 결과: {(allPass ? "모두 통과" : "오류 있음")}");
    }

    /// <summary>디바이스 접두사 → SLMP Binary 코드 변환</summary>
    private byte GetDeviceCode(string device)
    {
        char prefix = char.ToUpper(device[0]);

        switch (prefix)
        {
            case 'D': return 0xA8;
            case 'M': return 0x90;
            case 'X': return 0x9C;
            case 'Y': return 0x9D;
            case 'R': return 0xAF;
            case 'W': return 0xB4;
            default:  return 0xA8;
        }
    }
}
