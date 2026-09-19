using ConnectionSocket.LocalEntity;

namespace ConnectionSocket
{
    /// <summary>
    /// 通用 TCP Socket 客户端业务接口。
    /// 封装短连接的“连接-发送-接收-断开”过程，支持二进制与字符串两种便捷收发，
    /// 面向非标 TCP 设备、自定义协议透传场景。
    /// </summary>
    public interface ISocketClient
    {
        /// <summary>
        /// 根据设备编码获取对应 Socket 配置。
        /// </summary>
        /// <param name="deviceCode">设备唯一编码</param>
        SocketClientConfig GetDeviceConfig(string deviceCode);

        /// <summary>
        /// 获取全部已加载的 Socket 设备编码列表。
        /// </summary>
        List<string> GetAllDeviceCodes();

        /// <summary>
        /// 连接设备、发送字节并等待应答（短连接，完成后自动断开）。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="sendData">待发送字节</param>
        /// <param name="timeoutMs">可选超时，传入则覆盖配置默认超时</param>
        /// <returns>设备返回的原始字节（超时未收到应答时返回空数组）</returns>
        Task<byte[]> SendAndReceiveAsync(string deviceCode, byte[] sendData, int? timeoutMs = null);

        /// <summary>
        /// 便捷接口：按配置编码发送字符串并等待字符串应答。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="message">待发送字符串</param>
        /// <param name="timeoutMs">可选超时</param>
        /// <returns>设备返回字符串（按配置 Encoding 解码）</returns>
        Task<string> SendAndReceiveStringAsync(string deviceCode, string message, int? timeoutMs = null);

        /// <summary>
        /// 仅发送字节、不等待应答（短连接发送后即断开）。
        /// </summary>
        Task SendAsync(string deviceCode, byte[] sendData);

        /// <summary>
        /// 测试设备 TCP 端口是否可连通（仅建立连接后立即断开）。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <returns>连通成功返回 true，否则 false</returns>
        Task<bool> TestConnectionAsync(string deviceCode);
    }
}
