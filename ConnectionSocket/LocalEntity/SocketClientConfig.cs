namespace ConnectionSocket.LocalEntity
{
    /// <summary>
    /// 通用 TCP Socket 客户端连接配置（与 appsettings.json 中 "SocketClientConfigs" 节点对应）。
    /// 用于透传报文与非标 TCP 设备通信，报文格式由调用方自行约定。
    /// </summary>
    public class SocketClientConfig
    {
        /// <summary>
        /// 设备唯一编码，业务侧通过该编码定位目标连接。
        /// </summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>
        /// 远端设备 IP 地址。
        /// </summary>
        public string IpAddress { get; set; } = string.Empty;

        /// <summary>
        /// 远端设备端口。
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 通信超时（毫秒），发送后等待应答的最长时间。
        /// </summary>
        public int TimeoutMs { get; set; } = 1000;

        /// <summary>
        /// 发送后是否等待设备应答（短连接、一问一答场景置 true）。
        /// </summary>
        public bool WaitResponse { get; set; } = true;

        /// <summary>
        /// 字符串收发默认编码名（UTF-8 / ASCII / GBK 等），仅用于字符串便捷接口。
        /// </summary>
        public string Encoding { get; set; } = "UTF-8";
    }
}
