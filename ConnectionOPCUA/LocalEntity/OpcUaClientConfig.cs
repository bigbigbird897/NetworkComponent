namespace ConnectionOPCUA.LocalEntity
{
    /// <summary>
    /// OPC UA 客户端单设备连接配置（与 appsettings.json 中 "OpcUaConfigs" 节点一一对应）。
    /// 采用与 ModbusTcp / Mqtt 一致的“一台设备一条配置”模型，按 DeviceCode 区分多设备。
    /// </summary>
    public class OpcUaClientConfig
    {
        /// <summary>
        /// 设备唯一编码，业务侧通过该编码定位对应 OPC UA 会话。
        /// </summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>
        /// OPC UA 服务端点地址，格式：opc.tcp://主机:端口
        /// 示例：opc.tcp://127.0.0.1:4840
        /// </summary>
        public string EndpointUrl { get; set; } = string.Empty;

        /// <summary>
        /// 会话名称（日志与服务端审计可见）。
        /// </summary>
        public string SessionName { get; set; } = "NetworkComponentOpcUaSession";

        /// <summary>
        /// 是否启用安全策略。工业内网调试阶段默认 false（None），
        /// 上线如需加密可置 true 并正确配置证书。
        /// </summary>
        public bool UseSecurity { get; set; } = false;

        /// <summary>
        /// 是否自动信任/接受未签名的服务端证书，内网调试置 true 可跳过证书信任弹窗。
        /// </summary>
        public bool AutoAcceptUntrustedCertificates { get; set; } = true;

        /// <summary>
        /// 建立连接超时（毫秒）。
        /// </summary>
        public int ConnectTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// 单次读写等服务端操作超时（毫秒）。
        /// </summary>
        public int OperationTimeoutMs { get; set; } = 15000;

        /// <summary>
        /// 用户名（匿名登录时留空）。
        /// </summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 密码（匿名登录时留空）。
        /// </summary>
        public string Password { get; set; } = string.Empty;
    }
}
