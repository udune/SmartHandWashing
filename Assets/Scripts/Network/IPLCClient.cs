using System.Threading.Tasks;

namespace Network
{
    public interface IPLCClient
    {
        bool IsConnected { get; }
        Task ConnectAsync(string ip, int port, int timeoutMs);
        void Disconnect();
        Task<int[]> ReadWordsAsync(string device, int count);
        Task<bool[]> ReadBitsAsync(string device, int count);
        Task WriteWordsAsync(string device, int[] values);
        Task WriteBitsAsync(string device, bool[] values);
    }
}
