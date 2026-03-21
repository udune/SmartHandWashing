using System.Threading.Tasks;

/// <summary>
/// PLC 통신 인터페이스.
/// Mock 클라이언트와 실제 SLMP 클라이언트를 교체 가능하게 한다.
/// </summary>
public interface IPLCClient
{
    bool IsConnected { get; }

    /// <summary>device 시작 주소에서 count개 워드 읽기 (D 디바이스용)</summary>
    Task<int[]> ReadWordsAsync(string device, int count);

    /// <summary>device 시작 주소에서 count개 비트 읽기 (M 디바이스용)</summary>
    Task<bool[]> ReadBitsAsync(string device, int count);

    /// <summary>워드 쓰기</summary>
    Task WriteWordsAsync(string device, int[] values);

    /// <summary>비트 쓰기</summary>
    Task WriteBitsAsync(string device, bool[] values);

    Task ConnectAsync(string ip, int port, int timeoutMs);
    void Disconnect();
}
