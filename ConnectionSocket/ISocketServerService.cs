using ConnectionSocket.LocalEntity;

namespace ConnectionSocket
{
    /// <summary>
    /// TCP Socket 服务端（监听端）业务接口。
    /// 负责启动/停止监听、管理已连接客户端、向指定客户端或全体客户端发送数据，
    /// 并在内存中保留最近收到的消息，供 HTTP 接口观测调试。
    /// </summary>
    public interface ISocketServerService
    {
        /// <summary>
        /// 根据服务端编码获取对应配置。
        /// </summary>
        SocketServerConfig GetServerConfig(string serverCode);

        /// <summary>
        /// 获取全部已加载的服务端编码列表。
        /// </summary>
        List<string> GetAllServerCodes();

        /// <summary>
        /// 批量获取所有服务端运行状态：是否在监听 + 当前客户端数。
        /// </summary>
        Dictionary<string, (bool running, int clientCount)> GetAllServerStatus();

        /// <summary>
        /// 启动指定服务端（开始监听并接受客户端连接）。
        /// </summary>
        Task StartAsync(string serverCode);

        /// <summary>
        /// 停止指定服务端，断开其全部客户端连接。
        /// </summary>
        Task StopAsync(string serverCode);

        /// <summary>
        /// 停止全部服务端。
        /// </summary>
        Task StopAllAsync();

        /// <summary>
        /// 向指定客户端发送字节。
        /// </summary>
        /// <param name="serverCode">服务端编码</param>
        /// <param name="clientId">客户端标识（远端 IP:端口）</param>
        /// <param name="data">待发送字节</param>
        Task SendToClientAsync(string serverCode, string clientId, byte[] data);

        /// <summary>
        /// 向指定客户端发送字符串（按服务端配置编码）。
        /// </summary>
        Task SendToClientStringAsync(string serverCode, string clientId, string message);

        /// <summary>
        /// 向当前所有已连接客户端广播字节。
        /// </summary>
        Task BroadcastAsync(string serverCode, byte[] data);

        /// <summary>
        /// 向当前所有已连接客户端广播字符串。
        /// </summary>
        Task BroadcastStringAsync(string serverCode, string message);

        /// <summary>
        /// 获取指定服务端当前已连接的客户端标识列表。
        /// </summary>
        List<string> GetConnectedClients(string serverCode);

        /// <summary>
        /// 获取指定服务端最近收到的消息（按时间倒序，默认最近 20 条）。
        /// </summary>
        List<ReceivedSocketMessage> GetRecentMessages(string serverCode, int take = 20);
        Task InitAllServersAsync();
    }
}
