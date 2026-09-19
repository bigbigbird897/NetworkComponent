namespace ConnectionSocket.LocalEntity
{
    /// <summary>
    /// TCP Socket 服务端（监听端）配置。
    /// 与 appsettings.json 中 "SocketServerConfigs" 节点对应；
    /// 一个服务端监听一个端口，可接受多个客户端连接。
    /// </summary>
    public class SocketServerConfig
    {
        /// <summary>
        /// 服务端唯一编码，业务侧通过该编码定位对应监听实例。
        /// </summary>
        public string ServerCode { get; set; } = string.Empty;

        /// <summary>
        /// 监听 IP。0.0.0.0 表示监听本机所有网卡；127.0.0.1 仅本机回环。
        /// </summary>
        public string IpAddress { get; set; } = "0.0.0.0";

        /// <summary>
        /// 监听端口。
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// 字符串收发默认编码名（UTF-8 / GBK / ASCII 等），用于字符串广播/回显与最近消息解析。
        /// </summary>
        public string Encoding { get; set; } = "UTF-8";
    }
}
