// System.Threading.Tasks: 비동기 Task/Task<T> 사용을 위한 네임스페이스
using System.Threading.Tasks;

/// <summary>
/// PLC 통신 인터페이스.
/// Mock 클라이언트와 실제 SLMP 클라이언트를 교체 가능하게 한다.
/// </summary>
// 인터페이스: 구현 클래스(MockPLCClient, SLMPClient)가 반드시 구현해야 할 계약 정의
public interface IPLCClient
{
    // 읽기 전용 프로퍼티: PLC 연결 상태 확인 (true=연결됨)
    bool IsConnected { get; }

    /// <summary>device 시작 주소에서 count개 워드 읽기 (D 디바이스용)</summary>
    // device: "D100" 형식의 주소, count: 읽을 워드 수, 반환: 16비트 정수 배열
    Task<int[]> ReadWordsAsync(string device, int count);

    /// <summary>device 시작 주소에서 count개 비트 읽기 (M 디바이스용)</summary>
    // device: "M0" 형식의 주소, count: 읽을 비트 수, 반환: ON/OFF 상태 배열
    Task<bool[]> ReadBitsAsync(string device, int count);

    /// <summary>워드 쓰기</summary>
    // device: 시작 주소, values: 쓸 16비트 워드 배열
    Task WriteWordsAsync(string device, int[] values);

    /// <summary>비트 쓰기</summary>
    // device: 시작 주소, values: 쓸 비트 배열 (true=ON, false=OFF)
    Task WriteBitsAsync(string device, bool[] values);

    // ConnectAsync: PLC에 TCP 소켓 연결 (ip: PLC IP, port: 통신 포트, timeoutMs: 타임아웃)
    Task ConnectAsync(string ip, int port, int timeoutMs);

    // Disconnect: 연결 해제 및 소켓 리소스 정리 (동기 메서드)
    void Disconnect();
}
