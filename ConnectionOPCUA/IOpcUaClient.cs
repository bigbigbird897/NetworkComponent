using ConnectionOPCUA.LocalEntity;

namespace ConnectionOPCUA
{
    /// <summary>
    /// OPC UA 客户端业务接口。
    /// 封装会话管理、节点读写、连接测试等能力，控制器面向本接口编程，
    /// 底层 OPCFoundation 库的会话细节对上层透明。
    /// </summary>
    public interface IOpcUaClient
    {
        /// <summary>
        /// 根据设备编码获取对应 OPC UA 配置。
        /// </summary>
        /// <param name="deviceCode">设备唯一编码</param>
        /// <returns>匹配的设备配置</returns>
        OpcUaClientConfig GetDeviceConfig(string deviceCode);

        /// <summary>
        /// 获取全部已加载的 OPC UA 设备编码列表。
        /// </summary>
        List<string> GetAllDeviceCodes();

        /// <summary>
        /// 批量获取所有 OPC UA 设备的连接状态（逐个 TestConnection 探测）。
        /// </summary>
        Task<Dictionary<string, bool>> GetAllDeviceStatusAsync();

        /// <summary>
        /// 读取单个节点的当前值。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="nodeId">节点 ID，如 "ns=2;s=Temperature" 或 "i=2258"</param>
        /// <returns>节点值（可能为数值/布尔/字符串等），读取失败抛异常</returns>
        Task<object?> ReadNodeAsync(string deviceCode, string nodeId);

        /// <summary>
        /// 批量读取多个节点，返回 节点ID -> 值 的字典。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="nodeIds">节点 ID 集合</param>
        Task<Dictionary<string, object?>> ReadNodesAsync(string deviceCode, IEnumerable<string> nodeIds);

        /// <summary>
        /// 向单个节点写入值。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <param name="nodeId">节点 ID</param>
        /// <param name="value">待写入值（字符串形式）</param>
        /// <param name="dataType">目标数据类型：Boolean/SByte/Int16/UInt16/Int32/UInt32/Int64/UInt64/Float/Double/String</param>
        Task WriteNodeAsync(string deviceCode, string nodeId, object value, string dataType = "Int32");

        /// <summary>
        /// 测试与指定设备的 OPC UA 服务端连接是否可达（建立并释放会话）。
        /// </summary>
        /// <param name="deviceCode">设备编码</param>
        /// <returns>连接成功返回 true，否则 false</returns>
        Task<bool> TestConnectionAsync(string deviceCode);

        /// <summary>
        /// 主动关闭并移除指定设备的会话。
        /// </summary>
        Task CloseAsync(string deviceCode);

        /// <summary>
        /// 关闭全部 OPC UA 会话（服务关停时调用）。
        /// </summary>
        Task CloseAllAsync();
    }
}
